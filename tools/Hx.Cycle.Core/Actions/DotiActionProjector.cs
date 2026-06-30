namespace Hx.Cycle.Core.Actions;

/// <summary>
/// 028 FR-010 / D6: the pure projection from a <see cref="CommandContext"/> to the valid workflow next-actions. It
/// yields (1) the static descriptors whose declarative <see cref="CommandDescriptor.When"/> applies at the context, and
/// (2) the recovery menu — the SINGLE <see cref="CycleRecoveryPlanner"/> projection on the context, each
/// <see cref="StageRecoveryStep"/> wrapped as a <see cref="ProjectedAction"/> tagged with its descriptor id (never a
/// second freshness evaluator — H6). The reviewed-no-impact verb's <c>{stage}</c> placeholder is substituted to the
/// concrete stale stage. Pure — no git, no I/O.
/// </summary>
public sealed class DotiActionProjector
{
    private readonly DotiActionModel _model;

    public DotiActionProjector(DotiActionModel model) => _model = model;

    /// <summary>The valid next-actions at the supplied decision point.</summary>
    public IReadOnlyList<ProjectedAction> Project(CommandContext context)
    {
        var actions = new List<ProjectedAction>();
        actions.AddRange(StaticActions(context));
        actions.AddRange(RecoveryActions(context));
        return actions;
    }

    private IEnumerable<ProjectedAction> StaticActions(CommandContext context)
    {
        string? staleStage = FirstReviewRebindStage(context);
        foreach (CommandDescriptor descriptor in _model.Descriptors)
        {
            if (!descriptor.AppliesTo(context))
            {
                continue;
            }

            yield return new ProjectedAction(
                descriptor,
                descriptor.Label,
                descriptor.Why,
                ResolveInvocation(descriptor.InvocationTemplate, staleStage));
        }
    }

    /// <summary>
    /// The recovery menu — the single <see cref="CycleRecoveryPlan"/> projection, each step tagged with its descriptor
    /// id (<see cref="DotiActionModel.RecoveryDescriptorIdFor"/>). Wraps the steps; never re-evaluates freshness. The
    /// step's already-computed <see cref="StageRecoveryStep.NextCommand"/> is the resolved invocation.
    /// </summary>
    private IEnumerable<ProjectedAction> RecoveryActions(CommandContext context)
    {
        if (context.RecoveryPlan is not { } plan)
        {
            yield break;
        }

        foreach (StageRecoveryStep step in plan.Steps)
        {
            string id = DotiActionModel.RecoveryDescriptorIdFor(step);
            CommandDescriptor descriptor = RecoveryDescriptor(id, step);
            yield return new ProjectedAction(
                descriptor,
                $"Recover '{step.Stage}'",
                step.Reason ?? step.Status,
                string.IsNullOrWhiteSpace(step.NextCommand) ? null : step.NextCommand);
        }
    }

    private static CommandDescriptor RecoveryDescriptor(string id, StageRecoveryStep step) =>
        new(
            id,
            id == DotiActionModel.RecoveryReviewRebindId ? CommandKind.ReviewRebind : CommandKind.Recovery,
            $"Recover '{step.Stage}'",
            step.Reason ?? step.Status,
            string.IsNullOrWhiteSpace(step.NextCommand) ? null : step.NextCommand,
            Applicability.RecoveryTier(step.Safety ?? RestampSafety.RerunRequired));

    /// <summary>The first stale stage at a <see cref="RestampSafety.ReviewedNoImpact"/> tier in the recovery plan — the
    /// concrete <c>{stage}</c> the reviewed-no-impact verb descriptor's invocation is substituted with.</summary>
    private static string? FirstReviewRebindStage(CommandContext context) =>
        context.RecoveryPlan?.Steps
            .FirstOrDefault(s => s.Safety == RestampSafety.ReviewedNoImpact)?.Stage;

    private static string? ResolveInvocation(string? template, string? staleStage)
    {
        if (template is null)
        {
            return null;
        }

        return staleStage is null ? template : template.Replace("{stage}", staleStage);
    }
}
