using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

public sealed partial class CycleService
{
    /// <summary>Read-only recovery projection for a target stage (FR-001): what is stale, why, and the single
    /// recommended next command per blocking prerequisite. Never mutates.</summary>
    public CycleRecoveryPlan RecoveryPlan(string target) =>
        CycleRecoveryPlanner.Project(Check(target), _stageModel);

    /// <summary>Project an already-computed check report into a recovery plan (so <c>cycle check</c> can attach
    /// next-actions without re-running the check).</summary>
    public CycleRecoveryPlan RecoveryPlanFor(CycleCheckReport report) =>
        CycleRecoveryPlanner.Project(report, _stageModel);

    /// <summary>
    /// Recover a target stage's freshness (FR-005/006/007). With <paramref name="applySafe"/>, re-stamps ONLY the
    /// SafeReinterpret stale prerequisites — in prerequisite-first declaration order (the planner preserves the
    /// check's transitive order), reusing the <see cref="Stamp"/> path which recomputes the runner-bound hashes.
    /// RerunRequired / NotBound steps are left untouched and reported, so refresh never silently re-interprets a
    /// real input change. This is the only mutating refresh path; <see cref="RecoveryPlan"/> stays read-only.
    /// </summary>
    public CycleRefreshResult Refresh(string target, bool applySafe)
    {
        CycleRecoveryPlan plan = RecoveryPlan(target);
        var refreshed = new List<string>();
        if (applySafe)
        {
            foreach (StageRecoveryStep step in plan.Steps.Where(s => s.Safety == RestampSafety.SafeReinterpret))
            {
                try
                {
                    Stamp(step.Stage, feature: null, baseRef: null);
                    refreshed.Add(step.Stage);
                }
                catch (InvalidOperationException)
                {
                    // A prerequisite is not yet fresh (e.g. it is RerunRequired); leave this step — it re-appears
                    // in the re-derived `remaining` below rather than being claimed as refreshed.
                }
            }
        }

        // apply-safe: re-derive so the refreshed stages drop out and only true blockers remain.
        // dry run: the full plan (nothing was applied), so the caller sees every safe + blocking step.
        IReadOnlyList<StageRecoveryStep> remaining = applySafe ? RecoveryPlan(target).Steps : plan.Steps;

        return new CycleRefreshResult(JsonContractDefaults.SchemaVersion, target, applySafe, refreshed, remaining);
    }
}
