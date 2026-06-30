using Hx.Cycle.Core;
using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;

namespace Hx.Doti.Core;

/// <summary>
/// 031 T006 (FR-007/008/009/010/011, D4): the self-owned, sanctioned reconcile commit. After a SUCCESSFUL reconcile
/// in a GIT work tree, it stages EXACTLY the managed-asset paths the reconcile touched (never <c>git add -A</c>) —
/// minus every <c>.new</c> merge-helper and minus gitignored runtime state — and makes ONE commit the coded path
/// OWNS: it sets <see cref="PrecommitGuard.SentinelEnvVar"/>=1 in the child <c>git commit</c> environment so the
/// insurance pre-commit hook permits it, exactly as the workflow-transition and release paths already do. No staged
/// change → NO commit (idempotent re-run). A non-git target → skip with no error (parity with the hook's non-git
/// skip). <c>--no-commit</c> → skip, leaving the reconciled changes in the working tree. Shells <c>git</c> through the
/// existing <see cref="ProcessRunner"/>/<see cref="ToolCommand"/> (the same pattern as
/// <see cref="Hx.Doti.Core.Bug.BugReleaseGit"/> / <c>GitRefs</c>).
/// </summary>
public static class DotiReconcileCommit
{
    /// <summary>
    /// Stage the <paramref name="touchedPaths"/> (already MINUS <c>.new</c> sidecars by the caller) in
    /// <paramref name="repoRoot"/> and commit them as a sanctioned Doti reconcile commit. <paramref name="commit"/>
    /// false (the <c>--no-commit</c> opt-out) returns <see cref="DotiCommitStatus.Disabled"/> without touching the
    /// index. The auto message names the <paramref name="beforeVersion"/>→<paramref name="afterVersion"/> move and any
    /// <paramref name="prunedPaths"/>.
    /// </summary>
    public static DotiReconcileCommitOutcome Commit(
        string repoRoot,
        IReadOnlyList<string> touchedPaths,
        string? beforeVersion,
        string? afterVersion,
        IReadOnlyList<string> prunedPaths,
        bool commit)
    {
        if (!commit)
        {
            return new DotiReconcileCommitOutcome(
                DotiCommitStatus.Disabled, null, [], "--no-commit: reconciled changes left in the working tree.");
        }

        string root = Path.GetFullPath(repoRoot);
        if (!IsInsideWorkTree(root))
        {
            return new DotiReconcileCommitOutcome(
                DotiCommitStatus.NonGit, null, [], "target is not a git work tree; reconcile applied, commit skipped.");
        }

        IReadOnlyList<string> candidates = NormalizeCandidates(touchedPaths);
        IReadOnlyList<string> stageable = ExcludeIgnored(root, candidates);
        if (stageable.Count == 0)
        {
            return NoChange();
        }

        ProcessRunResult add = Git(root, ["add", "--", .. stageable]);
        if (add.ExitCode != 0)
        {
            return new DotiReconcileCommitOutcome(
                DotiCommitStatus.Failed, null, [], "git add failed: " + Prefer(add.StandardError, add.StandardOutput));
        }

        IReadOnlyList<string> staged = StagedPaths(root);
        if (staged.Count == 0)
        {
            return NoChange();
        }

        string message = BuildMessage(beforeVersion, afterVersion, prunedPaths);
        ProcessRunResult result = Git(
            root,
            ["commit", "-m", message],
            new Dictionary<string, string> { [PrecommitGuard.SentinelEnvVar] = "1" });
        if (result.ExitCode != 0)
        {
            return new DotiReconcileCommitOutcome(
                DotiCommitStatus.Failed, null, staged, "git commit failed: " + Prefer(result.StandardError, result.StandardOutput));
        }

        return new DotiReconcileCommitOutcome(DotiCommitStatus.Committed, HeadSha(root), staged, null);
    }

    /// <summary>
    /// 031 D4/FR-008: the exact managed-asset paths a reconcile touched and the self-commit stages —
    /// <c>Installed ∪ Removed ∪ Rendered</c>, de-duped and MINUS every <c>.new</c> merge-helper. (Gitignored runtime
    /// state is dropped later by <see cref="ExcludeIgnored"/> against the real repo.) This is the precise set; the
    /// commit NEVER uses <c>git add -A</c>, so an operator's unrelated working-tree changes are never staged.
    /// </summary>
    public static IReadOnlyList<string> TouchedPaths(DotiInstallResult install)
    {
        IEnumerable<string> all = install.Installed.Select(e => e.Path)
            .Concat(install.Removed.Select(e => e.Path))
            .Concat(install.Rendered);
        return Dedupe(all);
    }

