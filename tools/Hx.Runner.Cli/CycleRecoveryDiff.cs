using Hx.Runner.Core.Process;

namespace Hx.Runner.Cli;

/// <summary>
/// 028 FR-002 / D4 (H2/H3): the self-describing line-level diff, computed at the CLI/recovery-presentation seam where
/// process execution already lives — NOT in the pure <c>FreshnessEvaluator</c> leaf. Given a stale stage's changed
/// prerequisite paths (surfaced by the pure evaluator) and the commit the stage was stamped at, it runs
/// <c>git diff &lt;stampedAtCommit&gt;..HEAD -- &lt;paths&gt;</c>. When <paramref name="stampedAtCommit"/> is null
/// (unborn/detached HEAD), it falls back to a worktree diff (<c>git diff -- &lt;paths&gt;</c>) and labels it; it never
/// throws. Computed LAZILY — only when an attest-eligible step's diff is requested.
/// </summary>
public static class CycleRecoveryDiff
{
    /// <summary>The label prepended to a worktree-fallback diff (when there is no stamp commit to diff against).</summary>
    public const string WorktreeFallbackLabel = "(worktree diff — no stamp commit to diff against)";

    public static string Surface(string repo, string? stampedAtCommit, IReadOnlyList<string>? changedPaths)
    {
        IReadOnlyList<string> paths = changedPaths ?? [];
        if (paths.Count == 0)
        {
            return string.Empty;
        }

        var args = new List<string> { "diff" };
        bool worktreeFallback = string.IsNullOrWhiteSpace(stampedAtCommit);
        if (!worktreeFallback)
        {
            args.Add($"{stampedAtCommit}..HEAD");
        }

        args.Add("--");
        args.AddRange(paths);

        ProcessRunResult result;
        try
        {
            result = ProcessRunner.Run(new ToolCommand("git", args.ToArray(), repo));
        }
        catch (Exception)
        {
            // Self-describing CI must never crash on a presentation enrichment — degrade to the path list.
            return $"(diff unavailable) changed: {string.Join(", ", paths)}";
        }

        if (result.ExitCode != 0)
        {
            return $"(diff unavailable) changed: {string.Join(", ", paths)}";
        }

        string diff = result.StandardOutput.TrimEnd();
        return worktreeFallback && diff.Length > 0 ? $"{WorktreeFallbackLabel}\n{diff}" : diff;
    }
}
