using Hx.Cycle.Core;
using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;

namespace Hx.Doti.Core.Bug;

/// <summary>
/// 034 (bug-only-release-doc-commit): the sanctioned, GATED commit for the release-documentation fix a bug-only
/// release train demands. A bug-only repo (a confirmed <c>/doti-bug</c> mini-cycle with NO numbered feature cycle,
/// hence no <c>.doti/cycle-state.json</c>) can run the release gate (033), whose <c>release-documentation</c> step
/// then requires the bug's slug in <c>README.md</c>/<c>CHANGELOG.md</c> — but there was no coded sanctioned path to
/// commit that fix. This is that path:
/// <list type="bullet">
///   <item>Requires a git work tree (a non-git target is a skip, not an error — parity with the hook's non-git skip).</item>
///   <item><b>Fail-closed GATE</b>: proceeds ONLY when <see cref="BugCycleService.ReleaseReadyBugMembers(string)"/>
///   reports ≥1 included member — a confirmed, fix-bound, test-passed bug candidate. With zero eligible members it
///   REFUSES before any git mutation; it is NOT a generic commit backdoor.</item>
///   <item>Stages EXACTLY the dirty release-documentation surfaces (<c>README.md</c>/<c>CHANGELOG.md</c>) and commits
///   ONLY those — the whole-index-safe, sentinel + lock-retry mechanics are the shared 035
///   <see cref="SanctionedGitCommit"/>, so a file the operator already staged is never swept into this commit.</item>
/// </list>
/// </summary>
public static class BugReleaseDocCommit
{
    /// <summary>The release-documentation surfaces this command is scoped to — never any other path.</summary>
    private static readonly IReadOnlyList<string> ReleaseDocSurfaces = ["README.md", "CHANGELOG.md"];

    /// <summary>
    /// Stage the dirty subset of <see cref="ReleaseDocSurfaces"/> in <paramref name="repoRoot"/> and commit them as a
    /// sanctioned bug-release-doc commit — but ONLY when the gate (<paramref name="releaseReadyBugMembers"/>) reports
    /// at least one eligible bug. <paramref name="bugIds"/> (already-included members, when known) names the commit
    /// message; when empty the message falls back to a generic bug-release-train summary.
    /// </summary>
    public static BugReleaseDocCommitOutcome Commit(
        string repoRoot,
        IReadOnlyList<string>? bugIds = null,
        Func<string, IReadOnlyList<CycleReleaseTrainFeature>>? releaseReadyBugMembers = null)
    {
        string root = Path.GetFullPath(repoRoot);
        if (!SanctionedGitCommit.IsInsideWorkTree(root))
        {
            return new BugReleaseDocCommitOutcome(
                DotiCommitStatus.NonGit, null, [], [],
                "target is not a git work tree; release-doc commit skipped.");
        }

        // Fail-closed gate — BEFORE any git mutation (only the read-only work-tree check above precedes it).
        Func<string, IReadOnlyList<CycleReleaseTrainFeature>> gate =
            releaseReadyBugMembers ?? BugCycleService.ReleaseReadyBugMembers;
        IReadOnlyList<CycleReleaseTrainFeature> eligible = gate(root);
        if (eligible.Count == 0)
        {
            return new BugReleaseDocCommitOutcome(
                DotiCommitStatus.Refused, null, [], [],
                "no release-ready bug member (a confirmed, fix-bound, test-passed /doti-bug mini-cycle) was found; "
                + "refusing to commit before any git mutation. Run `doti bug assess`/`fix`/`test` to completion first.");
        }

        IReadOnlyList<string> surfaces = DirtyReleaseDocSurfaces(root);
        string message = BuildMessage(bugIds is { Count: > 0 } ? bugIds : eligible.Select(e => e.Feature).ToList());
        SanctionedGitCommit.Result result = SanctionedGitCommit.Commit(root, surfaces, message);
        return result.Status switch
        {
            DotiCommitStatus.NoChange => NoChange(eligible),
            _ => new BugReleaseDocCommitOutcome(result.Status, result.Sha, result.Staged, eligible, result.Reason),
        };
    }

    /// <summary>The auto-commit message: names the bug slug(s) the release-documentation fix covers.</summary>
    public static string BuildMessage(IReadOnlyList<string> bugIds)
    {
        string subject = bugIds.Count > 0
            ? string.Join(", ", bugIds.Order(StringComparer.OrdinalIgnoreCase))
            : "the bug release train";
        return $"docs(release): document {subject} for release";
    }

    // The subset of ReleaseDocSurfaces that exist on disk AND are dirty (untracked or modified) per `git status
    // --porcelain`. Never a broader scope, and never a path outside the two release-documentation surfaces.
    private static IReadOnlyList<string> DirtyReleaseDocSurfaces(string root)
    {
        var dirty = new HashSet<string>(
            Git(root, ["status", "--porcelain", "--", .. ReleaseDocSurfaces]).StandardOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                // porcelain format: "XY <path>" (renames not applicable to these two fixed surfaces).
                .Select(line => line.Length > 3 ? line[3..].Trim().Replace('\\', '/') : string.Empty)
                .Where(path => path.Length > 0),
            StringComparer.OrdinalIgnoreCase);
        return ReleaseDocSurfaces.Where(dirty.Contains).ToList();
    }

    private static BugReleaseDocCommitOutcome NoChange(IReadOnlyList<CycleReleaseTrainFeature> eligible) =>
        new(DotiCommitStatus.NoChange, null, [], eligible, "no release-documentation change to commit.");

    // A read-only status probe (the stage+commit mutations are the shared SanctionedGitCommit); a git failure yields
    // empty output → no dirty surfaces → NoChange, never a false commit.
    private static ProcessRunResult Git(string root, IReadOnlyList<string> args)
    {
        try
        {
            return ProcessRunner.Run(new ToolCommand("git", args, root));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new ProcessRunResult(127, string.Empty, ex.Message);
        }
    }
}
