namespace Hx.Tooling.Contracts;

public sealed record LaneDecision(
    Lane Lane,
    StageOutcome Outcome,
    string Reason);
