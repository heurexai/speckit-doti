namespace Hx.Tooling.Contracts;

/// <summary>
/// A single hygiene finding. Secret values are never carried here; only a
/// redacted description, optional fingerprint, and location metadata.
/// </summary>
public sealed record HygieneFinding(
    HygieneFindingCategory Category,
    HygieneSeverity Severity,
    string RuleId,
    string FilePath,
    int? Line,
    string Description,
    string? Fingerprint = null);
