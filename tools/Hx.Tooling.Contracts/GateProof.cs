namespace Hx.Tooling.Contracts;

public sealed record GateProof(
    int SchemaVersion,
    StageOutcome Outcome,
    IReadOnlyList<GateStep> Steps,
    IReadOnlyList<GateEvidence> Evidence,
    AffectedTestProof? AffectedTestProof = null);
