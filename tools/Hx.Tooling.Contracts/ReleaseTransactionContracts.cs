using System.Linq;

namespace Hx.Tooling.Contracts;

/// <summary>
/// 039 WI2: the ordered mutating stages of <c>hx release</c>. Named so the test fault-hook can force a failure at a
/// chosen stage and the <see cref="RollbackReport"/> can say WHERE the release failed.
/// </summary>
public enum ReleaseStage
{
    Validation,
    Tag,
    Pack,
    Smoke,
    Copy,
    Record,
}

/// <summary>
/// 039 WI2/FR-030/FR-013: what the engine automatically rolled back after a failed release, so the agent is TOLD what
/// happened (not asked to clean up). Fail-closed: <see cref="AnyResidual"/> is true when a compensation itself failed,
/// and the CLI must never report such a revert as clean.
/// </summary>
public sealed record RollbackReport(
    ReleaseStage FailedStage,
    string Reason,
    IReadOnlyList<CompensationOutcome> Compensations)
{
    public bool AnyResidual => Compensations.Any(c => !c.Succeeded);
}

/// <summary>One compensating action's result during a rollback.</summary>
public sealed record CompensationOutcome(string Action, bool Succeeded, string? Detail);
