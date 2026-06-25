using System.Text.Json;
using Hx.Doti.Core.ManagedAssets;
using Hx.Scaffold.Core.Prerequisites;
using Hx.Tooling.Contracts;
using Hx.Version.Core;

namespace Hx.Scaffold.Core.Versioning;

public static class ScaffoldVersionRelation
{
    public const string Unknown = "unknown";
    public const string Behind = "behind";
    public const string Equal = "equal";
    public const string Newer = "newer";
}

public sealed record ScaffoldVersionIdentity(
    string Version,
    string NormalizedVersion,
    string? ReleaseTag,
    string? SourceCommit,
    string? BuildMetadata,
    string Source,
    string? ReleaseAssetName = null,
    string? ReleaseAssetSha256 = null,
    string? ExecutablePath = null,
    string? ApplicationDirectory = null);

public sealed record ScaffoldVersionStamp(
    int SchemaVersion,
    ScaffoldVersionIdentity Installed);

public sealed record ManagedAssetModificationSummary(
    string State,
    IReadOnlyList<ManagedAssetStatus> ModifiedWorkflowTemplates,
    IReadOnlyList<ManagedAssetStatus> ModifiedSkillGeneratedInstructions,
    IReadOnlyList<ManagedAssetStatus> Missing);

public sealed record ScaffoldVersionReport(
    int SchemaVersion,
    ScaffoldVersionIdentity Running,
    ScaffoldVersionIdentity? Target,
    string TargetRelation,
    ManagedAssetModificationSummary? ManagedAssets,
    IReadOnlyList<string> Diagnostics,
    PrerequisiteCheckReport? Prerequisites = null);

public static class ScaffoldVersionReporter
{
    public const string StampRelativePath = ".doti/scaffold-version.json";

    public static ScaffoldVersionIdentity IdentityFromVersion(
        string version,
        string source,
        string? releaseAssetName = null,
        string? releaseAssetSha256 = null,
        string? executablePath = null,
        string? applicationDirectory = null)
    {
        string trimmed = string.IsNullOrWhiteSpace(version) ? "0.0.0" : version.Trim();
        string normalized = NormalizeSemVer(trimmed);
        string? build = null;
        int plus = trimmed.IndexOf('+');
        if (plus >= 0 && plus + 1 < trimmed.Length)
        {
            build = trimmed[(plus + 1)..];
        }

        return new ScaffoldVersionIdentity(
            trimmed,
            normalized,
            "v" + normalized,
            TrySourceCommit(build),
            build,
            source,
            releaseAssetName,
            releaseAssetSha256,
            executablePath,
            applicationDirectory);
    }

    public static void WriteStamp(string repoRoot, ScaffoldVersionIdentity identity)
    {
        string path = FullStampPath(repoRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        JsonSerializerOptions options = JsonContractSerializerOptions.Create();
        options.WriteIndented = true;
        File.WriteAllText(path, JsonSerializer.Serialize(
            new ScaffoldVersionStamp(JsonContractDefaults.SchemaVersion, identity), options));
    }

    public static ScaffoldVersionReport Report(
        string runningVersion,
        string? repoRoot,
        PrerequisiteCheckReport? prerequisites = null)
    {
        string? executablePath = Environment.ProcessPath;
        string? applicationDirectory = executablePath is null ? AppContext.BaseDirectory : Path.GetDirectoryName(executablePath);
        ScaffoldVersionIdentity running = IdentityFromVersion(
            runningVersion,
            "hx-scaffold",
            executablePath: executablePath,
            applicationDirectory: applicationDirectory);
        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            return new ScaffoldVersionReport(
                JsonContractDefaults.SchemaVersion,
                running,
                null,
                ScaffoldVersionRelation.Unknown,
                null,
                [],
                prerequisites);
        }

        string root = Path.GetFullPath(repoRoot);
        var diagnostics = new List<string>();
        ScaffoldVersionIdentity? target = ReadStamp(root, diagnostics);
        ManagedAssetModificationSummary? managed = ReadManagedAssets(root, diagnostics);
        string relation = target is null
            ? ScaffoldVersionRelation.Unknown
            : Compare(running.NormalizedVersion, target.NormalizedVersion);

        return new ScaffoldVersionReport(
            JsonContractDefaults.SchemaVersion,
            running,
            target,
            relation,
            managed,
            diagnostics,
            prerequisites);
    }

    private static ScaffoldVersionIdentity? ReadStamp(string repoRoot, List<string> diagnostics)
    {
        string path = FullStampPath(repoRoot);
        if (!File.Exists(path))
        {
            diagnostics.Add($"version stamp missing: {StampRelativePath}");
            return null;
        }

        try
        {
            ScaffoldVersionStamp? stamp = JsonSerializer.Deserialize<ScaffoldVersionStamp>(
                File.ReadAllText(path), JsonContractSerializerOptions.Create());
            if (stamp?.Installed is null)
            {
                diagnostics.Add($"version stamp is empty: {StampRelativePath}");
                return null;
            }

            return stamp.Installed;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            diagnostics.Add($"version stamp is invalid: {ex.Message}");
            return null;
        }
    }

    private static ManagedAssetModificationSummary? ReadManagedAssets(string repoRoot, List<string> diagnostics)
    {
        try
        {
            ManagedAssetScanResult scan = ManagedAssetScanner.Scan(repoRoot);
            string state = scan.Outcome == StageOutcome.Pass.ToString().ToLowerInvariant() ? "clean" : "modified";
            return new ManagedAssetModificationSummary(
                state,
                scan.ModifiedWorkflowTemplates,
                scan.ModifiedSkillGeneratedInstructions,
                scan.Missing);
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            diagnostics.Add("managed asset metadata unavailable: " + ex.Message);
            return null;
        }
    }

    private static string FullStampPath(string repoRoot) =>
        Path.GetFullPath(Path.Combine(repoRoot, StampRelativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string NormalizeSemVer(string version)
    {
        string value = version.TrimStart('v', 'V');
        int plus = value.IndexOf('+');
        if (plus >= 0)
        {
            value = value[..plus];
        }

        return value;
    }

    private static string Compare(string running, string target)
    {
        int compare = GitVersionTool.CompareVersions(running, target);
        return compare switch
        {
            < 0 => ScaffoldVersionRelation.Behind,
            0 => ScaffoldVersionRelation.Equal,
            _ => ScaffoldVersionRelation.Newer,
        };
    }

    private static string? TrySourceCommit(string? buildMetadata)
    {
        if (string.IsNullOrWhiteSpace(buildMetadata))
        {
            return null;
        }

        string last = buildMetadata.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
        return last.Length >= 7 && last.All(Uri.IsHexDigit) ? last : null;
    }
}
