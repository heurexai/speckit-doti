using Hx.Runner.Core.Process;
using Hx.Runner.Core.Io;

namespace Hx.Cycle.Core;

/// <summary>The staged/unstaged shape of the working tree for the codified commit's scope check.</summary>
public sealed record CommitScope(
    bool HasStaged,
    bool HasUnstagedTrackedChanges,
    bool HasUntrackedChanges,
    int StagedCount,
    string StagedTreeId,
    IReadOnlyList<string> StagedPaths);

/// <summary>
/// Inspects <c>git status --porcelain=v1 -z</c> to decide whether the tree is a clean, deliberate scope
/// for a Doti-owned transition/release commit: no unstaged tracked modifications and no untracked files.
/// Fails closed (throws) on a git error.
/// </summary>
public static class CommitScopeInspector
{
    public static CommitScope Inspect(string repositoryRoot)
    {
        ProcessRunResult result = ProcessRunner.Run(
            new ToolCommand("git", ["status", "--porcelain=v1", "-z"], repositoryRoot));
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"git status failed: {result.StandardError.Trim()}");
        }

        ParsedScope parsed = ParseStatus(result.StandardOutput);
        return new CommitScope(
            parsed.StagedCount > 0,
            parsed.HasUnstagedTrackedChanges,
            parsed.HasUntrackedChanges,
            parsed.StagedCount,
            ComputeStagedTreeId(repositoryRoot, parsed.StagedPaths ?? []),
            parsed.StagedPaths ?? []);
    }

    private static ParsedScope ParseStatus(string porcelain)
    {
        var parsed = new ParsedScope();
        var stagedPaths = new List<string>();
        string[] tokens = porcelain.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            string entry = tokens[i];
            if (entry.Length < 3)
            {
                continue;
            }

            PorcelainEntry parsedEntry = PorcelainEntry.Parse(entry);
            if (parsedEntry.SkipsFollowingPath)
            {
                i++;
            }

            if (parsedEntry.IsUntracked)
            {
                parsed = parsed with { HasUntrackedChanges = true };
                continue;
            }

            if (parsedEntry.IsStaged)
            {
                stagedPaths.Add(parsedEntry.Path);
                parsed = parsed with { StagedCount = parsed.StagedCount + 1 };
            }

            if (parsedEntry.HasUnstagedTrackedChange)
            {
                parsed = parsed with { HasUnstagedTrackedChanges = true };
            }
        }

        return parsed with { StagedPaths = stagedPaths };
    }

    private static string ComputeStagedTreeId(string repositoryRoot, IEnumerable<string> paths)
    {
        var lines = new List<string>();
        foreach (string path in paths.OrderBy(p => p, StringComparer.Ordinal))
        {
            ProcessRunResult blob = ProcessRunner.Run(
                new ToolCommand("git", ["rev-parse", "--verify", $":{path}"], repositoryRoot));
            string blobId = blob.ExitCode == 0 ? blob.StandardOutput.Trim() : "absent";
            lines.Add($"{path}\t{blobId}");
        }

        return FileHashing.Sha256OfText(string.Join("\n", lines));
    }

    private sealed record ParsedScope(
        bool HasUnstagedTrackedChanges = false,
        bool HasUntrackedChanges = false,
        int StagedCount = 0,
        IReadOnlyList<string>? StagedPaths = null);

    private sealed record PorcelainEntry(char Index, char Worktree, string Path)
    {
        public bool SkipsFollowingPath => Index is 'R' or 'C';
        public bool IsUntracked => Index == '?' && Worktree == '?';
        public bool IsStaged => Index != ' ' && Index != '?';
        public bool HasUnstagedTrackedChange => Worktree != ' ' && Worktree != '?';

        public static PorcelainEntry Parse(string entry) =>
            new(entry[0], entry[1], entry[3..].Replace('\\', '/'));
    }
}
