using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>
/// 007 T022 / FR-033 trust hardening (version-drift gate): the hash-capture workflow
/// (<c>.github/workflows/compute-tool-hashes.yml</c>) MUST fetch the EXACT version each tool manifest pins. A drift —
/// capturing a different version than the manifest claims — means the recorded SHA-256s are for the wrong binary.
/// The real defect this catches: the workflow once fetched Sentrux <c>v0.5.10</c> while the manifest pinned
/// <c>v0.5.11</c>. This validator runs in the test suite the gate executes, so the drift fails the gate.
/// </summary>
public sealed class ToolHashCaptureDriftTests
{
    [Theory]
    [InlineData("tools/gitleaks/gitleaks.version.json", "github.com/gitleaks/gitleaks/releases/download/")]
    [InlineData("tools/gitversion/gitversion.version.json", "github.com/GitTools/GitVersion/releases/download/")]
    [InlineData("tools/sentrux/sentrux.version.json", "github.com/heurexai/sentrux/releases/download/")]
    public void Hash_capture_workflow_version_matches_the_manifest(string manifestRelative, string downloadPrefix)
    {
        string repo = RepoRoot();
        string pinned = Normalize(ManifestVersion(Path.Combine(repo, manifestRelative)));
        string workflow = File.ReadAllText(Path.Combine(repo, ".github", "workflows", "compute-tool-hashes.yml"));

        List<string> captured = Regex.Matches(workflow, Regex.Escape(downloadPrefix) + "(?<v>[^/]+)/")
            .Select(m => Normalize(m.Groups["v"].Value))
            .Distinct()
            .ToList();

        Assert.NotEmpty(captured); // the workflow captures this tool at all
        Assert.All(captured, version => Assert.Equal(pinned, version));
    }

    [Theory]
    [InlineData("tools/gitleaks/gitleaks.version.json")]
    [InlineData("tools/gitversion/gitversion.version.json")]
    [InlineData("tools/sentrux/sentrux.version.json")]
    public void Manifest_records_hash_provenance_matching_its_pinned_version(string manifestRelative)
    {
        string repo = RepoRoot();
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, manifestRelative)));
        JsonElement root = doc.RootElement;

        Assert.True(root.TryGetProperty("hashProvenance", out JsonElement provenance),
            $"{manifestRelative} must record hashProvenance (007 T022).");
        string captured = Normalize(provenance.GetProperty("capturedToolVersion").GetString()!);
        Assert.Equal(Normalize(ManifestVersion(Path.Combine(repo, manifestRelative))), captured);
        Assert.False(string.IsNullOrWhiteSpace(provenance.GetProperty("capturedBy").GetString()));
    }

    private static string ManifestVersion(string manifestPath)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement root = doc.RootElement;
        if (root.TryGetProperty("version", out JsonElement version) && version.ValueKind == JsonValueKind.String)
        {
            return version.GetString()!;
        }

        if (root.TryGetProperty("releaseTag", out JsonElement tag) && tag.ValueKind == JsonValueKind.String)
        {
            return tag.GetString()!;
        }

        throw new InvalidOperationException($"Manifest {manifestPath} has neither a 'version' nor a 'releaseTag'.");
    }

    private static string Normalize(string version) => version.TrimStart('v', 'V');

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "scaffold-dotnet.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("scaffold-dotnet.slnx not found above the test output.");
    }
}
