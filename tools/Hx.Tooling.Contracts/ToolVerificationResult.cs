namespace Hx.Tooling.Contracts;

/// <summary>
/// Result of verifying an external tool localization (for example Gitleaks):
/// manifest presence, hash match, license copy, and rendered config.
/// </summary>
public sealed record ToolVerificationResult(
    int SchemaVersion,
    string Tool,
    bool Verified,
    StageOutcome Outcome,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Problems,
    string? Message = null);
