using System.Text.Json;
using Hx.Runner.Core.Platform;
using Hx.Runner.Core.Tools;

namespace Hx.Scaffold.Core;

/// <summary>
/// Populates the shared <see cref="ToolStore"/> from the scaffold's vendored tool manifests + binaries, so a
/// generated solution resolves verified tool binaries from one machine-global store instead of carrying a
/// ~127MB per-solution copy. Reads each <c>tools/&lt;tool&gt;/*.version.json</c> generically (the three
/// manifests differ only in their version field — <c>version</c> vs sentrux's <c>releaseTag</c>), takes the
/// host-RID asset, and installs the source binary into the store via <see cref="StorePopulator"/> (SHA-256
/// verified, fail-closed). Best-effort: a tool whose source binary is absent is skipped — the generated repo
/// can still self-provision in-repo via <c>tools fetch</c>; this never throws out of <c>new</c>.
/// </summary>
public static class StoreProvisioner
{
    private static readonly string[] ManifestRelativePaths =
    [
        "tools/gitleaks/gitleaks.version.json",
        "tools/sentrux/sentrux.version.json",
        "tools/gitversion/gitversion.version.json",
    ];

    public static void PopulateFromVendoredTools(string sourceRepoRoot)
    {
        string rid = HostPlatformDetector.DetectCurrent().RuntimeIdentifier;
        foreach (string manifestRelative in ManifestRelativePaths)
        {
            try
            {
                TryPopulate(sourceRepoRoot, manifestRelative, rid);
            }
            catch
            {
                // best-effort: never break `new` on a tool-store population failure.
            }
        }
    }

    private static void TryPopulate(string sourceRepoRoot, string manifestRelative, string rid)
    {
        string manifestPath = Path.Combine(sourceRepoRoot, manifestRelative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(manifestPath))
        {
            return;
        }

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        if (!TryReadToolManifest(doc.RootElement, out string tool, out string version, out JsonElement assets))
        {
            return;
        }

        JsonElement? asset = FindAssetForRid(assets, rid);
        if (asset is null)
        {
            return;
        }

        TryInstallAsset(sourceRepoRoot, tool, version, rid, asset.Value);
    }

    private static bool TryReadToolManifest(JsonElement root, out string tool, out string version, out JsonElement assets)
    {
        tool = GetString(root, "tool") ?? "unknown";
        version = GetString(root, "version") ?? GetString(root, "releaseTag") ?? "";
        bool hasAssets = root.TryGetProperty("assets", out assets);
        return !string.IsNullOrWhiteSpace(version) && hasAssets;
    }

    private static JsonElement? FindAssetForRid(JsonElement assets, string rid)
    {
        foreach (JsonElement asset in assets.EnumerateArray())
        {
            if (string.Equals(GetString(asset, "rid"), rid, StringComparison.OrdinalIgnoreCase))
            {
                return asset;
            }
        }

        return null;
    }

    private static void TryInstallAsset(string sourceRepoRoot, string tool, string version, string rid, JsonElement asset)
    {
        string? exePathRel = GetString(asset, "executablePath");
        string? exeSha = GetString(asset, "executableSha256");
        string? exeName = GetString(asset, "executableName");
        if (string.IsNullOrWhiteSpace(exePathRel) || string.IsNullOrWhiteSpace(exeSha) || string.IsNullOrWhiteSpace(exeName))
        {
            return;
        }

        string exeFull = Path.Combine(sourceRepoRoot, exePathRel.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(exeFull))
        {
            return; // binary not vendored locally; the generated repo self-provisions in-repo via `tools fetch`.
        }

        StorePopulator.InstallBytes(tool, version, rid, exeName, File.ReadAllBytes(exeFull), exeSha, source: "vendored");
    }

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
