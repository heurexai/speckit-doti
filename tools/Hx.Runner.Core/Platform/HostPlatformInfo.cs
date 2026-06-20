using System.Runtime.InteropServices;

namespace Hx.Runner.Core.Platform;

public sealed record HostPlatformInfo(
    HostOperatingSystem OperatingSystem,
    Architecture Architecture,
    string RuntimeIdentifier,
    PlatformSupportLevel SupportLevel,
    IReadOnlyList<string> Warnings);
