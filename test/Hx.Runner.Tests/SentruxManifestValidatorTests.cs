using System.Text.Json;
using Hx.Sentrux.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class SentruxManifestValidatorTests
{
    [Fact]
    public void MissingManifestFailsClosed()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-sx-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);

        try
        {
            ToolVerificationResult result = SentruxManifestValidator.Verify(dir, "win-x64", SentruxPolicy.Default());

            Assert.False(result.Verified);
            Assert.Equal(StageOutcome.Blocked, result.Outcome);
            Assert.Contains(result.Problems, p => p.Contains("manifest is missing", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void RepositoryManifestPinsHeurexForkV0511ForDeclaredRids()
    {
        string manifestPath = Path.Combine(RepositoryRoot(), SentruxManifestValidator.ManifestRelativePath);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement root = document.RootElement;

        Assert.Equal("https://github.com/heurexai/sentrux", root.GetProperty("sourceRemote").GetString());
        Assert.Equal("v0.5.11", root.GetProperty("releaseTag").GetString());

        Dictionary<string, string> expected = new(StringComparer.OrdinalIgnoreCase)
        {
            ["win-x64"] = "ed387706d10fc2708507939d5390016b256cc4a725afcf9e209021cbab2bf88c",
            ["linux-x64"] = "6da9bede77654a54425c101d37d1bbb192a51c8cf7bd2823ad9c0da45ee34fae",
            ["osx-arm64"] = "0389d2b84075bf02a5f53707f589e5a999b65b7b31acc47e622417d23745dad2",
        };

        foreach (JsonElement asset in root.GetProperty("assets").EnumerateArray())
        {
            string rid = asset.GetProperty("rid").GetString()!;
            Assert.True(expected.TryGetValue(rid, out string? sha), "unexpected or duplicate Sentrux RID: " + rid);
            Assert.Equal(sha, asset.GetProperty("executableSha256").GetString());
            Assert.Contains("/v0.5.11/", asset.GetProperty("downloadUrl").GetString(), StringComparison.Ordinal);
            expected.Remove(rid);
        }

        Assert.Empty(expected);
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, SentruxManifestValidator.ManifestRelativePath)))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
