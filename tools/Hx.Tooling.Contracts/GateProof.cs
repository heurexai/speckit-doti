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
    IReadOnlyList<GateLadderEntry>? LadderCoverage = null,
    // 008 FR-028: the change-SCOPE skip dimension — recorded SEPARATELY from the tier ladder and recomputed from the
    // change set at validation (a scope skip can never be minted for a change that is not docs-only). Null on a
    // pre-FR-028 proof.
    GateScope? Scope = null);

/// <summary>One tier-ladder coverage entry (gate step + its mode) recorded in a <see cref="GateProof"/> (FR-029).</summary>
public sealed record GateLadderEntry(string Step, string Mode);

/// <summary>FR-028: the docs-only change-scope skip recorded in a <see cref="GateProof"/>. <see cref="DocsOnly"/> is
/// the AND of "affected plan = no-tests-required" and "review context = prose/docs-only" — when true, the
/// <see cref="ScopeSkippedSteps"/> (architecture + Sentrux) are skipped with a scope reason, distinct from a tier
/// skip and from a missing-config Fail. Recomputed from the change set at validation, so it is provable-not-bypassed.</summary>
public sealed record GateScope(
    int SchemaVersion,
    bool DocsOnly,
    string Reason,
    IReadOnlyList<string> ScopeSkippedSteps);

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
