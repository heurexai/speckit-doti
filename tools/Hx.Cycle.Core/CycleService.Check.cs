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
            return new CycleStatusReport(JsonContractDefaults.SchemaVersion, state, completed, completion, recovery.Report);
        }

        string identity = ChangeSetIdentity.Of(_repositoryRoot, state.BaseRef, "HEAD");
        var evaluator = new FreshnessEvaluator(_repositoryRoot, _stageModel);
        List<StageFreshnessResult> freshness = state.Stages
            .Select(proof => evaluator.Evaluate(proof, state.Feature, identity))
            .ToList();

        return new CycleStatusReport(JsonContractDefaults.SchemaVersion, state, freshness, state.Completion, recovery.Report);
    }

    /// <summary>Fail-closed chokepoint: every transitive prerequisite of <paramref name="stageId"/> must be
    /// stamped, fresh (artifact + change-set identity unchanged), and valid (a doc prerequisite has no open
    /// [NEEDS CLARIFICATION] marker). The CLI exits non-zero unless <see cref="CycleCheckReport.Passed"/>.</summary>
    public CycleCheckReport Check(string stageId)
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

        string baseRef = state?.BaseRef ?? GitRefs.ResolveBaseRef(_repositoryRoot);
        string identity = ChangeSetIdentity.Of(_repositoryRoot, baseRef, "HEAD");
        var evaluator = new FreshnessEvaluator(_repositoryRoot, _stageModel);

        var results = new List<StagePrereqResult>();
        foreach (string prereqId in ResolveTransitivePrerequisites(target))
        {
            results.Add(EvaluatePrerequisite(prereqId, state, evaluator, identity));
        }

        bool passed = results.All(r => r.Ok);
        return new CycleCheckReport(JsonContractDefaults.SchemaVersion, target.Id, passed, results, state?.Completion, recovery.Report);
    }

}
