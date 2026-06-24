using System.Text.Json;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Core.Release;

public static class ReleaseTargetManifest
{
    public const string RelativePath = ".doti/release.json";

    public static LocalReleaseTarget Load(string repositoryRoot)
    {
        string repo = Path.GetFullPath(repositoryRoot);
        string path = Path.Combine(repo, RelativePath);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"Release target manifest is missing: {RelativePath}. " +
                "Create this tracked repo-owned file so hx release knows which product executable to publish.");
        }

        ReleaseTargetManifestDocument document;
        try
        {
            document = JsonSerializer.Deserialize<ReleaseTargetManifestDocument>(
                    File.ReadAllText(path),
                    JsonContractSerializerOptions.Create())
                ?? throw new InvalidOperationException("Release target manifest is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Release target manifest is not valid JSON: {RelativePath}. {ex.Message}");
        }

        if (document.SchemaVersion != 1)
        {
            throw new InvalidOperationException(
                $"Release target manifest {RelativePath} has unsupported schemaVersion '{document.SchemaVersion}'. Expected 1.");
        }

        string productName = Required(document.ProductName, "productName");
        string packageName = SafeName(Required(document.PackageName, "packageName"), "packageName");
        string publishProject = Required(document.PublishProject, "publishProject").Replace('\\', '/');
        string publishedExecutableName = SafeName(Required(document.PublishedExecutableName, "publishedExecutableName"), "publishedExecutableName");
        string executableName = SafeName(Required(document.ExecutableName, "executableName"), "executableName");
        string defaultReleaseRootEnvironmentVariable = Required(
            document.DefaultReleaseRootEnvironmentVariable,
            "defaultReleaseRootEnvironmentVariable");

        if (!LocalReleaseRootResolver.IsValidEnvironmentVariableName(defaultReleaseRootEnvironmentVariable))
        {
            throw new InvalidOperationException(
                $"Release target manifest {RelativePath} has invalid defaultReleaseRootEnvironmentVariable '{defaultReleaseRootEnvironmentVariable}'.");
        }

        ValidatePublishProject(repo, publishProject);

        return new LocalReleaseTarget(
            productName,
            packageName,
            publishProject,
            publishedExecutableName,
            executableName,
            defaultReleaseRootEnvironmentVariable);
    }

    public static void WriteDefault(
        string repositoryRoot,
        string productName,
        string publishProject,
        string publishedExecutableName,
        string executableName,
        string defaultReleaseRootEnvironmentVariable)
    {
        string repo = Path.GetFullPath(repositoryRoot);
        string path = Path.Combine(repo, RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var document = new ReleaseTargetManifestDocument
        {
            SchemaVersion = 1,
            ProductName = productName,
            PackageName = SafeName(productName, "packageName"),
            PublishProject = publishProject.Replace('\\', '/'),
            PublishedExecutableName = SafeName(publishedExecutableName, "publishedExecutableName"),
            ExecutableName = SafeName(executableName, "executableName"),
            DefaultReleaseRootEnvironmentVariable = defaultReleaseRootEnvironmentVariable
        };

        File.WriteAllText(path, JsonSerializer.Serialize(document, JsonContractSerializerOptions.Create()));
    }

    private static void ValidatePublishProject(string repo, string publishProject)
    {
        if (Path.IsPathRooted(publishProject) || publishProject.Split('/').Any(segment => segment == ".."))
        {
            throw new InvalidOperationException(
                $"Release target manifest {RelativePath} publishProject must be a relative path inside the repository.");
        }

        if (!publishProject.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Release target manifest {RelativePath} publishProject must point to a .csproj file.");
        }

        string full = Path.GetFullPath(Path.Combine(repo, publishProject));
        string repoFull = EnsureTrailingSeparator(Path.GetFullPath(repo));
        if (!full.StartsWith(repoFull, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
        {
            throw new InvalidOperationException(
                $"Release target manifest {RelativePath} publishProject was not found inside the repository: {publishProject}");
        }
    }

    private static string Required(string? value, string property)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Release target manifest {RelativePath} is missing required property '{property}'.");
        }

        return value.Trim();
    }

    private static string SafeName(string value, string property)
    {
        string trimmed = value.Trim();
        if (trimmed.Length == 0
            || trimmed.Contains('/')
            || trimmed.Contains('\\')
            || trimmed.Contains(':')
            || trimmed.Any(c => !char.IsLetterOrDigit(c) && c is not '.' and not '_' and not '-')
            || trimmed.Trim('.', '-', '_').Length == 0)
        {
            throw new InvalidOperationException(
                $"Release target manifest {RelativePath} property '{property}' must contain only letters, digits, dots, underscores, or hyphens.");
        }

        return trimmed;
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;

    private sealed class ReleaseTargetManifestDocument
    {
        public int SchemaVersion { get; init; }
        public string? ProductName { get; init; }
        public string? PackageName { get; init; }
        public string? PublishProject { get; init; }
        public string? PublishedExecutableName { get; init; }
        public string? ExecutableName { get; init; }
        public string? DefaultReleaseRootEnvironmentVariable { get; init; }
    }
}
