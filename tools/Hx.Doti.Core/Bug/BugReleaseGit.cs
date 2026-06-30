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
    /// <summary>The newest <c>v*</c> release tag (semver-descending), or null when the repo has cut no release yet.</summary>
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
    /// (no release yet) or an uncommitted bug record (brand-new) → false (unreleased).</summary>
    internal static bool IsReleased(string repoRoot, string bugDir, string? latestTag)
    {
        if (string.IsNullOrWhiteSpace(latestTag))
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
}
