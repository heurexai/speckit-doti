namespace Hx.Doti.Core.Setup;

/// <summary>029 D9: shared path-containment for the setup-config writers — reject a path that escapes the repository
/// root (mirrors <c>DotiInstaller.ResolveInside</c> / <c>ReleaseTargetManifest.ValidatePublishProject</c>). Fail-closed.</summary>
internal static class SetupAssetPaths
{
    /// <summary>Resolve <paramref name="relativePath"/> inside <paramref name="repositoryRoot"/>, throwing if it escapes.</summary>
    public static string ResolveInside(string repositoryRoot, string relativePath)
    {
        string root = Path.GetFullPath(repositoryRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Setup-config managed path escapes repository root: {relativePath}");
        }

        return full;
    }
}
