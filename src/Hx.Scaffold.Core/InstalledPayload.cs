using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Core;

/// <summary>
/// Resolves the asset root for installed <c>hx</c> operational commands (templates, prerequisite policy,
/// <c>.doti</c>, vendored tools). INSTALLED mode (FR-001/002/003): the source-free payload beside the executable
/// (<see cref="PayloadRoot"/> — a non-source <c>payload.manifest.json</c>, integrity-verified). SOURCE/DEVELOPER
/// fallback (FR-004; self-host/dev only, when NO released payload is present beside the executable): the scaffold
/// source root (<see cref="ScaffoldRoot"/>). A released <c>hx</c> resolves via <see cref="PayloadRoot"/> and
/// never reaches the source fallback at runtime; a present-but-invalid payload descriptor is a fail-closed
/// integrity error, not a silent source fallback.
/// </summary>
public static class InstalledPayload
{
    /// <summary>The installed-payload resolution (carries the verified descriptor, channel + command-mode, and
    /// the active-override flag), or a fail-closed result. Surfaced by version/describe (T012).</summary>
    public static PayloadResolution Resolve() => PayloadRoot.Resolve();

    /// <summary>
    /// The asset-root path. Prefers the installed payload (source-free); throws a structured
    /// <see cref="InvalidOperationException"/> on a present-but-invalid descriptor (fail closed); falls back to
    /// the scaffold source root only when no payload manifest exists beside the executable (dev/self-host).
    /// </summary>
    public static string ResolveAssetRoot(string currentDirectory)
    {
        PayloadResolution resolution = PayloadRoot.Resolve();
        if (resolution.Ok)
        {
            return resolution.Root!;
        }

        if (resolution.FailureKind == PayloadFailureKind.RootMissing
            && ScaffoldRoot.TryFind(currentDirectory) is { } sourceRoot)
        {
            return sourceRoot;
        }

        throw new InvalidOperationException(
            resolution.FailureReason ?? "could not resolve the installed payload root");
    }

    /// <summary>The active distribution channel + command-mode + update mechanism of the running <c>hx</c>
    /// (FR-013/FR-021/FR-022): read from the installed payload descriptor, or the source/developer build when no
    /// payload is present beside the executable. Read-only and source-free — never requires a source checkout.</summary>
    public static DistributionChannelInfo ResolveChannel() => ResolveChannel(PayloadRoot.Resolve());

    /// <summary>Map a payload resolution to its channel info (explicit input for testability).</summary>
    public static DistributionChannelInfo ResolveChannel(PayloadResolution resolution) =>
        resolution.Ok
            ? ChannelInfoFor(resolution.Descriptor!.Channel, resolution.Descriptor.Mode)
            : new DistributionChannelInfo(
                DistributionChannelId.Source, CommandMode.SourceDeveloper,
                InstallCommand: null, UpdateCommand: null, UpdateAuthority: "source checkout (self-host/dev build)");

    private static DistributionChannelInfo ChannelInfoFor(string channel, string mode) => channel switch
    {
        DistributionChannelId.GlobalTool => new(channel, mode,
            "dotnet tool install -g Heurex.SpeckitDoti",
            "dotnet tool update -g Heurex.SpeckitDoti",
            "dotnet tool"),
        DistributionChannelId.Msix => new(channel, mode,
            InstallCommand: null,
            UpdateCommand: "the Microsoft Store updates the MSIX automatically",
            UpdateAuthority: "Microsoft Store"),
        _ => new(channel, mode, null, null, null),
    };
}
