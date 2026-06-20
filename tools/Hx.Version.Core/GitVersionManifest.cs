namespace Hx.Version.Core;

/// <summary>
/// Machine-readable description of the vendored GitVersion release (<c>tools/gitversion/gitversion.version.json</c>),
/// mirroring the Gitleaks/Sentrux manifest pattern: maps host RIDs to the upstream asset + the vendored
/// executable + its SHA-256, so the runner never hard-codes naming and can verify the binary fail-closed.
/// </summary>
public sealed record GitVersionManifest(
    int SchemaVersion,
    string Tool,
    string License,
    string Repository,
    string Version,
    string ReleaseUrl,
    string? ReleasePublishedAt,
    string? VendoredAt,
    string UpdateChannel,
    IReadOnlyList<GitVersionAsset> Assets);

public sealed record GitVersionAsset(
    string Rid,
    string AssetName,
    string DownloadUrl,
    string? ArchiveSha256,
    string ExecutablePath,
    string? ExecutableSha256,
    string ExecutableName,
    string SupportLevel);
