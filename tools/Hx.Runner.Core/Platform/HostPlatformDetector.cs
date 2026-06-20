using System.Runtime.InteropServices;

namespace Hx.Runner.Core.Platform;

public static class HostPlatformDetector
{
    public static HostPlatformInfo DetectCurrent()
    {
        HostOperatingSystem operatingSystem = DetectOperatingSystem();
        Architecture architecture = RuntimeInformation.OSArchitecture;
        string runtimeIdentifier = ResolveRuntimeIdentifier(operatingSystem, architecture);
        PlatformSupportLevel supportLevel = ResolveSupportLevel(operatingSystem, architecture);
        IReadOnlyList<string> warnings = ResolveWarnings(operatingSystem, architecture, supportLevel);

        return new HostPlatformInfo(operatingSystem, architecture, runtimeIdentifier, supportLevel, warnings);
    }

    public static string ResolveRuntimeIdentifier(HostOperatingSystem operatingSystem, Architecture architecture)
    {
        string architecturePart = architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => "unknown"
        };

        string osPart = operatingSystem switch
        {
            HostOperatingSystem.Windows => "win",
            HostOperatingSystem.Linux => "linux",
            HostOperatingSystem.MacOS => "osx",
            _ => "unknown"
        };

        return $"{osPart}-{architecturePart}";
    }

    public static PlatformSupportLevel ResolveSupportLevel(HostOperatingSystem operatingSystem, Architecture architecture)
    {
        if (architecture is not (Architecture.X64 or Architecture.Arm64))
        {
            return PlatformSupportLevel.Unsupported;
        }

        return operatingSystem switch
        {
            HostOperatingSystem.Windows or HostOperatingSystem.Linux => PlatformSupportLevel.Active,
            HostOperatingSystem.MacOS => PlatformSupportLevel.Advisory,
            _ => PlatformSupportLevel.Unsupported
        };
    }

    private static HostOperatingSystem DetectOperatingSystem()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return HostOperatingSystem.Windows;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return HostOperatingSystem.Linux;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return HostOperatingSystem.MacOS;
        }

        return HostOperatingSystem.Unknown;
    }

    private static IReadOnlyList<string> ResolveWarnings(
        HostOperatingSystem operatingSystem,
        Architecture architecture,
        PlatformSupportLevel supportLevel)
    {
        List<string> warnings = [];

        if (supportLevel == PlatformSupportLevel.Advisory)
        {
            warnings.Add("macOS is warning-only; active hardening currently focuses on Windows and Linux.");
        }

        if (supportLevel == PlatformSupportLevel.Unsupported)
        {
            warnings.Add($"Unsupported host combination: {operatingSystem}/{architecture}.");
        }

        return warnings;
    }
}
