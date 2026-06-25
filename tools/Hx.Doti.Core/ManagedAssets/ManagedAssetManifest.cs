using System.Text.Json;
using Hx.Tooling.Contracts;

namespace Hx.Doti.Core.ManagedAssets;

public static class ManagedAssetCategory
{
    public const string WorkflowTemplate = "workflow-template";
    public const string SkillGeneratedInstruction = "skill-generated-instruction";
    public const string DotiSource = "doti-source";
    public const string Metadata = "metadata";
}

public static class ManagedAssetState
{
    public const string Clean = "clean";
    public const string Modified = "modified";
    public const string Missing = "missing";
}

public sealed record ManagedAssetHashEntry(
    string Path,
    string Category,
    string HashProfile,
    string Sha256,
    string SourceFormat = "unknown",
    string Canonicalizer = "unknown",
    string IdentityPolicy = "content-hash",
    string UpdateConflictPolicy = "fail-on-managed-modification");

public sealed record ManagedAssetManifest(
    int SchemaVersion,
    IReadOnlyList<ManagedAssetHashEntry> Assets,
    IReadOnlyList<ManagedAssetHashEntry>? ObsoleteAssets = null);

public sealed record ManagedAssetStatus(
    string Path,
    string Category,
    string State,
    string HashProfile,
    string RecordedSha256,
    string? CurrentSha256,
    string? Reason,
    string SourceFormat = "unknown",
    string Canonicalizer = "unknown",
    string IdentityPolicy = "content-hash",
    string UpdateConflictPolicy = "fail-on-managed-modification");

public sealed record ManagedAssetScanResult(
    int SchemaVersion,
    string Outcome,
    IReadOnlyList<ManagedAssetStatus> Assets,
    IReadOnlyList<ManagedAssetStatus> ModifiedWorkflowTemplates,
    IReadOnlyList<ManagedAssetStatus> ModifiedSkillGeneratedInstructions,
    IReadOnlyList<ManagedAssetStatus> Missing);

public static class ManagedAssetManifestStore
{
    public const string RelativePath = ".doti/managed-assets.json";

    public static ManagedAssetManifest? Read(string repoRoot)
    {
        string path = FullPath(repoRoot);
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ManagedAssetManifest>(
            File.ReadAllText(path), JsonContractSerializerOptions.Create());
    }

    public static void Write(string repoRoot, ManagedAssetManifest manifest)
    {
        string path = FullPath(repoRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        JsonSerializerOptions options = JsonContractSerializerOptions.Create();
        options.WriteIndented = true;
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, options));
    }

    public static string FullPath(string repoRoot) =>
        Path.GetFullPath(Path.Combine(repoRoot, RelativePath.Replace('/', Path.DirectorySeparatorChar)));
}
