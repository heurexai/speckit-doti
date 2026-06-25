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

            CycleTransitionRecord transition = CommitStageTransition(existing, target.Id, releaseIntent);
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
            return existing;
        }

        if (!target.Prereqs.Contains(existing.CurrentStage, StringComparer.OrdinalIgnoreCase))
        {
            return existing;
        }

        CycleTransitionRecord direct = CommitStageTransition(existing, target.Id, releaseIntent);
        return RebaseStateAfterTransition(existing, direct);
    }

    private CycleTransitionRecord CommitStageTransition(CycleState state, string nextStage, string? releaseIntent)
    {
        CycleStage current = _stageModel.Find(state.CurrentStage);
        TransitionReadiness readiness = ValidateTransitionReadiness(state, current);
        if (readiness.Reasons.Count > 0)
        {
            throw new InvalidOperationException(
                $"Cannot transition from '{state.CurrentStage}' to '{nextStage}': {string.Join("; ", readiness.Reasons)}");
        }

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
        if (string.Equals(nextStage, "release", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(releaseIntent))
        {
            message += $"\n\n+semver: {releaseIntent}";
        }

        return message;
    }
}
