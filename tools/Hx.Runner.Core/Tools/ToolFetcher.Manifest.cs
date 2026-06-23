using System.Text.Json;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Core.Tools;

public static partial class ToolFetcher
{
    private static ToolManifestDto ReadManifestOrThrow(string manifestPath, string rid)
    {
        if (TryReadManifest(manifestPath, rid, out ToolManifestDto? manifest, out ToolFetchOutcome? failure))
        {
            throw new ToolFetchFailed(failure!);
        }

        return manifest!;
    }

    private static ToolAssetDto SelectAssetOrThrow(ToolManifestDto manifest, string tool, string rid)
    {
        if (TrySelectAsset(manifest, tool, rid, out ToolAssetDto? asset, out ToolFetchOutcome? failure))
        {
            throw new ToolFetchFailed(failure!);
        }

        return asset!;
    }

    private static void ValidateAssetOrThrow(string tool, string rid, ToolAssetDto asset)
    {
        if (TryValidateAsset(tool, rid, asset, out ToolFetchOutcome? failure))
        {
            throw new ToolFetchFailed(failure!);
        }
    }

    private static bool TryReadManifest(
        string manifestPath,
        string rid,
        out ToolManifestDto? manifest,
        out ToolFetchOutcome? failure)
    {
        failure = null;
        try
        {
            manifest = JsonSerializer.Deserialize<ToolManifestDto>(File.ReadAllText(manifestPath), JsonContractSerializerOptions.Create());
        }
        catch (Exception ex)
        {
            manifest = null;
            failure = Failed("unknown", rid, ToolFetchFailureKind.DownloadFailed, null,
                $"Could not read the tool manifest '{manifestPath}': {ex.Message}");
            return true;
        }

        if (manifest is not null)
        {
            return false;
        }

        failure = Failed("unknown", rid, ToolFetchFailureKind.DownloadFailed, null,
            $"Tool manifest is empty: {manifestPath}");
        return true;
    }

    private static string ToolName(ToolManifestDto manifest) =>
        string.IsNullOrWhiteSpace(manifest.Tool) ? "unknown" : manifest.Tool;

    private static bool TrySelectAsset(
        ToolManifestDto manifest,
        string tool,
        string rid,
        out ToolAssetDto? asset,
        out ToolFetchOutcome? failure)
    {
        asset = manifest.Assets?.FirstOrDefault(a =>
            string.Equals(a.Rid, rid, StringComparison.OrdinalIgnoreCase));
        failure = null;
        if (asset is not null)
        {
            return false;
        }

        failure = new ToolFetchOutcome(tool, rid, ToolFetchStatus.Skipped, ToolFetchFailureKind.AssetUnavailable,
            null, $"No '{tool}' asset is mapped for host RID '{rid}'.");
        return true;
    }

    private static bool TryValidateAsset(string tool, string rid, ToolAssetDto asset, out ToolFetchOutcome? failure)
    {
        failure = null;
        if (!string.IsNullOrWhiteSpace(asset.ExecutablePath) && !string.IsNullOrWhiteSpace(asset.ExecutableSha256))
        {
            return false;
        }

        failure = Failed(tool, rid, ToolFetchFailureKind.DownloadFailed, asset.ExecutablePath,
            $"The '{tool}' asset for '{rid}' is missing executablePath/executableSha256.");
        return true;
    }
}
