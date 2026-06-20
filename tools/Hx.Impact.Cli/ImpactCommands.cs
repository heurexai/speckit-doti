using Hx.Cli.Kernel;
using Hx.Impact.Core;
using Hx.Impact.Core.Planning;
using Hx.Tooling.Contracts;

namespace Hx.Impact.Cli;

/// <summary>
/// The Impact CLI's command bodies: map the deterministic <see cref="AffectedPlan"/> onto the <see cref="CliResult"/>
/// envelope. Kept out of <c>Program.cs</c> wiring so the plan→envelope mapping is unit-testable in-process.
/// A produced plan is always a successful command (exit 0); the fail-closed <c>full-gate-required</c> escalation is
/// surfaced as Direction (a NextAction) + <c>data.outcome</c>, never as a process failure. A planner exception
/// propagates to <see cref="CliHost"/>, which fail-closes to an Internal diagnostic — with no plan in the envelope,
/// a failure can never be misread as "no tests affected".
/// </summary>
public static class ImpactCommands
{
    /// <summary>Run the deterministic affected-test planner for a change set and wrap the plan in the envelope.</summary>
    public static CliResult Plan(CliMeta meta, string repo, string baseRef, string headRef, string configuration) =>
        FromPlan(meta, "plan", new AffectedTestPlanner().Plan(Path.GetFullPath(repo), baseRef, headRef, configuration));

    /// <summary>Emit the placeholder full plan (smoke / bootstrap) in the envelope.</summary>
    public static CliResult BootstrapPlan(CliMeta meta) =>
        FromPlan(meta, "bootstrap-plan", AffectedPlanFactory.BootstrapFullPlan());

    /// <summary>Map any affected plan onto the success envelope (exit 0) with outcome-specific Direction.</summary>
    public static CliResult FromPlan(CliMeta meta, string command, AffectedPlan plan) =>
        CliResults.Ok(meta, command, Summary(plan), plan, nextActions: NextActions(plan));

    internal static string Summary(AffectedPlan plan) => plan.Outcome switch
    {
        AffectedOutcome.Affected => $"Affected: {plan.SelectedTests.Count} test project(s) selected.",
        AffectedOutcome.NoTestsRequired => "No tests required (only documentation/generated paths changed).",
        AffectedOutcome.FullGateRequired => "Full gate required: the change could not be narrowed safely.",
        _ => $"Plan outcome: {plan.Outcome}.",
    };

    internal static IReadOnlyList<CliNextAction> NextActions(AffectedPlan plan) => plan.Outcome switch
    {
        AffectedOutcome.FullGateRequired =>
            [new CliNextAction("Run the full gate", "The change is broad or unattributed; run the whole suite.", "gate run --profile normal")],
        AffectedOutcome.Affected =>
            [new CliNextAction("Run the selected test projects", "Only these projects cover the change; see data.selectedTests for the exact commands.")],
        _ => [],
    };
}
