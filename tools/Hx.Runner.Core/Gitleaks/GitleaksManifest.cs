namespace Hx.Runner.Core.Gitleaks;

/// <summary>
/// Machine-readable description of the vendored Gitleaks release
/// (<c>tools/gitleaks/gitleaks.version.json</c>). Maps scaffold RIDs to upstream
/// release asset names so command code never hard-codes naming assumptions.
/// </summary>
public sealed record GitleaksManifest(
    int SchemaVersion,
    string Tool,
    string License,
    string Repository,
    string Version,
    string ReleaseUrl,
    string? ReleasePublishedAt,
    string? ChecksumUrl,
    string? ChecksumSha256,
    string ConfigPath,
    string? ConfigSha256,
    string? VendoredAt,
    string UpdateChannel,
    IReadOnlyList<GitleaksAsset> Assets);

public sealed record GitleaksAsset(
    string Rid,
    string AssetName,
    string DownloadUrl,
    string? ArchiveSha256,
    string ExecutablePath,
    string? ExecutableSha256,
    string ExecutableName,
    string SupportLevel);
