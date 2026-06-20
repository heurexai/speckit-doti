namespace Hx.Runner.Core.Platform;

public sealed record CrossPlatformProbeResult(
    HostPlatformInfo Host,
    IReadOnlyList<string> ActiveTargetRids,
    IReadOnlyList<string> AdvisoryTargetRids,
    IReadOnlyList<string> Warnings);
