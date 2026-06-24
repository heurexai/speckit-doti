using Hx.Runner.Core.Platform;
using Hx.Tooling.Contracts;
using Hx.Version.Core;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class VersionTests
{
    [Fact]
    public void Verify_is_blocked_when_the_manifest_is_missing()
    {
        string temp = Directory.CreateTempSubdirectory("hx-ver-").FullName;
        try
        {
            ToolVerificationResult result = GitVersionTool.Verify(temp, "win-x64");
            Assert.False(result.Verified);
            Assert.Equal(StageOutcome.Blocked, result.Outcome);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public void Calculate_computes_a_version_when_gitversion_is_vendored()
    {
        string repoRoot = FindRepoRoot();
        string rid = HostPlatformDetector.DetectCurrent().RuntimeIdentifier;

        // The 76 MB GitVersion binary is gitignored (operational vendor step), so it is absent on a fresh
        // clone / CI — skip rather than fail there; the local run validates the real computation.
        Assert.SkipUnless(GitVersionTool.Verify(repoRoot, rid).Verified, "GitVersion binary not vendored for this RID.");

        VersionResult result = GitVersionTool.Calculate(repoRoot);
        Assert.False(string.IsNullOrWhiteSpace(result.Version));
        Assert.Contains("gitversion", result.Source, StringComparison.OrdinalIgnoreCase);
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
