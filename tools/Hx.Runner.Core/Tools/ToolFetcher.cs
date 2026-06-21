using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hx.Runner.Core.Io;
using Hx.Runner.Core.Repository;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Core.Tools;

/// <summary>
/// Deterministic, hash-verified provisioning of a vendored tool binary from its pinned
/// <c>tools/*/*.version.json</c> manifest. For the host RID it downloads the asset, verifies
/// <c>archiveSha256</c> (when present) then extracts <c>executableName</c> from the zip (else the raw
/// download IS the executable), verifies <c>executableSha256</c>, and writes <c>executablePath</c> —
/// fail-closed on any mismatch (no unverified binary is ever written).
///
/// The byte download is injected (<c>Func&lt;Uri, byte[]&gt;</c>) so the verify/extract path is unit-testable
/// with fixtures and no network; the CLI passes an <c>HttpClient</c>-backed delegate (mirroring
/// <see cref="Gitleaks.GitleaksUpdateChecker"/>'s graceful, short-timeout network use). The fetch is
/// fetch-if-missing: an already-present executable whose hash matches is reported <c>Fetched</c> without a download.
/// </summary>
public static class ToolFetcher
{
    /// <summary>The vendored tool manifests this scaffold provisions (relative to the repo root).</summary>
    public static readonly IReadOnlyList<string> ManifestRelativePaths =
    [
        "tools/gitleaks/gitleaks.version.json",
        "tools/sentrux/sentrux.version.json",
        "tools/gitversion/gitversion.version.json",
    ];

    /// <summary>Fetch every vendored tool for <paramref name="rid"/>, aggregating the per-tool outcomes.</summary>
    public static ToolFetchResult FetchAll(string repoRoot, string rid, Func<Uri, byte[]> fetchBytes)
    {
        List<ToolFetchOutcome> outcomes = [];
        foreach (string manifestRelative in ManifestRelativePaths)
        {
            string manifestPath = RepositoryPathResolver.ResolveInside(repoRoot, manifestRelative).FullPath;
            outcomes.Add(Fetch(manifestPath, rid, fetchBytes, repoRoot));
        }

        return Aggregate(rid, outcomes);
    }

    /// <summary>
    /// Fetch a single tool from <paramref name="manifestPath"/> for <paramref name="rid"/>.
    /// <paramref name="repoRoot"/> defaults to the manifest's repo (two levels up from <c>tools/&lt;tool&gt;/</c>) and
    /// anchors the relative <c>executablePath</c>; pass it explicitly from <see cref="FetchAll"/>.
    /// </summary>
    public static ToolFetchOutcome Fetch(string manifestPath, string rid, Func<Uri, byte[]> fetchBytes, string? repoRoot = null)
    {
        repoRoot ??= ResolveRepoRoot(manifestPath);

        ToolManifestDto? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ToolManifestDto>(File.ReadAllText(manifestPath), JsonContractSerializerOptions.Create());
        }
        catch (Exception ex)
        {
            return Failed("unknown", rid, ToolFetchFailureKind.DownloadFailed, null,
                $"Could not read the tool manifest '{manifestPath}': {ex.Message}");
        }

        if (manifest is null)
        {
            return Failed("unknown", rid, ToolFetchFailureKind.DownloadFailed, null,
                $"Tool manifest is empty: {manifestPath}");
        }

        string tool = string.IsNullOrWhiteSpace(manifest.Tool) ? "unknown" : manifest.Tool;
        ToolAssetDto? asset = manifest.Assets?.FirstOrDefault(a =>
            string.Equals(a.Rid, rid, StringComparison.OrdinalIgnoreCase));
        if (asset is null)
        {
            return new ToolFetchOutcome(tool, rid, ToolFetchStatus.Skipped, ToolFetchFailureKind.AssetUnavailable,
                null, $"No '{tool}' asset is mapped for host RID '{rid}'.");
        }

        if (string.IsNullOrWhiteSpace(asset.ExecutablePath) || string.IsNullOrWhiteSpace(asset.ExecutableSha256))
        {
            return Failed(tool, rid, ToolFetchFailureKind.DownloadFailed, asset.ExecutablePath,
                $"The '{tool}' asset for '{rid}' is missing executablePath/executableSha256.");
        }

        string exeFullPath = RepositoryPathResolver.ResolveInside(repoRoot, asset.ExecutablePath).FullPath;

        // Fetch-if-missing: an already-present executable whose hash matches needs no download.
        if (File.Exists(exeFullPath) &&
            HashMatches(FileHashing.Sha256OfFile(exeFullPath), asset.ExecutableSha256))
        {
            return new ToolFetchOutcome(tool, rid, ToolFetchStatus.Fetched, ToolFetchFailureKind.None,
                asset.ExecutablePath, $"'{tool}' already present and verified.");
        }

