using Hx.Scaffold.Core.Release;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Scaffold.Tests;

/// <summary>
/// 007 T028: LocalReleaseService is retargeted off vpk to the framework-dependent global-tool package + source-free
/// install smoke, recording the channel-neutral release identity (the Windows MSIX is a CI/Store-only channel,
/// recorded advisory). These cover the channel-decision logic; the heavy pack + install smoke itself is exercised by
/// the global-tool channel build during <c>hx release</c> and by the install smoke (T027) / release CI workflow.
/// </summary>
public sealed class LocalReleaseChannelTests
{
    [Theory]
    [InlineData("Heurex.SpeckitDoti.0.9.1.nupkg", "Heurex.SpeckitDoti", "0.9.1")]
    [InlineData("Heurex.SpeckitDoti.0.9.1-29.nupkg", "Heurex.SpeckitDoti", "0.9.1-29")]
    [InlineData("Heurex.SpeckitDoti.1.2.3-rc.4+build.5.nupkg", "Heurex.SpeckitDoti", "1.2.3-rc.4+build.5")]
    [InlineData("My.Tool.10.0.0.nupkg", "My.Tool", "10.0.0")]
    public void ParseNupkgIdentity_splits_packageId_from_version(string file, string expectedId, string expectedVersion)
    {
        (string PackageId, string Version) parsed = LocalReleaseService.ParseNupkgIdentity(file);

        Assert.Equal(expectedId, parsed.PackageId);
        Assert.Equal(expectedVersion, parsed.Version);
    }

    [Theory]
    [InlineData(true, true, "global-tool+msix")]
    [InlineData(true, false, "global-tool")]
    [InlineData(false, true, "msix")]
    [InlineData(false, false, "none")]
    public void ComposeReleaseProduct_names_the_channels_produced(bool globalTool, bool msix, string expected) =>
        Assert.Equal(expected, LocalReleaseService.ComposeReleaseProduct(globalTool, msix));

    [Fact]
    public void MsixChannelProof_is_advisory_when_the_repo_ships_a_manifest()
    {
        // This repo ships packaging/msix/AppxManifest.xml: the MSIX channel applies but is CI/Store-only -> advisory.
        ChannelInstallProof? proof = LocalReleaseService.MsixChannelProof(FindRepoRoot());

        Assert.NotNull(proof);
        Assert.Equal(DistributionChannelId.Msix, proof!.Channel);
        Assert.Equal("advisory", proof.Outcome);
        Assert.NotEmpty(proof.Blockers);
    }

    [Fact]
    public void MsixChannelProof_is_null_when_the_repo_ships_no_manifest() =>
        Assert.Null(LocalReleaseService.MsixChannelProof(Path.GetTempPath()));

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
