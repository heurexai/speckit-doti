namespace Hx.Cycle.Core.Actions;

/// <summary>
/// 028 FR-010: the class a <see cref="CommandDescriptor"/> belongs to — the workflow-affordance taxonomy the action
/// model is scoped to. Payload-derived next-actions (affected-test hints, per-prereq install strings, hook paths) are
/// NOT modeled here (they are functions of a computed result, not cycle state) and stay locally constructed.
/// </summary>
public enum CommandKind
{
    /// <summary>Advance to the next stage in the chain (one enabled per stage — the registry static-invariant).</summary>
    Advance,

    /// <summary>Recover a stale prerequisite — wraps a <see cref="CycleRecoveryPlanner"/> step (a refresh, the
    /// reviewed-no-impact verb, or a re-run). Never a second freshness evaluator.</summary>
    Recovery,

    /// <summary>The agent-gated reviewed-no-impact rebind verb (FR-003).</summary>
    ReviewRebind,

    /// <summary>The publish-boundary STOP — an operator-decision affordance with no command (<c>Command == null</c>).</summary>
    PublishBoundary,

    /// <summary>The release train-loop affordance (start the next feature / run release).</summary>
    TrainLoop,

    /// <summary>A bug mini-cycle step (assess → fix → test).</summary>
    BugPhase,

    /// <summary>A utility skill that runs by name outside the numbered cycle (doti-bug/amend/converge/drift-fix/
    /// constitution/auto/upgrade) — a <c>Utility</c> descriptor so deleting <c>skills.json nextStage</c> never strands it.</summary>
    Utility,
}
