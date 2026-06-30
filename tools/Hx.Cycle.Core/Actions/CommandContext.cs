using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core.Actions;

/// <summary>
/// 028 FR-010 / D6: the single decision-point context the <see cref="DotiActionProjector"/> evaluates descriptor
/// applicability against. Assembled ONCE per turn from the EXISTING read-models — the cycle state, a
/// <see cref="CycleCheckReport"/>, the single <see cref="CycleRecoveryPlan"/> projection, the release train, and a
/// gate proof — plus the optional bug-cycle phase. It introduces NO new freshness logic and runs NO second evaluator
/// (H6): the recovery tiers are read straight off the supplied recovery plan, which is the one
/// <see cref="CycleRecoveryPlanner"/> projection. All accessors are pure.
/// </summary>
public sealed class CommandContext
{
    public CommandContext(
        CycleState? state,
        CycleCheckReport? checkReport = null,
        CycleRecoveryPlan? recoveryPlan = null,
        CycleReleaseTrain? releaseTrain = null,
        GateProof? gateProof = null,
        string? bugPhase = null)
    {
        State = state;
        CheckReport = checkReport;
        RecoveryPlan = recoveryPlan;
        ReleaseTrain = releaseTrain;
        GateProof = gateProof;
        BugPhase = bugPhase;
    }

    public CycleState? State { get; }
    public CycleCheckReport? CheckReport { get; }
    public CycleRecoveryPlan? RecoveryPlan { get; }
    public CycleReleaseTrain? ReleaseTrain { get; }
    public GateProof? GateProof { get; }
    public string? BugPhase { get; }

    /// <summary>The last-authored stage marker, or null outside a cycle.</summary>
    public string? CurrentStage => State?.CurrentStage;

    /// <summary>True when there is no active cycle state (the <c>OutsideCycle</c> condition).</summary>
    public bool IsOutsideCycle => State is null;

    /// <summary>True when the supplied check report passed (no recovery needed).</summary>
    public bool CheckPassed => CheckReport?.Passed ?? false;

    /// <summary>True when the gate proof's outcome is a failure (the <c>GateFailed</c> condition).</summary>
    public bool GateFailed => GateProof is { } proof && proof.Outcome != StageOutcome.Pass;

    /// <summary>True when the supplied release train is present and valid (the <c>TrainValid</c> condition).</summary>
    public bool TrainValid => ReleaseTrain?.Valid ?? false;

    /// <summary>True when the single recovery projection contains a step at the given restamp-safety tier — read
    /// straight off the supplied plan, never re-evaluated (H6).</summary>
    public bool HasRecoveryTier(RestampSafety tier) =>
        RecoveryPlan?.Steps.Any(s => s.Safety == tier) ?? false;
}
