namespace Hx.Runner.Core.Platform;

public static class CrossPlatformProbe
{
    public static CrossPlatformProbeResult Create()
    {
        HostPlatformInfo host = HostPlatformDetector.DetectCurrent();

        string[] activeTargetRids = ["win-x64", "linux-x64", "linux-arm64"];
        string[] advisoryTargetRids = ["osx-x64", "osx-arm64"];

        List<string> warnings = [.. host.Warnings];
        warnings.Add("Cross-RID publish verification is warning-only until CI provides Windows and Linux coverage.");

        return new CrossPlatformProbeResult(
            host,
            activeTargetRids,
            advisoryTargetRids,
            warnings);
    }
}
