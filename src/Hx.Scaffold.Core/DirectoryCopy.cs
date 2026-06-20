namespace Hx.Scaffold.Core;

/// <summary>Recursive directory copy used by the vendorers. Includes dotfiles; a directory filter
/// lets callers skip build artifacts (bin/obj). Overwrites existing files (idempotent re-vendor).</summary>
internal static class DirectoryCopy
{
    public static void Copy(string sourceDir, string targetDir, Func<string, bool> includeDirectory)
    {
        Directory.CreateDirectory(targetDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), overwrite: true);
        }

        foreach (string sub in Directory.GetDirectories(sourceDir))
        {
            if (!includeDirectory(sub))
            {
                continue;
            }

            Copy(sub, Path.Combine(targetDir, Path.GetFileName(sub)), includeDirectory);
        }
    }

    /// <summary>A directory filter that skips MSBuild output folders (<c>bin</c>/<c>obj</c>).</summary>
    public static bool ExcludeBuildArtifacts(string directory) =>
        Path.GetFileName(directory) is not ("bin" or "obj");
}
