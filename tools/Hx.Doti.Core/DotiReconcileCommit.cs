using Hx.Tooling.Contracts;

namespace Hx.Doti.Core;

/// <summary>
/// 031 T006 (FR-007/008/009/010/011, D4): the self-owned, sanctioned reconcile commit. After a SUCCESSFUL reconcile
/// in a GIT work tree, it stages the managed-asset paths the reconcile touched (never <c>git add -A</c>) and makes ONE
/// commit the coded path OWNS. The staging + pathspec-scoped commit + sentinel + lock-retry mechanics live in the
/// shared <see cref="SanctionedGitCommit"/> (035) so this and <see cref="Hx.Doti.Core.Bug.BugReleaseDocCommit"/>
/// cannot drift; this type owns only the reconcile-specific candidate set (<see cref="TouchedPaths(DotiInstallResult)"/>),
/// the <c>--no-commit</c>/non-git pre-checks, and the auto message. No staged change → NO commit (idempotent re-run);
/// a non-git target → skip with no error; <c>--no-commit</c> → skip, leaving the reconciled changes in the working tree.
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
        if (!SanctionedGitCommit.IsInsideWorkTree(root))
        {
            return new DotiReconcileCommitOutcome(
                DotiCommitStatus.NonGit, null, [], "target is not a git work tree; reconcile applied, commit skipped.");
        }

        IReadOnlyList<string> candidates = NormalizeCandidates(touchedPaths);
        string message = BuildMessage(beforeVersion, afterVersion, prunedPaths);
        SanctionedGitCommit.Result result = SanctionedGitCommit.Commit(root, candidates, message);
        return result.Status switch
        {
            DotiCommitStatus.NoChange => NoChange(),
            _ => new DotiReconcileCommitOutcome(result.Status, result.Sha, result.Staged, result.Reason),
        };
    }

    /// <summary>
    /// 031 D4/FR-008: the exact managed-asset paths a reconcile touched and the self-commit stages —
    /// <c>Installed ∪ Removed ∪ Rendered</c>, de-duped and MINUS every <c>.new</c> merge-helper. (An unstageable path —
    /// a gitignored runtime path, or an untracked orphan — is skipped by <see cref="SanctionedGitCommit"/> at stage
    /// time.) The commit NEVER uses <c>git add -A</c>, so an operator's unrelated working-tree changes are never staged.
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

    private static IReadOnlyList<string> Dedupe(IEnumerable<string> all) => NormalizeCandidates([.. all]);

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

    // De-dupe, normalize to forward slashes, and drop any leftover .new (a defensive fence so a .new can never be
    // staged) and empty entries. This is the "already normalized" contract SanctionedGitCommit.Commit expects.
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
}
