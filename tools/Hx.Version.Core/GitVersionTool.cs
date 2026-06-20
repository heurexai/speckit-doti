using System.Text.Json;
using Hx.Runner.Core.Io;
using Hx.Runner.Core.Platform;
using Hx.Runner.Core.Process;
using Hx.Runner.Core.Repository;
using Hx.Tooling.Contracts;

namespace Hx.Version.Core;

/// <summary>
/// Wraps the vendored GitVersion CLI: verifies the pinned binary (manifest + MIT + RID asset + SHA-256,
/// the Gitleaks/Sentrux pattern), computes the version via <c>gitversion /output json</c>, and records an
/// operator-instructed major/minor bump as an annotated git tag. Fails closed when the binary is not
/// vendored/verified for the host RID — never guesses a version.
/// </summary>
public static class GitVersionTool
{
    public const string ManifestRelativePath = "tools/gitversion/gitversion.version.json";
    public const string Tool = "gitversion";

    public static ToolVerificationResult Verify(string repositoryRoot, string hostRuntimeIdentifier)
    {
        List<string> checks = [];
        List<string> problems = [];

        RepositoryPath manifestPath = RepositoryPathResolver.ResolveInside(repositoryRoot, ManifestRelativePath);
        if (!File.Exists(manifestPath.FullPath))
        {
            problems.Add($"GitVersion manifest is missing: {ManifestRelativePath}");
            return Result(false, StageOutcome.Blocked, checks, problems,
                "GitVersion is not vendored yet. Vendor a pinned release before enabling version calculation.");
        }

        checks.Add("manifest present");

        GitVersionManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<GitVersionManifest>(
                File.ReadAllText(manifestPath.FullPath), JsonContractSerializerOptions.Create());
        }
        catch (JsonException ex)
        {
            problems.Add($"GitVersion manifest is not valid JSON: {ex.Message}");
            return Result(false, StageOutcome.Fail, checks, problems);
        }

        if (manifest is null)
        {
            problems.Add("GitVersion manifest is empty.");
            return Result(false, StageOutcome.Fail, checks, problems);
        }

        if (!string.Equals(manifest.License, "MIT", StringComparison.Ordinal))
        {
            problems.Add($"GitVersion manifest license must be MIT, found '{manifest.License}'.");
        }
        else
        {
            checks.Add("license is MIT");
        }

        GitVersionAsset? asset = manifest.Assets.FirstOrDefault(a =>
            string.Equals(a.Rid, hostRuntimeIdentifier, StringComparison.OrdinalIgnoreCase));
        if (asset is null)
        {
            problems.Add($"No GitVersion asset mapped for host RID '{hostRuntimeIdentifier}'.");
            return Result(false, StageOutcome.Blocked, checks, problems);
        }

        checks.Add($"asset mapped for {hostRuntimeIdentifier} ({asset.SupportLevel})");

        RepositoryPath exePath = RepositoryPathResolver.ResolveInside(repositoryRoot, asset.ExecutablePath);
        if (!File.Exists(exePath.FullPath))
        {
            problems.Add($"GitVersion executable is missing for {hostRuntimeIdentifier}: {asset.ExecutablePath}");
            return Result(false, StageOutcome.Blocked, checks, problems,
                "Vendor the GitVersion executable for this RID (operational step), or version calculation stays advisory.");
        }

        if (!string.IsNullOrWhiteSpace(asset.ExecutableSha256))
        {
            string actual = FileHashing.Sha256OfFile(exePath.FullPath);
            if (!string.Equals(actual, asset.ExecutableSha256, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add("GitVersion executable hash does not match the manifest.");
            }
            else
            {
                checks.Add("executable hash matches manifest");
            }
        }

        bool verified = problems.Count == 0;
        return Result(verified, verified ? StageOutcome.Pass : StageOutcome.Fail, checks, problems);
    }

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

    /// <summary>Record an operator-instructed major/minor bump as an annotated git tag (the sole bump surface).</summary>
    public static VersionResult Bump(string repositoryRoot, string increment)
    {
        VersionResult current = Calculate(repositoryRoot);
        string next = NextVersion(current.Version, increment);
        ProcessRunResult tag = ProcessRunner.Run(new ToolCommand(
            "git", ["tag", "-a", "v" + next, "-m", $"Release v{next} ({increment} bump via `version bump`)"], repositoryRoot));
        if (tag.ExitCode != 0)
        {
            throw new InvalidOperationException("git tag failed: " +
                (string.IsNullOrWhiteSpace(tag.StandardError) ? tag.StandardOutput : tag.StandardError));
        }

        return new VersionResult(next, increment, $"gitversion {ManifestVersion(repositoryRoot)} + tag v{next}");
    }

    /// <summary>Pure next-version computation (testable).</summary>
    public static string NextVersion(string current, string increment)
    {
        string core = current.TrimStart('v', 'V');
        int delimiter = core.IndexOfAny(['+', '-']);
        if (delimiter >= 0)
        {
            core = core[..delimiter];
        }

        string[] parts = core.Split('.');
        int major = parts.Length > 0 && int.TryParse(parts[0], out int m) ? m : 0;
        int minor = parts.Length > 1 && int.TryParse(parts[1], out int n) ? n : 0;
        int patch = parts.Length > 2 && int.TryParse(parts[2], out int p) ? p : 0;

        return increment.ToLowerInvariant() switch
        {
            "major" => $"{major + 1}.0.0",
            "minor" => $"{major}.{minor + 1}.0",
            "patch" => $"{major}.{minor}.{patch + 1}",
            _ => throw new ArgumentException($"Unknown increment '{increment}'. Use major|minor|patch.")
        };
    }

    private static VersionResult ParseVersion(string json, string toolVersion)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        string majorMinorPatch = ReadString(root, "MajorMinorPatch") ?? "0.0.0";
        string semVer = ReadString(root, "SemVer") ?? majorMinorPatch;
        return new VersionResult(semVer, "patch", $"gitversion {toolVersion}");
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
        GitVersionAsset? asset = LoadManifest(repositoryRoot).Assets
            .FirstOrDefault(a => string.Equals(a.Rid, rid, StringComparison.OrdinalIgnoreCase));
        if (asset is null)
        {
            return null;
        }

        return RepositoryPathResolver.ResolveInside(repositoryRoot, asset.ExecutablePath).FullPath;
    }

    private static ToolVerificationResult Result(
        bool verified, StageOutcome outcome, List<string> checks, List<string> problems, string? message = null) =>
        new(JsonContractDefaults.SchemaVersion, Tool, verified, outcome, checks, problems, message);
}
