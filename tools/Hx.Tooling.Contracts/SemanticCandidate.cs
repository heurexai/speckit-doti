namespace Hx.Tooling.Contracts;

/// <summary>
/// FR-019: one ADVISORY semantic drift candidate — a changed-code chunk that is semantically close to a doc/skill/
/// help/test section, suggesting that text may have drifted from the behaviour the code now has. Never gating: it
/// carries no proof and authorizes no stamp; it points a human/deterministic check at a spot worth examining.
/// </summary>
public sealed record SemanticCandidate(
    string EvidenceSnippet,
    double Confidence,
    string RelatedPath,
    IReadOnlyList<string> AffectedAxes,
    IReadOnlyList<string> SuggestedDeterministicChecks);

/// <summary>
/// FR-018/020 (SC-008): the <c>hx doti drift-candidates</c> payload. Reports the ACTIVE embedding engine (FR-042),
/// how many chunks were embedded, and the ranked candidates. <see cref="AbsenceNote"/> states the honesty contract:
/// an EMPTY candidate list is NOT a clean-bill signal — only the deterministic drift-review gate is. This contract
/// carries no stamp/proof/gate-proof field; the semantic finder can never reach a gate (FR-020/SC-009).
/// </summary>
public sealed record DriftCandidatesResult(
    int SchemaVersion,
    string ActiveEngine,
    int ChunksEmbedded,
    IReadOnlyList<SemanticCandidate> Candidates,
    string AbsenceNote);
