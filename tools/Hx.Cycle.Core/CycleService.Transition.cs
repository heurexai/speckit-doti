using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

public sealed partial class CycleService
{
    private const string TransitionShape = "cycle-transition/v1";

    private CycleState? TransitionBeforeStamp(
        CycleStage target,
        string? suppliedFeature,
        CycleState? existing,
        string? releaseIntent)
    {
        if (existing is null)
        {
            return null;
        }

        bool startingNewFeature = target.Prereqs.Count == 0
            && !string.IsNullOrWhiteSpace(suppliedFeature)
            && !string.Equals(existing.Feature, suppliedFeature, StringComparison.OrdinalIgnoreCase);

        if (startingNewFeature)
        {
            if (string.Equals(existing.CurrentStage, target.Id, StringComparison.OrdinalIgnoreCase)
                && !CycleFeatureSlug.IsNumbered(existing.Feature)
                && CycleFeatureSlug.IsNumbered(suppliedFeature!))
            {
                return existing;
            }

            if (!string.Equals(existing.CurrentStage, "drift-review", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(target.Id, "specify", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Cannot start feature '{suppliedFeature}' from stage '{existing.CurrentStage}'. Complete drift-review before beginning another specification.");
            }

            // FR-038/BL-2: exclude the incoming feature's owned paths so its just-written spec is not read as the
            // prior feature's dirty tree (the transition still rebases the prior proofs — only the pre-check over-reads).
            CycleTransitionRecord transition = CommitStageTransition(
                existing, target.Id, releaseIntent, FeatureArtifactScope.OwnedPaths(_stageModel, suppliedFeature!));
            CycleCompletionRecord completed = CompletionFromTransition(transition, existing);
            CycleState nextTrain = RebaseStateAfterTransition(existing, transition) with
            {
                Feature = suppliedFeature!,
                CurrentStage = target.Id,
                Stages = [],
                Completion = null,
                CompletedUnreleasedCycles = Append(existing.CompletedUnreleasedCycles, completed),
            };
            _store.Write(nextTrain);
            return nextTrain;
        }

        if (string.Equals(existing.CurrentStage, target.Id, StringComparison.OrdinalIgnoreCase))
        {
            if (CanCommitReleaseStageRecovery(target) && CommitScopeInspector.Inspect(_repositoryRoot).HasStaged)
            {
                CycleTransitionRecord recovery = CommitStageTransition(existing, target.Id, releaseIntent);
                return RebaseStateAfterTransition(existing, recovery);
            }

            return existing;
        }

        if (!target.Prereqs.Contains(existing.CurrentStage, StringComparer.OrdinalIgnoreCase))
        {
            return existing;
        }

        CycleTransitionRecord direct = CommitStageTransition(existing, target.Id, releaseIntent);
        return RebaseStateAfterTransition(existing, direct);
    }

    private CycleTransitionRecord CommitStageTransition(
        CycleState state, string nextStage, string? releaseIntent, IReadOnlyList<string>? excludedOwnedPaths = null)
    {
        CycleStage current = _stageModel.Find(state.CurrentStage);
        // 039 WI1/FR-001: the ENGINE — not the agent — stages the leaving stage's declared produced artifact (scoped,
        // never `git add -A`), so an authored-but-untracked produced doc is captured by this transition commit instead
        // of being left orphaned in the working tree (the empty-transition-commit bug).
        StageProducesArtifact(current, state.Feature);
        TransitionReadiness readiness = ValidateTransitionReadiness(state, current, excludedOwnedPaths);
        if (readiness.Reasons.Count > 0)
        {
            throw new InvalidOperationException(
                $"Cannot transition from '{state.CurrentStage}' to '{nextStage}': {string.Join("; ", readiness.Reasons)}");
        }

        // 039 WI1/FR-002: fail closed ONLY when the leaving stage declares a `produces` artifact that is neither staged
        // here nor already committed (a genuine orphan/missing artifact) — NEVER when it is present-and-committed
        // unchanged (e.g. `clarify` re-declaring `specify`'s committed spec), which transitions as a normal no-change
        // commit. This narrows the silent `allowEmpty` path to exactly the orphan case (no doc-dance regression).
        EnsureProducesNotOrphaned(current, state.Feature, readiness.CommitReadiness.Scope);

        string message = TransitionCommitMessage(state, nextStage, releaseIntent);
        PendingCycleCommit pending = PreparePendingCommit(
            message,
            state,
            readiness.CommitReadiness,
            nextStage,
            TransitionShape);
        ProcessRunResult commit = RunGitCommit(pending.FullMessage, allowEmpty: !readiness.CommitReadiness.Scope.HasStaged);
        if (commit.ExitCode != 0)
        {
            RecoveryEvaluation afterFailure = RecoverStateIfNeeded();
            string detail = string.IsNullOrWhiteSpace(commit.StandardError)
                ? commit.StandardOutput.Trim()
                : commit.StandardError.Trim();
            throw new InvalidOperationException(
                $"git transition commit failed: {detail}; recovery={afterFailure.Report.Verdict}");
        }

        string commitSha = GitRefs.TryHeadSha(_repositoryRoot)
            ?? throw new InvalidOperationException("Could not resolve HEAD after transition commit.");
        CycleTransitionRecord transition = TransitionFromIntent(pending.Intent, commitSha);
        CycleState transitioned = RebaseStateAfterTransition(pending.StateWithIntent, transition);
        _store.Write(transitioned);
        return transition;
    }

    private static string TransitionCommitMessage(CycleState state, string nextStage, string? releaseIntent)
    {
        string message = $"{state.CurrentStage}: {state.Feature}";
        if (string.Equals(nextStage, "release", StringComparison.OrdinalIgnoreCase))
        {
            message += $"\n\n+semver: {ReleaseSemverSignal(releaseIntent)}";
        }

        return message;
    }

    /// <summary>
    /// 007 T041 (FR-044/SC-016): the GitVersion <c>+semver:</c> signal a FEATURE cycle's release-stage transition
    /// writes. A blank intent defaults to <c>minor</c> so GitVersion calculates a minor bump and <c>hx release</c>'s
    /// default intent (which follows that bump) validates by default — no more blank-intent "Release intent mismatch".
    /// An explicit <c>--release-intent</c> overrides (e.g. a feature that is really a patch). The bug-fix-only cycle is
    /// the assess→fix→test mini-cycle (FR-034); it never reaches this transition, so it writes no minor signal → patch.
    /// Public + pure so the default is unit-testable without a git repo.
    /// </summary>
    public static string ReleaseSemverSignal(string? releaseIntent) =>
        string.IsNullOrWhiteSpace(releaseIntent) ? "minor" : releaseIntent.Trim().ToLowerInvariant();

    private static bool CanCommitReleaseStageRecovery(CycleStage target) =>
        string.Equals(target.Id, "release", StringComparison.OrdinalIgnoreCase)
        && string.Equals(target.Kind, "release", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 039 WI1/FR-001: stage EXACTLY the leaving stage's declared <c>produces</c> path (scoped; never <c>git add -A</c>),
    /// so the coded transition commits the produced artifact the agent authored rather than relying on the agent to
    /// pre-stage it. A stage with no declared produces, or a produces file absent from disk, stages nothing (the
    /// absent case is caught by <see cref="EnsureProducesNotOrphaned"/>).
    /// </summary>
    private void StageProducesArtifact(CycleStage stage, string feature)
    {
        if (stage.Produces is not { } pattern)
        {
            return;
        }

        string rel = StageModel.ResolveProduces(pattern, feature);
        string full = Path.GetFullPath(Path.Combine(_repositoryRoot, rel.Replace('/', Path.DirectorySeparatorChar)));
        if (!File.Exists(full))
        {
            return;
        }

        ProcessRunner.Run(new ToolCommand("git", ["add", "--", rel], _repositoryRoot));
    }

    /// <summary>
    /// 039 WI1/FR-002: a declared <c>produces</c> must end up captured by the transition commit — either newly staged
    /// (by <see cref="StageProducesArtifact"/>) OR already tracked (committed unchanged by a prior transition). Neither
    /// means the stage claims an artifact it never produced: fail closed. A present-and-committed-unchanged produces is
    /// NOT an orphan and transitions normally (the same-artifact <c>clarify→plan</c> case).
    /// </summary>
    private void EnsureProducesNotOrphaned(CycleStage stage, string feature, CommitScope scope)
    {
        if (stage.Produces is not { } pattern)
        {
            return;
        }

        string rel = StageModel.ResolveProduces(pattern, feature);
        bool staged = scope.StagedPaths.Any(p => string.Equals(p, rel, StringComparison.OrdinalIgnoreCase));
        if (staged || IsPathTracked(rel))
        {
            return;
        }

        throw new CycleInputException(
            $"Stage '{stage.Id}' declares produces '{rel}' but it is neither staged nor committed — author the artifact "
            + $"before transitioning out of '{stage.Id}'.");
    }

    private bool IsPathTracked(string relPath) =>
        ProcessRunner.Run(new ToolCommand(
            "git", ["ls-files", "--error-unmatch", "--", relPath], _repositoryRoot)).ExitCode == 0;
}
