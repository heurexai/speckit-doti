using Hx.Runner.Core.Process;

namespace Hx.Impact.Core.ChangeDetection;

/// <summary>
/// Collects the changed repo-relative paths for a change set: the committed diff
/// <c>merge-base(base,head)..head</c> UNION the working tree (staged + unstaged + untracked) via
/// <c>git status --porcelain</c>. Reuses <see cref="ProcessRunner"/>; all paths are '/'-normalized.
/// Fails closed (throws) if the merge-base cannot be resolved.
/// </summary>
public sealed class ImpactChangeCollector
{
    public IReadOnlyList<string> Collect(string repositoryRoot, string baseRef, string headRef)
    {
        ProcessRunResult mergeBase = Git(repositoryRoot, "merge-base", baseRef, headRef);
        if (mergeBase.ExitCode != 0 || string.IsNullOrWhiteSpace(mergeBase.StandardOutput))
        {
            string detail = string.IsNullOrWhiteSpace(mergeBase.StandardError) ? $"exit {mergeBase.ExitCode}" : mergeBase.StandardError.Trim();
            throw new InvalidOperationException($"Could not resolve merge-base for '{baseRef}'..'{headRef}': {detail}");
        }

        string baseSha = mergeBase.StandardOutput.Trim();
        var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        ProcessRunResult diff = Git(repositoryRoot, "diff", "--name-status", "-M", "-z", baseSha, headRef);
        foreach (string path in ParseNameStatusZ(diff.StandardOutput))
        {
            paths.Add(path);
        }

        ProcessRunResult status = Git(repositoryRoot, "status", "--porcelain=v1", "-z", "--untracked-files=all");
        foreach (string path in ParsePorcelainZ(status.StandardOutput))
        {
            paths.Add(path);
        }

        return paths.ToArray();
    }

    private static ProcessRunResult Git(string repositoryRoot, params string[] arguments) =>
        ProcessRunner.Run(new ToolCommand("git", arguments, repositoryRoot));

    // `--name-status -z`: NUL-separated [status, path] pairs; rename/copy entries are [status, old, new].
    private static IEnumerable<string> ParseNameStatusZ(string output)
    {
        if (string.IsNullOrEmpty(output)) { yield break; }

        string[] tokens = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length;)
        {
            string status = tokens[i++];
            if ((status.StartsWith('R') || status.StartsWith('C')) && i + 1 < tokens.Length)
            {
                i++; // skip the old path
                yield return Normalize(tokens[i++]);
            }
            else if (i < tokens.Length)
            {
                yield return Normalize(tokens[i++]);
            }
        }
    }

    // `status --porcelain=v1 -z`: each record is "XY <path>"; rename records add a following old-path token.
    private static IEnumerable<string> ParsePorcelainZ(string output)
    {
        if (string.IsNullOrEmpty(output)) { yield break; }

        string[] tokens = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (token.Length < 4) { continue; }

            string statusCode = token[..2];
            yield return Normalize(token[3..]);
            if (statusCode.Contains('R') && i + 1 < tokens.Length)
            {
                i++; // the rename's old path follows; the new path (above) is what we keep
            }
        }
    }

    private static string Normalize(string path) => path.Replace('\\', '/').Trim();
}
