namespace Hx.Runner.Core.Repository;

public static class RepositoryPathResolver
{
    public static RepositoryPath ResolveInside(string repositoryRoot, string path)
    {
        string root = Path.GetFullPath(repositoryRoot);
        string fullPath = Path.GetFullPath(Path.Combine(root, path));

        if (!IsInside(root, fullPath))
        {
            throw new InvalidOperationException($"Path is outside the repository root: {path}");
        }

        string relativePath = Path.GetRelativePath(root, fullPath).Replace('\\', '/');
        return new RepositoryPath(fullPath, relativePath);
    }

    public static string NormalizeManifestPath(string relativePath)
    {
        return relativePath.Replace('\\', '/');
    }

    private static bool IsInside(string root, string fullPath)
    {
        string normalizedRoot = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        StringComparison pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return fullPath.StartsWith(normalizedRoot, pathComparison)
            || string.Equals(Path.TrimEndingDirectorySeparator(root), Path.TrimEndingDirectorySeparator(fullPath), pathComparison);
    }
}
