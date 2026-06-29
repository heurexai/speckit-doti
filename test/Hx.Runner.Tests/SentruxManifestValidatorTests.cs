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
    public void RepositoryManifestPinsHeurexForkV0512ForDeclaredRids()
    {
        string manifestPath = Path.Combine(RepositoryRoot(), SentruxManifestValidator.ManifestRelativePath);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement root = document.RootElement;

        Assert.Equal("https://github.com/heurexai/sentrux", root.GetProperty("sourceRemote").GetString());
        Assert.Equal("v0.5.12", root.GetProperty("releaseTag").GetString());

        Dictionary<string, string> expected = new(StringComparer.OrdinalIgnoreCase)
        {
            ["win-x64"] = "63be6c50f5efdbebc4b520f60435887becd54be86562c63f05d87377ccd5b626",
            ["linux-x64"] = "d7d09faaf6220fbb91057182b3709ffda01549461cb3c9a9c64afaeece890ab2",
            ["osx-arm64"] = "8e53e6862543030cca24c9a7922a55cd9f039200d48ad1c387980257776918e4",
        };

        foreach (JsonElement asset in root.GetProperty("assets").EnumerateArray())
        {
            string rid = asset.GetProperty("rid").GetString()!;
            Assert.True(expected.TryGetValue(rid, out string? sha), "unexpected or duplicate Sentrux RID: " + rid);
            Assert.Equal(sha, asset.GetProperty("executableSha256").GetString());
            Assert.Contains("/v0.5.12/", asset.GetProperty("downloadUrl").GetString(), StringComparison.Ordinal);
            expected.Remove(rid);
        }

        Assert.Empty(expected);

        // Bug#1 fix: the fork v0.5.12 added `--include-untracked` to the regression `gate`; the manifest pins the
        // capability so a downstream environment cannot silently run an older fork that drops untracked-file growth.
        string[] requiredFeatures = root.GetProperty("requiredFeatures").EnumerateArray().Select(f => f.GetString()!).ToArray();
        Assert.Contains("gate-include-untracked", requiredFeatures);
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
