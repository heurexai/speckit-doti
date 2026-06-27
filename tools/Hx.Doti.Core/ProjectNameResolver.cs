namespace Hx.Doti.Core;

/// <summary>
/// Derives a project name for the constitution title (009 FR-015) — never a <c>[PROJECT_NAME]</c> placeholder.
/// Precedence: an explicit name (e.g. <c>hx new --name</c>) wins; else the single top-level <c>.slnx</c>/<c>.sln</c>
/// solution base name (<c>.slnx</c> preferred); else the repository directory name. Pure + IO-light (a top-level
/// glob), single-responsibility, deterministic.
/// </summary>
public static class ProjectNameResolver
{
    public static string Resolve(string repositoryRoot, string? explicitName = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitName))
        {
            return explicitName.Trim();
        }

        return SingleSolutionName(repositoryRoot, "*.slnx")
            ?? SingleSolutionName(repositoryRoot, "*.sln")
            ?? DirectoryName(repositoryRoot);
    }

    private static string? SingleSolutionName(string repositoryRoot, string pattern)
    {
        if (!Directory.Exists(repositoryRoot))
        {
            return null;
        }

        string[] files = Directory.GetFiles(repositoryRoot, pattern, SearchOption.TopDirectoryOnly);
        return files.Length == 1 ? Path.GetFileNameWithoutExtension(files[0]) : null;
    }

    private static string DirectoryName(string repositoryRoot)
    {
        string name = new DirectoryInfo(Path.GetFullPath(repositoryRoot)).Name;
        return string.IsNullOrWhiteSpace(name) ? "Project" : name;
    }
}
