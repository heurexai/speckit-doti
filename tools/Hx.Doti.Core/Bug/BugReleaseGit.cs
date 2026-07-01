using Hx.Runner.Core.Process;

namespace Hx.Doti.Core.Bug;

/// <summary>
/// 030 (bug-release-bridge): decides whether a <c>/doti-bug</c> mini-cycle has ALREADY shipped, so the release-train
/// bridge counts only UNRELEASED bug cycles. A bug's fix commit is the last commit that touched its
/// <c>.doti/bugs/&lt;id&gt;/</c> record; the bug is released iff that commit is reachable from the latest <c>v*</c>
/// release tag. This is SELF-MAINTAINING — once a release tag is cut over a bug's fix, the bug drops out of every
/// future train with no marker to write or seed; a fresh/untagged repo has cut no release, so every test-passed bug
/// stays a member (the bug-fix-only FIRST release works). Fails OPEN to "unreleased" on any git error (a non-git or
/// tag-less tree cannot have shipped the fix), so it never wrongly excludes a genuinely unreleased bug.
/// </summary>
internal static class BugReleaseGit
{
    /// <summary>
    /// The newest <c>v*</c> release tag in the repo (semver-descending), or null when no such release exists yet.
    /// NOTE (bug 035 J(a), DEFERRED): this is intentionally NOT constrained to <c>--merged HEAD</c>. In this project's
    /// dev→main flow the release tag is cut on the MAIN merge-commit, which is a DESCENDANT of the dev working branch —
    /// so <c>--merged HEAD</c> from dev sees NONE of the recent release tags (it drops back to an ancient reachable
    /// one) and would treat every shipped bug as unreleased, breaking the normal path. The reviewer's fork-topology
    /// edge (a side-branch higher-semver tag governing exclusion) is a rare, off-normal-path case whose
    /// <c>--merged HEAD</c> "fix" is incompatible with tags-on-merge-commits and is deferred. <see cref="IsReleased"/>
    /// still verifies the bug's fix commit is reachable FROM this tag, so a bug not under the tag is unreleased.
    /// </summary>
    internal static string? LatestReleaseTag(string repoRoot)
    {
        ProcessRunResult result = ProcessRunner.Run(
            new ToolCommand("git", ["tag", "--list", "v[0-9]*", "--sort=-v:refname"], repoRoot));
        return result.ExitCode != 0
            ? null
            : result.StandardOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()
                ?.Trim();
    }

    /// <summary>True iff the bug's fix commit is reachable from <paramref name="latestTag"/> (already shipped). No tag
    /// (no release yet), an uncommitted bug record (brand-new), or a WORKING-TREE-DIRTY record (a re-opened shipped
    /// bug being re-fixed) → false (unreleased).</summary>
    internal static bool IsReleased(string repoRoot, string bugDir, string? latestTag)
    {
        if (string.IsNullOrWhiteSpace(latestTag))
        {
            return false;
        }

        // Bug 035 Fix J(b): FixCommit only looks at COMMITTED history, so a shipped bug that was re-opened (its
        // record dirtied in the working tree by a fresh assess/fix/test) still resolves to the OLD tagged commit and
        // is wrongly excluded as "already released" — the re-opened work never gets a chance to join a new train.
        // A dirty record is fail-closed treated as unreleased so the re-fix is never silently dropped.
        if (IsWorkingTreeDirty(repoRoot, bugDir))
        {
            return false;
        }

        string? fixCommit = FixCommit(repoRoot, bugDir);
        if (fixCommit is null)
        {
            return false;
        }

        ProcessRunResult result = ProcessRunner.Run(
            new ToolCommand("git", ["merge-base", "--is-ancestor", fixCommit, latestTag], repoRoot));
        return result.ExitCode == 0;
    }

    /// <summary>The last commit that touched the bug record dir, or null when the record is uncommitted / not in git.</summary>
    private static string? FixCommit(string repoRoot, string bugDir)
    {
        string pathspec = Path.GetRelativePath(Path.GetFullPath(repoRoot), bugDir).Replace('\\', '/');
        ProcessRunResult result = ProcessRunner.Run(
            new ToolCommand("git", ["log", "-1", "--format=%H", "--", pathspec], repoRoot));
        string sha = result.StandardOutput.Trim();
        return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(sha) ? sha : null;
    }

    /// <summary>True iff the bug record dir has uncommitted (staged or unstaged) changes. A git error is treated as
    /// "not dirty" so it falls through to the existing committed-history check rather than blocking on a tool fault.</summary>
    private static bool IsWorkingTreeDirty(string repoRoot, string bugDir)
    {
        string pathspec = Path.GetRelativePath(Path.GetFullPath(repoRoot), bugDir).Replace('\\', '/');
        ProcessRunResult result = ProcessRunner.Run(
            new ToolCommand("git", ["status", "--porcelain", "--", pathspec], repoRoot));
        return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput);
    }
}
