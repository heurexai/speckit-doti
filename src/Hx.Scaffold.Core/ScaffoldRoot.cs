namespace Hx.Scaffold.Core;

/// <summary>
/// SOURCE/DEVELOPER-ONLY (FR-004): locates the scaffold SOURCE root (the directory containing
/// <c>scaffold-dotnet.slnx</c>) for self-host/dev commands. Installed <c>hx</c> commands MUST NOT use this —
/// they resolve their payload source-free via <see cref="PayloadRoot"/> (a non-source
/// <c>payload.manifest.json</c> beside the executable). T011 rewires the installed <c>new</c>/<c>version</c>/
/// <c>prereq</c> commands off <see cref="Resolve"/> onto <see cref="PayloadRoot"/> and removes the
/// executable-directory fallback so only <see cref="PayloadRoot"/> is executable-anchored.
/// </summary>
public static class ScaffoldRoot
{
    public const string Marker = "scaffold-dotnet.slnx";

    /// <summary>Walk up from <paramref name="startDirectory"/> for the marker; throws if not found. The pure
    /// source walk — the installer-location/override fallback is gone, so only <see cref="PayloadRoot"/> is
    /// executable-anchored (T011).</summary>
    public static string Find(string startDirectory) =>
        TryFind(startDirectory) ?? throw new InvalidOperationException(
            $"Could not locate the scaffold root ('{Marker}') from '{startDirectory}'. Run from inside the scaffold repo.");

    /// <summary>Walk up from <paramref name="startDirectory"/> for the source marker; null if not found.</summary>
    public static string? TryFind(string startDirectory)
    {
        DirectoryInfo? dir = new(Path.GetFullPath(startDirectory));
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, Marker)))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
