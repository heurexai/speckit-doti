using Hx.Runner.Core.Process;

namespace Hx.Cycle.Core;

/// <summary>The staged/unstaged shape of the working tree for the codified commit's scope check.</summary>
public sealed record CommitScope(bool HasStaged, bool HasUnstagedTrackedChanges, int StagedCount);

/// <summary>
/// Inspects <c>git status --porcelain=v1 -z</c> to decide whether the tree is a clean, deliberate scope
/// for <c>cycle commit</c>: a non-empty staged set and no unstaged tracked modifications. Untracked files
/// (<c>??</c>) are ignored — they are not part of a staged commit. Fails closed (throws) on a git error.
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

        int staged = 0;
        bool unstagedTracked = false;
        string[] tokens = result.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            string entry = tokens[i];
            if (entry.Length < 3)
            {
                continue;
            }

            char index = entry[0];
            char worktree = entry[1];

            // Rename/copy entries (`-z`) are followed by the original-path token — skip it.
            if (index is 'R' or 'C')
            {
                i++;
            }

            if (index == '?' && worktree == '?')
            {
                continue; // untracked — not part of a staged commit
            }

            if (index != ' ' && index != '?')
            {
                staged++; // staged (index) change
            }

            if (worktree != ' ' && worktree != '?')
            {
                unstagedTracked = true; // a tracked file changed in the worktree but not staged
            }
        }

        return new CommitScope(staged > 0, unstagedTracked, staged);
    }
}
