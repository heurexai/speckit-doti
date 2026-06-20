using Hx.Runner.Core.Process;

namespace Hx.Runner.Core.Git;

/// <summary>
/// Materializes selected staged blobs into a private temporary scan root so a
/// changed-file scan never traverses the whole repository. Repo-relative
/// structure is preserved; the root is deleted on <see cref="Dispose"/>.
/// </summary>
public sealed class StagedBlobMaterializer : IDisposable
{
    private StagedBlobMaterializer(string root, IReadOnlyList<string> materializedPaths)
    {
        Root = root;
        MaterializedPaths = materializedPaths;
    }

    public string Root { get; }

    /// <summary>Repo-relative paths that were materialized into <see cref="Root"/>.</summary>
    public IReadOnlyList<string> MaterializedPaths { get; }

    public static StagedBlobMaterializer Create(string repositoryRoot, IEnumerable<string> repoRelativePaths)
    {
        string root = Path.Combine(Path.GetTempPath(), "hx-hygiene-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(root);

        List<string> materialized = [];
        foreach (string relative in repoRelativePaths)
        {
            ProcessRunResult result = ProcessRunner.Run(new ToolCommand(
                "git",
                ["show", ":" + relative],
                repositoryRoot));

            if (result.ExitCode != 0)
            {
                continue;
            }

            string destination = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllText(destination, result.StandardOutput);
            materialized.Add(relative);
        }

        return new StagedBlobMaterializer(root, materialized);
    }

    public string ToRepoRelative(string materializedPath)
    {
        string full = Path.GetFullPath(materializedPath);
        string relative = Path.GetRelativePath(Root, full).Replace('\\', '/');
        return relative;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of a temporary directory.
        }
    }
}
