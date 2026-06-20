using System.Runtime.InteropServices;
using Hx.Runner.Core.Platform;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class HostPlatformDetectorTests
{
    [Theory]
    [InlineData(HostOperatingSystem.Windows, Architecture.X64, "win-x64", PlatformSupportLevel.Active)]
    [InlineData(HostOperatingSystem.Linux, Architecture.X64, "linux-x64", PlatformSupportLevel.Active)]
    [InlineData(HostOperatingSystem.Linux, Architecture.Arm64, "linux-arm64", PlatformSupportLevel.Active)]
    [InlineData(HostOperatingSystem.MacOS, Architecture.Arm64, "osx-arm64", PlatformSupportLevel.Advisory)]
    public void ResolvesRuntimeIdentifierAndSupport(
        HostOperatingSystem operatingSystem,
        Architecture architecture,
        string expectedRid,
        PlatformSupportLevel expectedSupport)
    {
        Assert.Equal(expectedRid, HostPlatformDetector.ResolveRuntimeIdentifier(operatingSystem, architecture));
        Assert.Equal(expectedSupport, HostPlatformDetector.ResolveSupportLevel(operatingSystem, architecture));
    }

    [Fact]
    public void CurrentProbeKeepsWindowsAndLinuxAsActiveTargets()
    {
        var probe = CrossPlatformProbe.Create();

        Assert.Contains("win-x64", probe.ActiveTargetRids);
        Assert.Contains("linux-x64", probe.ActiveTargetRids);
        Assert.Contains("linux-arm64", probe.ActiveTargetRids);
        Assert.Contains("osx-arm64", probe.AdvisoryTargetRids);
        Assert.NotEmpty(probe.Warnings);
    }
}
