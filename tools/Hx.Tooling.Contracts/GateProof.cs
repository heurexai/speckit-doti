namespace Hx.Tooling.Contracts;

public sealed record GateProof(
    int SchemaVersion,
    StageOutcome Outcome,
    IReadOnlyList<GateStep> Steps,
    IReadOnlyList<GateEvidence> Evidence,
    AffectedTestProof? AffectedTestProof = null,
    TaskCompletionProof? TaskCompletionProof = null,
    ReleaseDocumentationProof? ReleaseDocumentationProof = null);

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
