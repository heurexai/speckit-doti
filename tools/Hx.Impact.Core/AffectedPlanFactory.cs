using Hx.Tooling.Contracts;

namespace Hx.Impact.Core;

public static class AffectedPlanFactory
{
    /// <summary>A conservative full-gate plan: the escalation result and the `bootstrap-plan` output.</summary>
    public static AffectedPlan FullGate(params string[] reasons) =>
        new(JsonContractDefaults.SchemaVersion,
            AffectedOutcome.FullGateRequired,
            [],
            [],
            reasons.Length == 0 ? ["Full gate required."] : reasons);

    public static AffectedPlan BootstrapFullPlan() =>
        FullGate("Affected-change planner invoked in bootstrap mode; full gate required.");
}
