using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hx.Runner.Core.Repository;
using Hx.Runner.Core.Tools;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Core.Release;

public sealed record VelopackToolInvocation(string FileName, string ArgumentsPrefix, string PackagePath, string ExtractedToolPath);

public static class VelopackTool
{
    public const string ManifestRelativePath = "tools/velopack/velopack.version.json";

    public static VelopackToolInvocation Prepare(string repoRoot, string rid, string extractionRoot)
    {
        string repo = Path.GetFullPath(repoRoot);
        VelopackManifest manifest = ReadManifest(RepositoryPathResolver.ResolveInside(repo, ManifestRelativePath).FullPath);
        VelopackAsset asset = manifest.Assets.FirstOrDefault(a => string.Equals(a.Rid, rid, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No pinned Velopack CLI asset is mapped for RID '{rid}'. Run `tools fetch --tool velopack --rid {rid}` only after adding a manifest asset.");

        ValidateManifest(manifest, asset, rid);

        string inRepoPackage = RepositoryPathResolver.ResolveInside(repo, asset.ExecutablePath).FullPath;
        string packagePath = ToolStoreResolver.ResolveOrFallback(
            "velopack",
            manifest.Version,
            rid,
            asset.ExecutableName,
            asset.ExecutableSha256,
            inRepoPackage);
        VerifyPackage(packagePath, asset, rid);

        string extractTo = Path.Combine(extractionRoot, "velopack-tool-" + rid);
        if (Directory.Exists(extractTo))
        {
            Directory.Delete(extractTo, recursive: true);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(extractTo)!);
        ZipFile.ExtractToDirectory(packagePath, extractTo);
        string vpkDll = ResolveVpkDll(extractTo);
        return new VelopackToolInvocation("dotnet", Quote(vpkDll), packagePath, vpkDll);
    }

    private static VelopackManifest ReadManifest(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Pinned Velopack CLI manifest was not found: {path}");
        }

        return JsonSerializer.Deserialize<VelopackManifest>(File.ReadAllText(path), JsonContractSerializerOptions.Create())
            ?? throw new InvalidOperationException($"Pinned Velopack CLI manifest is empty: {path}");
    }

    private static void ValidateManifest(VelopackManifest manifest, VelopackAsset asset, string rid)
    {
        if (!string.Equals(manifest.Tool, "velopack", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Pinned Velopack CLI manifest has an unexpected tool identity: " + manifest.Tool);
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new InvalidOperationException("Pinned Velopack CLI manifest is missing a version.");
        }

        if (string.IsNullOrWhiteSpace(asset.ExecutablePath)
            || string.IsNullOrWhiteSpace(asset.ExecutableName)
            || string.IsNullOrWhiteSpace(asset.ExecutableSha256))
        {
            throw new InvalidOperationException($"Pinned Velopack CLI asset for '{rid}' is missing package path/name/hash metadata.");
        }
    }

    private static void VerifyPackage(string packagePath, VelopackAsset asset, string rid)
    {
        if (!File.Exists(packagePath))
        {
            throw new InvalidOperationException(
                $"Pinned Velopack CLI package for '{rid}' is missing: {packagePath}. Run `tools fetch --tool velopack --rid {rid}`.");
        }

        string actual = Sha256(packagePath);
        if (!string.Equals(actual, asset.ExecutableSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Pinned Velopack CLI package hash mismatch for '{rid}': expected {asset.ExecutableSha256}, actual {actual}. Refusing to run untrusted vpk.");
        }
    }

    private static string ResolveVpkDll(string extractTo)
    {
        string preferred = Path.Combine(extractTo, "tools", "net8.0", "any", "vpk.dll");
        if (File.Exists(preferred))
        {
            return preferred;
        }

        string? fallback = Directory.EnumerateFiles(Path.Combine(extractTo, "tools"), "vpk.dll", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        return fallback ?? throw new InvalidOperationException("Pinned Velopack CLI package did not contain tools/*/any/vpk.dll.");
    }

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Quote(string value) => '"' + value.Replace("\"", "\\\"", StringComparison.Ordinal) + '"';

    private sealed record VelopackManifest(
        [property: JsonPropertyName("tool")] string Tool,
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("assets")] IReadOnlyList<VelopackAsset> Assets);

    private sealed record VelopackAsset(
        [property: JsonPropertyName("rid")] string Rid,
        [property: JsonPropertyName("executablePath")] string ExecutablePath,
        [property: JsonPropertyName("executableSha256")] string ExecutableSha256,
        [property: JsonPropertyName("executableName")] string ExecutableName);
}
