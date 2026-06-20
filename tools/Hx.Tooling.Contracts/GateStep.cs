namespace Hx.Tooling.Contracts;

public sealed record GateStep(
    string Name,
    StageOutcome Outcome,
    IReadOnlyList<GateEvidence> Evidence);
