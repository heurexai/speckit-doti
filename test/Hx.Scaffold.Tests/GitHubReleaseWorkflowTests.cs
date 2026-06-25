using Xunit;

namespace Hx.Scaffold.Tests;

public sealed class GitHubReleaseWorkflowTests
{
    [Fact]
    public void Release_workflow_publishes_velopack_artifacts_not_source_archives()
    {
        string workflow = File.ReadAllText(Path.Combine(FindRepoRoot(), ".github", "workflows", "release.yml"));

        Assert.Contains("vpk-payload", workflow);
        Assert.Contains("vpk.dll", workflow);
        Assert.Contains("pack `", workflow);
        Assert.Contains("releaseProduct = \"velopack\"", workflow);
        Assert.Contains("sourceArchiveExcluded = $true", workflow);
        Assert.Contains("release-artifacts", workflow);
        Assert.Contains("gh release upload", workflow);
        Assert.DoesNotContain("git archive", workflow);
        Assert.DoesNotContain("Compress-Archive", workflow);
        Assert.DoesNotContain("tar czf", workflow);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "scaffold-dotnet.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("scaffold-dotnet.slnx not found above the test output.");
    }
}
