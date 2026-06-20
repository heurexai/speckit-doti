namespace Hx.Tooling.Contracts;

/// <summary>
/// Result of an explicit, network-enabled Gitleaks update check. Normal gates
/// stay offline; this runs only on demand.
/// </summary>
public sealed record GitleaksUpdateCheck(
    int SchemaVersion,
    bool ManifestPresent,
    bool UpdateCheckPerformed,
    string? CurrentVersion,
    string? LatestVersion,
    bool? UpdateAvailable,
    string? ReleaseUrl,
    IReadOnlyList<string> Notes);
