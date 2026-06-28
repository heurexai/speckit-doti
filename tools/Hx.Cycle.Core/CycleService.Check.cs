using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

public sealed partial class CycleService
{
    public CycleStatusReport Status()
    {
        RecoveryEvaluation recovery = RecoverStateIfNeeded();
        CycleState state = recovery.State
            ?? throw new InvalidOperationException(
                $"No cycle state at {CycleStateStore.RelativePath}; run `doti cycle stamp --stage <id> --feature <NNN-slug>` first.");

        if (TryCompletedClean(state, out CycleCompletionRecord? completion))
        {
            List<StageFreshnessResult> completed = state.Stages
                .Select(proof => new StageFreshnessResult(proof.Stage, StageFreshness.Completed,
                    $"cycle completed at commit {completion.CommitSha}"))
                .ToList();
            return new CycleStatusReport(JsonContractDefaults.SchemaVersion, state, completed, completion, recovery.Report, BuildReleaseTrain(state));
        }

        string identity = FreshnessIdentity(state);
        var evaluator = new FreshnessEvaluator(_repositoryRoot, _stageModel);
        List<StageFreshnessResult> freshness = state.Stages
            .Select(proof => evaluator.Evaluate(
                proof,
                state.Feature,
                identity,
                RequiresChangeSetIdentity(proof.Stage)))
            .ToList();

        return new CycleStatusReport(JsonContractDefaults.SchemaVersion, state, freshness, state.Completion, recovery.Report, BuildReleaseTrain(state));
    }

    /// <summary>Fail-closed chokepoint: every transitive prerequisite of <paramref name="stageId"/> must be
    /// stamped, fresh (artifact + change-set identity unchanged), and valid (a doc prerequisite has no open
    /// [NEEDS CLARIFICATION] marker). The CLI exits non-zero unless <see cref="CycleCheckReport.Passed"/>.</summary>
    public CycleCheckReport Check(string stageId) => Check(stageId, null);

    /// <inheritdoc cref="Check(string)"/>
    /// <param name="excludedOwnedPaths">Extra owned artifact paths (e.g. an INCOMING feature's just-written spec on a
    /// new-feature start) subtracted from the freshness change-set identity, on top of the current feature's own
    /// owned paths. So a stage's in-flight artifact never falsely stales a diff-kind prerequisite.</param>
    public CycleCheckReport Check(string stageId, IReadOnlyList<string>? excludedOwnedPaths)
    {
        CycleStage target = _stageModel.Find(stageId); // fail-closed on an unknown stage
        RecoveryEvaluation recovery = RecoverStateIfNeeded();
        CycleState? state = recovery.State;
        if (recovery.Report.Verdict == CycleRecoveryVerdict.Ambiguous)
        {
            return new CycleCheckReport(JsonContractDefaults.SchemaVersion, target.Id, false,
                [new StagePrereqResult("commit-recovery", "ambiguous", false, recovery.Report.Reason)],
                state?.Completion,
                recovery.Report);
        }

        if (TryCompletedCycleCheck(target, state, recovery.Report, out CycleCheckReport? completedReport))
        {
            return completedReport!;
        }

        string identity = FreshnessIdentity(state, excludedOwnedPaths);
        var evaluator = new FreshnessEvaluator(_repositoryRoot, _stageModel);

        var results = new List<StagePrereqResult>();
        foreach (string prereqId in ResolveTransitivePrerequisites(target))
        {
            results.Add(EvaluatePrerequisite(prereqId, state, evaluator, identity));
        }

        CycleReleaseTrain? releaseTrain = null;
        if (string.Equals(target.Id, "release", StringComparison.OrdinalIgnoreCase) && state is not null)
        {
            releaseTrain = BuildReleaseTrain(state);
            if (!releaseTrain.Valid)
            {
                foreach (string blocker in releaseTrain.Blockers)
                {
                    results.Add(new StagePrereqResult("release-train", "invalid", false, blocker));
                }
            }
        }

        bool passed = results.All(r => r.Ok);
        return new CycleCheckReport(JsonContractDefaults.SchemaVersion, target.Id, passed, results, state?.Completion, recovery.Report, releaseTrain);
    }

    /// <summary>The change-set identity used for prerequisite-FRESHNESS evaluation: the live <c>base..HEAD ∪ working
    /// tree</c> set with the cycle's OWN doc/review artifacts subtracted. A stage's in-flight owned artifact — the /08
    /// drift-review report staged for the release transition, or an incoming feature's just-written spec on a
    /// new-feature start (<paramref name="additionalExcluded"/>) — must not move the identity that decides whether a
    /// DIFF-kind prerequisite (implement) is still fresh. Safe: the only diff-kind stage produces no artifact, so no
    /// code edit is ever subtracted; doc/review artifacts are bound by their own proof hashes, not this identity.</summary>
    private string FreshnessIdentity(CycleState? state, IReadOnlyList<string>? additionalExcluded = null)
    {
        string baseRef = state?.BaseRef ?? GitRefs.ResolveBaseRef(_repositoryRoot);
        var excluded = new List<string>();
        if (state is not null)
        {
            excluded.AddRange(FeatureArtifactScope.OwnedPaths(_stageModel, state.Feature));
        }

        if (additionalExcluded is { Count: > 0 })
        {
            excluded.AddRange(additionalExcluded);
        }

        return ChangeSetIdentity.Of(_repositoryRoot, baseRef, "HEAD", excluded);
    }

}