        byte[] downloaded;
        try
        {
            if (!Uri.TryCreate(asset.DownloadUrl, UriKind.Absolute, out Uri? url))
            {
                return Failed(tool, rid, ToolFetchFailureKind.DownloadFailed, asset.ExecutablePath,
                    $"The '{tool}' asset for '{rid}' has an invalid downloadUrl.");
            }

            downloaded = fetchBytes(url);
        }
        catch (Exception ex)
        {
            return Failed(tool, rid, ToolFetchFailureKind.DownloadFailed, asset.ExecutablePath,
                $"Download failed for '{tool}' ({rid}): {ex.Message}");
        }

        // Archive present → verify its hash, then extract executableName. Else the download IS the executable.
        byte[] executableBytes;
        if (!string.IsNullOrWhiteSpace(asset.ArchiveSha256))
        {
            if (!HashMatches(Sha256OfBytes(downloaded), asset.ArchiveSha256))
            {
                return Failed(tool, rid, ToolFetchFailureKind.ArchiveHashMismatch, asset.ExecutablePath,
                    $"Downloaded '{tool}' archive SHA-256 does not match the manifest.");
            }

            try
            {
                executableBytes = ExtractFromZip(downloaded, asset.ExecutableName);
            }
            catch (Exception ex)
            {
                return Failed(tool, rid, ToolFetchFailureKind.DownloadFailed, asset.ExecutablePath,
                    $"Could not extract '{asset.ExecutableName}' from the '{tool}' archive: {ex.Message}");
            }
        }
        else
        {
            executableBytes = downloaded;
        }

        if (!HashMatches(Sha256OfBytes(executableBytes), asset.ExecutableSha256))
        {
            return Failed(tool, rid, ToolFetchFailureKind.ExecutableHashMismatch, asset.ExecutablePath,
                $"Fetched '{tool}' executable SHA-256 does not match the manifest.");
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(exeFullPath)!);
            File.WriteAllBytes(exeFullPath, executableBytes);
        }
        catch (Exception ex)
        {
            return Failed(tool, rid, ToolFetchFailureKind.DownloadFailed, asset.ExecutablePath,
                $"Could not write the '{tool}' executable to '{asset.ExecutablePath}': {ex.Message}");
        }

        return new ToolFetchOutcome(tool, rid, ToolFetchStatus.Fetched, ToolFetchFailureKind.None,
            asset.ExecutablePath, $"'{tool}' fetched and verified for '{rid}'.");
    }

    /// <summary>Pass when every tool is present + verified; Fail if any failed closed (a skipped RID alone does not fail).</summary>
    private static ToolFetchResult Aggregate(string rid, IReadOnlyList<ToolFetchOutcome> outcomes)
    {
        StageOutcome outcome =
            outcomes.Any(o => o.Status == ToolFetchStatus.Failed) ? StageOutcome.Fail :
            outcomes.All(o => o.Status == ToolFetchStatus.Fetched) ? StageOutcome.Pass :
            StageOutcome.Blocked;
        return new ToolFetchResult(JsonContractDefaults.SchemaVersion, outcome, rid, outcomes);
    }

    private static byte[] ExtractFromZip(byte[] archive, string executableName)
    {
        using var stream = new MemoryStream(archive, writable: false);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        // Match the entry by file name (the executable may sit at the archive root or under a directory).
        ZipArchiveEntry entry = zip.Entries.FirstOrDefault(e =>
                string.Equals(e.Name, executableName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Archive does not contain '{executableName}'.");

        using Stream entryStream = entry.Open();
        using var buffer = new MemoryStream();
        entryStream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string Sha256OfBytes(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static bool HashMatches(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static ToolFetchOutcome Failed(
        string tool, string rid, ToolFetchFailureKind kind, string? executablePath, string reason) =>
        new(tool, rid, ToolFetchStatus.Failed, kind, executablePath, reason);

    // tools/<tool>/<tool>.version.json → repo root is two directories up from the manifest.
    private static string ResolveRepoRoot(string manifestPath)
    {
        string toolDir = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;     // tools/<tool>
        string toolsDir = Path.GetDirectoryName(toolDir)!;                            // tools
        return Path.GetDirectoryName(toolsDir)!;                                      // repo root
    }

    /// <summary>Minimal manifest projection — only the asset shape the fetch needs; extra fields are ignored.</summary>
    private sealed record ToolManifestDto(
        [property: JsonPropertyName("tool")] string? Tool,
        [property: JsonPropertyName("assets")] IReadOnlyList<ToolAssetDto>? Assets);

    private sealed record ToolAssetDto(
        [property: JsonPropertyName("rid")] string Rid,
        [property: JsonPropertyName("downloadUrl")] string DownloadUrl,
        [property: JsonPropertyName("archiveSha256")] string? ArchiveSha256,
        [property: JsonPropertyName("executablePath")] string ExecutablePath,
        [property: JsonPropertyName("executableSha256")] string ExecutableSha256,
        [property: JsonPropertyName("executableName")] string ExecutableName);
}
