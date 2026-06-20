using Hx.Runner.Core.Process;

namespace Hx.Runner.Core.Git;

/// <summary>
/// Discovers changed files from the Git index (staged) or a ref range. This is a
/// hygiene-scoping helper only; it is not the affected-change planner.
/// </summary>
public static class GitChangedFileDiscovery
{
    public static IReadOnlyList<ChangedFile> DiscoverStaged(string repositoryRoot)
    {
        ProcessRunResult result = ProcessRunner.Run(new ToolCommand(
            "git",
            ["diff", "--cached", "--name-status", "--find-renames", "-z"],
            repositoryRoot));

        EnsureGitSucceeded(result);
        return ParseNameStatusZ(result.StandardOutput);
    }

    public static IReadOnlyList<ChangedFile> DiscoverRange(string repositoryRoot, string baseRef, string headRef)
    {
        ProcessRunResult result = ProcessRunner.Run(new ToolCommand(
            "git",
            ["diff", "--name-status", "-z", baseRef, headRef],
            repositoryRoot));

        EnsureGitSucceeded(result);
        return ParseNameStatusZ(result.StandardOutput);
    }

    private static void EnsureGitSucceeded(ProcessRunResult result)
    {
        if (result.ExitCode != 0)
        {
            string message = string.IsNullOrWhiteSpace(result.StandardError)
                ? $"git diff exited with code {result.ExitCode}."
                : result.StandardError.Trim();
            throw new InvalidOperationException(message);
        }
    }

    // git -z output is NUL-separated. For rename/copy entries the status token is
    // followed by two path tokens (source, destination); we keep the destination.
    private static IReadOnlyList<ChangedFile> ParseNameStatusZ(string output)
    {
        List<ChangedFile> files = [];
        string[] tokens = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        int i = 0;
        while (i < tokens.Length)
        {
            string status = tokens[i++];
            char code = status.Length > 0 ? status[0] : 'M';
            ChangeKind kind = MapKind(code);

            string path;
            if ((code == 'R' || code == 'C') && i + 1 < tokens.Length)
            {
                i++; // skip source path
                path = tokens[i++];
            }
            else if (i < tokens.Length)
            {
                path = tokens[i++];
            }
            else
            {
                break;
            }

            files.Add(new ChangedFile(path.Replace('\\', '/'), kind));
        }

        return files;
    }

    private static ChangeKind MapKind(char code) => code switch
    {
        'A' => ChangeKind.Added,
        'M' => ChangeKind.Modified,
        'R' => ChangeKind.Renamed,
        'C' => ChangeKind.Copied,
        'D' => ChangeKind.Deleted,
        _ => ChangeKind.Other
    };
}
