using System.Text.Json.Serialization;
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
public static partial class ToolFetcher
{
    /// <summary>The vendored tool manifests this scaffold provisions (relative to the repo root).</summary>
    public static readonly IReadOnlyList<string> ManifestRelativePaths =
    [
        "tools/gitleaks/gitleaks.version.json",
        "tools/sentrux/sentrux.version.json",
        "tools/gitversion/gitversion.version.json",
        "tools/velopack/velopack.version.json",
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
        try
        {
            ToolManifestDto manifest = ReadManifestOrThrow(manifestPath, rid);
            string tool = ToolName(manifest);
            ToolAssetDto asset = SelectAssetOrThrow(manifest, tool, rid);
            ValidateAssetOrThrow(tool, rid, asset);
            string exeFullPath = RepositoryPathResolver.ResolveInside(repoRoot, asset.ExecutablePath).FullPath;
            if (AlreadyVerified(exeFullPath, asset.ExecutableSha256))
            {
                return new ToolFetchOutcome(tool, rid, ToolFetchStatus.Fetched, ToolFetchFailureKind.None,
                    asset.ExecutablePath, $"'{tool}' already present and verified.");
            }

            byte[] downloaded = DownloadOrThrow(tool, rid, asset, fetchBytes);
            byte[] executableBytes = BuildExecutableBytesOrThrow(tool, rid, asset, downloaded);
            EnsureExecutableHash(tool, rid, asset, executableBytes);
            return WriteExecutable(tool, rid, asset, exeFullPath, executableBytes);
        }
        catch (ToolFetchFailed ex)
        {
            return ex.Outcome;
        }
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

    private sealed class ToolFetchFailed(ToolFetchOutcome outcome) : Exception
    {
        public ToolFetchOutcome Outcome { get; } = outcome;
    }
}
