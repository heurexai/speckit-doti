using Hx.Cycle.Core;
using Hx.Cycle.Core.Actions;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

/// <summary>
/// 028 FR-010 / B4 / T014: the descriptor → <see cref="CliNextAction"/> mapping for the WORKFLOW-affordance class. It
/// lives in <see cref="Hx.Runner.Cli"/> (which already references both <see cref="Hx.Cycle.Core"/> and the kernel) so
/// <c>Hx.Cli.Kernel</c>/<c>CliResults</c> stay domain-agnostic — the kernel never learns the cycle action model. The
/// workflow cycle results (stamp/check/refresh/status/recovery) build their <c>nextActions</c> by projecting the cycle
/// <see cref="CommandContext"/> through the <see cref="DotiActionProjector"/> and mapping each
/// <see cref="ProjectedAction"/> here. Payload-derived next-actions (the affected-test hints in <c>ImpactCommands</c>,
/// the per-prereq install strings in <c>Scaffold.Cli</c>, install-hook paths) are NOT a function of cycle state and
/// stay locally constructed (FR-010/B5 scope boundary) — they are not routed through this mapping.
/// </summary>
internal static class CliActionRendering
{
    /// <summary>Map one projected workflow action to a <see cref="CliNextAction"/>. The publish-boundary STOP has a null
    /// command (an operator-decision affordance, never an automated next command).</summary>
    public static CliNextAction ToNextAction(ProjectedAction action) =>
        new(action.Label, action.Why, action.Command);

    /// <summary>Project the valid workflow next-actions at a decision-point context and map them to
    /// <see cref="CliNextAction"/>s — the single source for the workflow-affordance class.</summary>
    public static IReadOnlyList<CliNextAction> ForContext(DotiActionProjector projector, CommandContext context) =>
        projector.Project(context).Select(ToNextAction).ToList();
}
