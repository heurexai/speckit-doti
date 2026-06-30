using Hx.Cli.Kernel;
using Hx.Cycle.Core;
using Hx.Cycle.Core.Actions;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

// 028: the agent-gated reconciliation CLI surface — the reviewed-no-impact verb plus the workflow-affordance
// next-action projection (FR-010) and the FR-002 upstream-diff enrichment. Split into its own partial so the
// action-model + diff + review-rebind type references do not inflate RunnerCommands.DotiCycle.cs's fan-out.
public static partial class RunnerCommands
{
    /// <summary>
    /// 028 FR-010 / T014: the valid WORKFLOW next-actions at the current decision point, projected from the
    /// <see cref="DotiActionProjector"/> and mapped via <see cref="CliActionRendering"/> — the single source for the
    /// workflow-affordance class. Builds the <see cref="CommandContext"/> from the current stage's check + recovery
    /// plan; the <see cref="RestampSafety.ReviewedNoImpact"/> recovery action carries the lazily-surfaced upstream diff
    /// (FR-002) the verdict needs. Never throws — status is non-enforcing; on any error the surface degrades to no
    /// affordances rather than failing.
    /// </summary>
    private static IReadOnlyList<CliNextAction> WorkflowNextActions(
        string repo, CycleService service, CycleStatusReport report)
    {
        if (report.State.CurrentStage is not { Length: > 0 } currentStage)
        {
            return [];
        }

        try
        {
            CycleCheckReport check = service.Check(currentStage);
            CycleRecoveryPlan plan = service.RecoveryPlanFor(check);
            var context = new CommandContext(report.State, check, plan, report.ReleaseTrain);
            var projector = new DotiActionProjector(new DotiActionModel(service.StageModel));
            return projector.Project(context)
                .Select(action => EnrichWithDiff(repo, service, plan, action))
                .ToList();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            return [];
        }
    }

    /// <summary>FR-002: enrich the reviewed-no-impact recovery action with the lazily-surfaced line-level upstream diff
    /// of the changed prerequisite paths (the self-describing evidence the verdict needs); every other projected action
    /// maps through unchanged. The changed-path set comes from the SAME recovery plan the projector wrapped (the single
    /// CycleRecoveryPlanner projection) — never a second evaluator.</summary>
    private static CliNextAction EnrichWithDiff(
        string repo, CycleService service, CycleRecoveryPlan plan, ProjectedAction action)
    {
        if (action.Descriptor.Kind != CommandKind.ReviewRebind || action.Command is null)
        {
            return CliActionRendering.ToNextAction(action);
        }

        StageRecoveryStep? step = plan.Steps
            .FirstOrDefault(s => s.Safety == RestampSafety.ReviewedNoImpact);
        if (step is null)
        {
            return CliActionRendering.ToNextAction(action);
        }

        string diff = CycleRecoveryDiff.Surface(repo, service.StampedAtCommitOf(step.Stage), step.ChangedPrereqPaths);
        string why = action.Why;
        if (diff.Length > 0)
        {
            why += $"\n--- upstream diff ---\n{diff}";
        }

        return new CliNextAction(action.Label, why, action.Command);
    }

    /// <summary>
    /// 028 FR-002: build a recovery step's next-action. For an agent-attestable
    /// (<see cref="RestampSafety.ReviewedNoImpact"/>) step, lazily surface the line-level upstream diff of the changed
    /// prerequisite paths (the self-describing evidence the verdict needs) in the action's <c>Why</c>; every other step
    /// carries its single recommended next command unchanged.
    /// </summary>
    private static CliNextAction RecoveryNextAction(string repo, CycleService service, StageRecoveryStep step)
    {
        if (step.Safety != RestampSafety.ReviewedNoImpact)
        {
            return new CliNextAction($"Recover '{step.Stage}'", step.Reason ?? step.Status, step.NextCommand);
        }

        string diff = CycleRecoveryDiff.Surface(repo, service.StampedAtCommitOf(step.Stage), step.ChangedPrereqPaths);
        string why = step.Reason ?? step.Status;
        if (diff.Length > 0)
        {
            why += $"\n--- upstream diff ---\n{diff}";
        }

        return new CliNextAction($"Review + rebind '{step.Stage}' (read the diff first)", why, step.NextCommand);
    }
}
