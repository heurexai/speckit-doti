namespace Hx.Tooling.Contracts;

/// <summary>JSON proof for <c>gate run</c>: the resolved lane plus the aggregated gate proof.</summary>
public sealed record GateRunResult(
    int SchemaVersion,
    LaneDecision Lane,
    GateProof Proof,
    // 012 (FR-009): the operator-facing visibility trace, additively on the ENVELOPE (M1: never on the hashed
    // proof). Additive nullable (M2) — null on a pre-012 result and from the lane-fail fast path; the proof hashes
    // are byte-unchanged by this field.
    GateTrace? Trace = null);
