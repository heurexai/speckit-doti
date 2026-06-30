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
    /// safe stale prerequisites — both <see cref="RestampSafety.SafeReinterpret"/> (a runner/binding migration) and
    /// 027 <see cref="RestampSafety.ReBindContentEqual"/> (a planner-gated pure edge/reorder) — in prerequisite-first
    /// declaration order (the planner preserves the check's transitive order), reusing the <see cref="Stamp"/> path
    /// which recomputes the runner-bound hashes. The plan is RE-DERIVED after each safe re-stamp so a chain settles
    /// in one pass: rebinding an upstream can promote a downstream's gated ReBindContentEqual to safe. RerunRequired /
    /// NotBound / inserted-stage steps are left untouched and reported, so refresh never silently re-interprets a real
    /// input change. This is the only mutating refresh path; <see cref="RecoveryPlan"/> stays read-only.
    /// </summary>
    public CycleRefreshResult Refresh(string target, bool applySafe)
    {
        CycleRecoveryPlan plan = RecoveryPlan(target);
        var refreshed = new List<string>();
        if (applySafe)
        {
            var attempted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Fixpoint over the re-derived plan: stamp one safe step at a time, then re-derive so a freshly
            // rebound upstream can unlock a downstream's gated ReBindContentEqual. `attempted` guarantees
            // termination — a step that fails to become Fresh is never retried (it re-surfaces in `remaining`).
            while (true)
            {
                StageRecoveryStep? next = plan.Steps.FirstOrDefault(s =>
                    (s.Safety is RestampSafety.SafeReinterpret or RestampSafety.ReBindContentEqual)
                    && !attempted.Contains(s.Stage));
                if (next is null)
                {
                    break;
                }

                attempted.Add(next.Stage);
                try
                {
                    Stamp(next.Stage, feature: null, baseRef: null);
                    refreshed.Add(next.Stage);
                }
                catch (InvalidOperationException)
                {
                    // A prerequisite is not yet fresh (e.g. it is RerunRequired); leave this step — it re-appears
                    // in the re-derived `remaining` below rather than being claimed as refreshed.
                }

                plan = RecoveryPlan(target);
            }
        }

        // apply-safe: `plan` is already the re-derived plan, so the refreshed stages have dropped out and only
        // true blockers remain. dry run: the full plan (nothing was applied), every safe + blocking step.
        IReadOnlyList<StageRecoveryStep> remaining = plan.Steps;

        return new CycleRefreshResult(JsonContractDefaults.SchemaVersion, target, applySafe, refreshed, remaining);
    }
}
