using System.Text.Json;
using Hx.Runner.Core.Platform;
using Hx.Runner.Core.Process;
using Hx.Runner.Core.Repository;
using Hx.Runner.Core.Tools;
using Hx.Tooling.Contracts;

namespace Hx.Version.Core;

public static partial class GitVersionTool
{
    /// <summary>Compute the version via the vendored GitVersion CLI. Throws (fail closed) when unverified.</summary>
    public static VersionResult Calculate(string repositoryRoot)
    {
        string rid = HostPlatformDetector.DetectCurrent().RuntimeIdentifier;
        ToolVerificationResult verify = Verify(repositoryRoot, rid);
        if (!verify.Verified)
        {
            throw new InvalidOperationException("GitVersion not verified (fail closed): " + string.Join("; ", verify.Problems));
        }

        string exe = ResolveExecutable(repositoryRoot, rid)!;
        ProcessRunResult run = ProcessRunner.Run(new ToolCommand(exe, ["/output", "json"], repositoryRoot));
        if (run.ExitCode != 0)
        {
            throw new InvalidOperationException("gitversion failed: " +
                (string.IsNullOrWhiteSpace(run.StandardError) ? run.StandardOutput : run.StandardError));
        }

        return ParseVersion(run.StandardOutput, ManifestVersion(repositoryRoot));
    }

    public static int CompareVersions(string left, string right)
    {
        SemVer l = SemVer.Parse(left);
        SemVer r = SemVer.Parse(right);
        int core = CompareVersionCore(l, r);
        return core != 0 ? core : ComparePrerelease(l.Prerelease, r.Prerelease);
    }

    private static int CompareVersionCore(SemVer left, SemVer right)
    {
        if (left.Major != right.Major)
        {
            return left.Major.CompareTo(right.Major);
        }

        if (left.Minor != right.Minor)
        {
            return left.Minor.CompareTo(right.Minor);
        }

        return left.Patch.CompareTo(right.Patch);
    }

    private static int ComparePrerelease(string? left, string? right)
    {
        if (left is null && right is null)
        {
            return 0;
        }

        if (left is null)
        {
            return 1;
        }

        return right is null ? -1 : string.CompareOrdinal(left, right);
    }

    private static VersionResult ParseVersion(string json, string toolVersion)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        string majorMinorPatch = ReadString(root, "MajorMinorPatch") ?? "0.0.0";
        return new VersionResult(majorMinorPatch, "patch", $"gitversion {toolVersion}");
    }

    private static string? ReadString(JsonElement root, string name)
    {
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }

    private static GitVersionManifest LoadManifest(string repositoryRoot)
    {
        RepositoryPath manifestPath = RepositoryPathResolver.ResolveInside(repositoryRoot, ManifestRelativePath);
        return JsonSerializer.Deserialize<GitVersionManifest>(
            File.ReadAllText(manifestPath.FullPath), JsonContractSerializerOptions.Create())
            ?? throw new InvalidOperationException("GitVersion manifest is empty.");
    }

    private static string ManifestVersion(string repositoryRoot)
    {
        try { return LoadManifest(repositoryRoot).Version; }
        catch { return "unknown"; }
    }

    private static string? ResolveExecutable(string repositoryRoot, string rid)
    {
        GitVersionManifest manifest = LoadManifest(repositoryRoot);
        GitVersionAsset? asset = manifest.Assets
            .FirstOrDefault(a => string.Equals(a.Rid, rid, StringComparison.OrdinalIgnoreCase));
        if (asset is null)
        {
            return null;
        }

        string inRepo = RepositoryPathResolver.ResolveInside(repositoryRoot, asset.ExecutablePath).FullPath;
        return ToolStoreResolver.ResolveOrFallback(Tool, manifest.Version, rid, asset.ExecutableName, asset.ExecutableSha256 ?? string.Empty, inRepo);
    }
}
