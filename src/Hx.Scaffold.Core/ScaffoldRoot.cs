namespace Hx.Scaffold.Core;

/// <summary>Locates the scaffold repo / payload root (the directory containing <c>scaffold-dotnet.slnx</c>),
/// which is the SOURCE of the template pack, vendored tools, and Doti assets.</summary>
public static class ScaffoldRoot
{
    public const string Marker = "scaffold-dotnet.slnx";
    public const string PayloadOverrideEnvVar = "HX_PAYLOAD_ROOT";

    /// <summary>Walk up from <paramref name="startDirectory"/> for the marker; throws if not found.</summary>
    public static string Find(string startDirectory) =>
        TryFind(startDirectory) ?? throw new InvalidOperationException(
            $"Could not locate the scaffold root ('{Marker}') from '{startDirectory}'. Run from inside the scaffold repo.");

    /// <summary>
    /// Resolve the scaffold SOURCE/payload root. Order: the <c>HX_PAYLOAD_ROOT</c> override; then walking up
    /// from <paramref name="currentDirectory"/> (running inside a scaffold checkout — dev/self-host); then
    /// walking up from the running executable's directory (the standalone installer ships its payload
    /// alongside the executable, so <c>hx new</c> works from any directory). Throws if none contains the marker.
    /// </summary>
    public static string Resolve(string currentDirectory)
    {
        string? overridePath = Environment.GetEnvironmentVariable(PayloadOverrideEnvVar);
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(Path.Combine(Path.GetFullPath(overridePath), Marker)))
        {
            return Path.GetFullPath(overridePath);
        }

        return TryFind(currentDirectory)
            ?? TryFind(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException(
                $"Could not locate the scaffold payload ('{Marker}') from the current directory, the installer location, or {PayloadOverrideEnvVar}.");
    }

    private static string? TryFind(string startDirectory)
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
