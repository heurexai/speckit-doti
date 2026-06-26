namespace Hx.Tooling.Contracts;

public sealed record GateProof(
    int SchemaVersion,
    StageOutcome Outcome,
    IReadOnlyList<GateStep> Steps,
    IReadOnlyList<GateEvidence> Evidence,
    AffectedTestProof? AffectedTestProof = null,
    TaskCompletionProof? TaskCompletionProof = null,
    ReleaseDocumentationProof? ReleaseDocumentationProof = null,
    // 007 T006 (FR-029): the active tier + its ladder coverage. Recomputed by GateProofValidator so a silently
    // narrowed/downgraded ladder cannot mint a passing proof. Null on proofs from a pre-FR-029 runner.
    string? Tier = null,
    IReadOnlyList<GateLadderEntry>? LadderCoverage = null);

/// <summary>One tier-ladder coverage entry (gate step + its mode) recorded in a <see cref="GateProof"/> (FR-029).</summary>
public sealed record GateLadderEntry(string Step, string Mode);

public sealed record ReleaseDocumentationProof(
    int SchemaVersion,
    StageOutcome Outcome,
    string ReleaseNotes,
    IReadOnlyList<string> Features,
    IReadOnlyList<ReleaseDocumentationFileProof> Documents,
    IReadOnlyList<string> Blockers);

public sealed record ReleaseDocumentationFileProof(
    string Path,
    string Status,
    string Reason);
