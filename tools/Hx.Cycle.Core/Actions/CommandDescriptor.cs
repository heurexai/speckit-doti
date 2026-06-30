namespace Hx.Cycle.Core.Actions;

/// <summary>
/// 028 FR-010 / D6: one workflow-affordance descriptor — the single source of a next-action's exact invocation + when
/// it applies. <see cref="Id"/> is the stable descriptor id (recovery descriptors tag the wrapped
/// <see cref="StageRecoveryStep"/> with it — never a second evaluator). <see cref="InvocationTemplate"/> is the ONE
/// source of the exact flags (null for the publish-boundary STOP, which is an operator-decision affordance with no
/// command). <see cref="When"/> is the declarative applicability the projector evaluates AND renders the "available
/// when …" from. <see cref="Label"/>/<see cref="Why"/> are the agent-facing affordance text.
/// </summary>
public sealed record CommandDescriptor(
    string Id,
    CommandKind Kind,
    string Label,
    string Why,
    string? InvocationTemplate,
    Applicability When)
{
    /// <summary>True when this descriptor applies at the supplied decision-point context.</summary>
    public bool AppliesTo(CommandContext context) => When.Evaluate(context);

    /// <summary>The human "available when …" rendered from the SAME declarative condition the projector evaluates.</summary>
    public string AvailableWhen() => When.Describe();
}

/// <summary>A descriptor projected as available at a concrete decision point: the descriptor plus the resolved
/// invocation (with <c>{feature}</c>/<c>{stage}</c> substituted) the agent runs. <see cref="Command"/> is null for the
/// publish-boundary STOP.</summary>
public sealed record ProjectedAction(CommandDescriptor Descriptor, string Label, string Why, string? Command);
