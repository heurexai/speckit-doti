namespace Hx.Tooling.Contracts;

/// <summary>
/// Deterministic public-hygiene scan proof. Merges scaffold-specific findings
/// with redacted Gitleaks findings into one JSON contract.
/// </summary>
public sealed record HygieneScanResult(
    int SchemaVersion,
    StageOutcome Outcome,
    HygieneScope Scope,
    HygieneSource Source,
    int ScannedFileCount,
    IReadOnlyList<HygieneSkippedFile> SkippedFiles,
    ToolVerificationResult? GitleaksVerification,
    IReadOnlyList<HygieneFinding> Findings,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> AdvisoryGaps);
