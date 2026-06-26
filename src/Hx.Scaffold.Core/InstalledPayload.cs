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
}
