namespace Hx.Sentrux.Core;

/// <summary>
/// Vendored Sentrux release manifest (`tools/sentrux/sentrux.version.json`).
/// Maps scaffold RIDs to fork release assets and pins the grammars required for
/// deterministic, offline analysis. Mirrors the Gitleaks manifest shape.
/// </summary>
public sealed record SentruxManifest(
    int SchemaVersion,
    string Tool,
    string License,
    string SourceRemote,
    string ReleaseTag,
    string SourceCommit,
    string DistributionIdentity,
    string UpdateChannel,
    string? VendoredAt,
    IReadOnlyList<SentruxAsset> Assets,
    IReadOnlyList<SentruxGrammar> Grammars,
    IReadOnlyList<string> RequiredFeatures);

public sealed record SentruxAsset(
    string Rid,
    string AssetName,
    string DownloadUrl,
    string? ArchiveSha256,
    string ExecutablePath,
    string? ExecutableSha256,
    string ExecutableName,
    string SupportLevel);

public sealed record SentruxGrammar(
    string Name,
    string Rid,
    string Path,
    string? Sha256);