    /// <summary>
    /// 031 D4/FR-008 (worktree path): the touched set derived from a <see cref="DotiUpdateOutcome"/>'s flattened
    /// <c>Changes</c> — the installed (which already includes rendered) and removed effects. Used by
    /// <see cref="DotiWorktreeUpdate"/>, which has applied the change set back to the REAL repo and commits there.
    /// </summary>
    public static IReadOnlyList<string> TouchedPaths(DotiUpdateOutcome outcome) =>
        Dedupe(outcome.Changes
            .Where(c => c.Effect is "installed" or "removed")
            .Select(c => c.Path));

    private static IReadOnlyList<string> Dedupe(IEnumerable<string> all)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();
        foreach (string raw in all)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            string path = raw.Replace('\\', '/').TrimEnd('/');
            if (path.EndsWith(".new", StringComparison.OrdinalIgnoreCase) || !seen.Add(path))
            {
                continue;
            }

            ordered.Add(path);
        }

        return ordered;
    }

    /// <summary>
    /// The auto-commit message: a <c>chore(doti):</c> headline naming the version move, a before→after line, and a
    /// one-line pruned-orphan summary when any orphan was pruned.
    /// </summary>
    public static string BuildMessage(string? beforeVersion, string? afterVersion, IReadOnlyList<string> prunedPaths)
    {
        string before = string.IsNullOrWhiteSpace(beforeVersion) ? "—" : beforeVersion!;
        string after = string.IsNullOrWhiteSpace(afterVersion) ? "—" : afterVersion!;
        var lines = new List<string>
        {
            $"chore(doti): reconcile Doti assets to {after}",
            string.Empty,
            $"Doti payload {before} -> {after}.",
        };
        if (prunedPaths.Count > 0)
        {
            lines.Add($"Pruned {prunedPaths.Count} orphaned managed asset(s) the render no longer targets:");
            foreach (string path in prunedPaths)
            {
                lines.Add($"  - {path}");
            }
        }

        return string.Join("\n", lines);
    }

    private static DotiReconcileCommitOutcome NoChange() =>
        new(DotiCommitStatus.NoChange, null, [], "no managed-asset change to commit.");

    // De-dupe, normalize to forward slashes, and drop any leftover .new (the caller already excludes them; this is a
    // defensive second fence so a .new can never be staged) and empty entries.
    private static IReadOnlyList<string> NormalizeCandidates(IReadOnlyList<string> touchedPaths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();
        foreach (string raw in touchedPaths)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            string path = raw.Replace('\\', '/').TrimEnd('/');
            if (path.EndsWith(".new", StringComparison.OrdinalIgnoreCase) || !seen.Add(path))
            {
                continue;
            }

            ordered.Add(path);
        }

        return ordered;
    }

    // FR-008: drop gitignored runtime state so the precise staging never adds an ignored path. `git check-ignore`
    // returns the subset of the candidates that ARE ignored; everything else is stageable. A non-zero exit with empty
    // output means "none ignored" (git returns 1 when no path matches), so an empty stdout keeps all candidates.
    private static IReadOnlyList<string> ExcludeIgnored(string root, IReadOnlyList<string> candidates)
    {
        if (candidates.Count == 0)
        {
            return candidates;
        }

        ProcessRunResult result = Git(root, ["check-ignore", "--", .. candidates]);
        var ignored = new HashSet<string>(
            result.StandardOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim().Replace('\\', '/').TrimEnd('/')),
            StringComparer.OrdinalIgnoreCase);
        return ignored.Count == 0
            ? candidates
            : candidates.Where(p => !ignored.Contains(p)).ToList();
    }

    private static IReadOnlyList<string> StagedPaths(string root)
    {
        ProcessRunResult result = Git(root, ["diff", "--cached", "--name-only"]);
        return result.ExitCode != 0
            ? []
            : result.StandardOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim().Replace('\\', '/'))
                .Where(p => p.Length > 0)
                .ToList();
    }

    private static bool IsInsideWorkTree(string root)
    {
        ProcessRunResult result = Git(root, ["rev-parse", "--is-inside-work-tree"]);
        return result.ExitCode == 0 && result.StandardOutput.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static string? HeadSha(string root)
    {
        ProcessRunResult result = Git(root, ["rev-parse", "HEAD"]);
        return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput)
            ? result.StandardOutput.Trim()
            : null;
    }

    private static ProcessRunResult Git(string root, IReadOnlyList<string> args, IReadOnlyDictionary<string, string>? env = null)
    {
        try
        {
            return ProcessRunner.Run(new ToolCommand("git", args, root, env));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new ProcessRunResult(127, string.Empty, ex.Message);
        }
    }

    private static string Prefer(string primary, string fallback) =>
        string.IsNullOrWhiteSpace(primary) ? fallback.Trim() : primary.Trim();
}
