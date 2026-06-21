using System.Text.Json;
using Hx.Runner.Core.Repository;
using Hx.Runner.Core.Tools;
using Hx.Tooling.Contracts;

namespace Hx.Sentrux.Core;

public static class SentruxToolPathResolver
{
    public const string Tool = "sentrux";
    public const string ManifestRelativePath = "tools/sentrux/sentrux.version.json";

    public static string ResolveRepoRelativeToolPath(string runtimeIdentifier)
    {
        string executableName = runtimeIdentifier.StartsWith("win-", StringComparison.OrdinalIgnoreCase)
            ? "sentrux.exe"
            : "sentrux";

        return $"tools/sentrux/bin/{runtimeIdentifier}/{executableName}";
    }

    /// <summary>
    /// The absolute path to the Sentrux executable to run: the verified shared-store entry when present,
    /// otherwise the in-repo vendored path. Falls back to the in-repo path on any manifest problem, so
    /// resolution is never worse than the pre-store behavior. (Only the binary moves to the store; the
    /// grammar — which has no downloadUrl — stays vendored in-repo and is staged from there.)
    /// </summary>
    public static string ResolveExecutable(string repositoryRoot, string runtimeIdentifier)
    {
        string inRepo = RepositoryPathResolver
            .ResolveInside(repositoryRoot, ResolveRepoRelativeToolPath(runtimeIdentifier)).FullPath;

        try
        {
            string manifestFull = RepositoryPathResolver.ResolveInside(repositoryRoot, ManifestRelativePath).FullPath;
            if (!File.Exists(manifestFull))
            {
                return inRepo;
            }

            SentruxManifest? manifest = JsonSerializer.Deserialize<SentruxManifest>(
                File.ReadAllText(manifestFull), JsonContractSerializerOptions.Create());
            SentruxAsset? asset = manifest?.Assets.FirstOrDefault(a =>
                string.Equals(a.Rid, runtimeIdentifier, StringComparison.OrdinalIgnoreCase));
            if (manifest is null || asset is null || string.IsNullOrWhiteSpace(asset.ExecutableSha256))
            {
                return inRepo;
            }

            return ToolStoreResolver.ResolveOrFallback(
                Tool, manifest.ReleaseTag, runtimeIdentifier, asset.ExecutableName, asset.ExecutableSha256, inRepo);
        }
        catch
        {
            return inRepo;
        }
    }
}
