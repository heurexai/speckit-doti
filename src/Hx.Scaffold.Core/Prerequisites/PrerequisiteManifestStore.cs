using System.Security.Cryptography;
using System.Text.Json;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Core.Prerequisites;

public sealed record LoadedPrerequisiteManifest(
    PrerequisiteManifest Manifest,
    string Path,
    string Sha256);

public static class PrerequisiteManifestStore
{
    public const string SourceRelativePath = "doti/core/prerequisites.json";
    public const string TargetRelativePath = ".doti/prerequisites.json";

    public static LoadedPrerequisiteManifest LoadFromSourceRoot(string sourceRoot)
    {
        string path = Path.GetFullPath(Path.Combine(
            sourceRoot, SourceRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!File.Exists(path))
        {
            throw new InvalidOperationException("trusted prerequisite manifest is missing: " + path);
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            PrerequisiteManifest? manifest = JsonSerializer.Deserialize<PrerequisiteManifest>(
                bytes, JsonContractSerializerOptions.Create());
            if (manifest is null || manifest.SchemaVersion != JsonContractDefaults.SchemaVersion)
            {
                throw new InvalidOperationException("trusted prerequisite manifest has an unsupported schema version");
            }

            if (manifest.Requirements.Count == 0)
            {
                throw new InvalidOperationException("trusted prerequisite manifest has no requirements");
            }

            return new LoadedPrerequisiteManifest(
                manifest,
                path,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("trusted prerequisite manifest is invalid JSON: " + ex.Message, ex);
        }
    }

    public static void WriteTargetCopy(string sourceRoot, string targetRepoRoot)
    {
        LoadedPrerequisiteManifest loaded = LoadFromSourceRoot(sourceRoot);
        string target = Path.GetFullPath(Path.Combine(
            targetRepoRoot, TargetRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(loaded.Path, target, overwrite: true);
    }
}
