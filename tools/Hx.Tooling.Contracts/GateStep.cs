namespace Hx.Tooling.Contracts;

public sealed record GateStep(
    string Name,
    StageOutcome Outcome,
    IReadOnlyList<GateEvidence> Evidence,
    // 012 FR-019: wall-clock duration of the step in milliseconds, captured by the gate runner so the human trace
    // can show per-step timing. Additive nullable (M2) — null on a pre-012 proof and never a proof-hash input
    // (review/telemetry context only, M1).
    long? DurationMs = null);
