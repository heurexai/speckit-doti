namespace Hx.Tooling.Contracts;

/// <summary>JSON proof for <c>gate run</c>: the resolved lane plus the aggregated gate proof.</summary>
public sealed record GateRunResult(
    int SchemaVersion,
    LaneDecision Lane,
    GateProof Proof);
