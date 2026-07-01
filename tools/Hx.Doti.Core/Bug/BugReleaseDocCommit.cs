using Hx.Cycle.Core;
using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;

namespace Hx.Doti.Core.Bug;

/// <summary>
/// 034 (bug-only-release-doc-commit): the sanctioned, GATED commit for the release-documentation fix a bug-only
/// release train demands. A bug-only repo (a confirmed <c>/doti-bug</c> mini-cycle with NO numbered feature cycle,
/// hence no <c>.doti/cycle-state.json</c>) can run the release gate (033), and its <c>release-documentation</c> step
/// (<see cref="Hx.Cycle.Core.Documentation.ReleaseDocumentationInspector"/>) then requires the bug's slug in
/// <c>README.md</c>/<c>CHANGELOG.md</c> — but there was no coded sanctioned path to commit that fix. This is that
/// path, modeled on <see cref="DotiReconcileCommit"/>:
/// <list type="bullet">
///   <item>Requires a git work tree (a non-git target is a skip, not an error — parity with the hook's non-git skip).</item>
///   <item><b>Fail-closed GATE</b>: proceeds ONLY when <see cref="BugCycleService.ReleaseReadyBugMembers(string)"/>
///   reports at least one <c>included</c> member — a valid, test-passed bug release-train candidate. This is NOT a
///   generic "commit anything" backdoor; with zero eligible members the command REFUSES before any git mutation.</item>
///   <item>Stages EXACTLY the release-documentation surfaces the inspector requires — <c>README.md</c> and
///   <c>CHANGELOG.md</c> — that exist and are dirty. Never <c>git add -A</c>.</item>
///   <item>Nothing staged (the docs are already committed) → idempotent <see cref="DotiCommitStatus.NoChange"/>, no
///   empty commit.</item>
///   <item>Commits with <see cref="PrecommitGuard.SentinelEnvVar"/>=1 on the child <c>git commit</c>, reusing the same
///   transient-lock retry <c>DotiReconcileCommit</c> uses (032).</item>
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
        if (!IsInsideWorkTree(root))
        {
            return new BugReleaseDocCommitOutcome(
                DotiCommitStatus.NonGit, null, [], [],
                "target is not a git work tree; release-doc commit skipped.");
        }

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

        IReadOnlyList<string> stageable = DirtyReleaseDocSurfaces(root);
        if (stageable.Count == 0)
        {
            return NoChange(eligible);
        }

        ProcessRunResult add = Git(root, ["add", "--", .. stageable]);
        if (add.ExitCode != 0)
        {
            return new BugReleaseDocCommitOutcome(
                DotiCommitStatus.Failed, null, [], eligible,
                "git add failed: " + Prefer(add.StandardError, add.StandardOutput));
        }

        IReadOnlyList<string> staged = StagedPaths(root);
        if (staged.Count == 0)
        {
            return NoChange(eligible);
        }

        string message = BuildMessage(bugIds is { Count: > 0 } ? bugIds : eligible.Select(e => e.Feature).ToList());
        ProcessRunResult result = CommitWithRetry(root, message);
        if (result.ExitCode != 0)
        {
            return new BugReleaseDocCommitOutcome(
                DotiCommitStatus.Failed, null, staged, eligible,
                "git commit failed: " + Prefer(result.StandardError, result.StandardOutput));
        }

        return new BugReleaseDocCommitOutcome(DotiCommitStatus.Committed, HeadSha(root), staged, eligible, null);
    }

    // 032 D1(d) parity: the same transient-lock retry DotiReconcileCommit uses — up to two retries (three attempts)
    // with a brief backoff for a lock-contention signature; a non-transient failure returns immediately.
    private const int MaxCommitAttempts = 3;
    private static readonly TimeSpan CommitRetryBackoff = TimeSpan.FromMilliseconds(200);

    private static ProcessRunResult CommitWithRetry(string root, string message)
    {
        var env = new Dictionary<string, string> { [PrecommitGuard.SentinelEnvVar] = "1" };
        ProcessRunResult result = Git(root, ["commit", "-m", message], env);
        for (int attempt = 2; attempt <= MaxCommitAttempts && result.ExitCode != 0 && IsTransientLockFailure(result); attempt++)
        {
            Thread.Sleep(CommitRetryBackoff);
            result = Git(root, ["commit", "-m", message], env);
        }

        return result;
    }

    private static bool IsTransientLockFailure(ProcessRunResult result)
    {
        string combined = (result.StandardError + "\n" + result.StandardOutput);
        return combined.Contains("index.lock", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("cannot lock ref", StringComparison.OrdinalIgnoreCase)
            || (combined.Contains("unable to create", StringComparison.OrdinalIgnoreCase)
                && combined.Contains(".lock", StringComparison.OrdinalIgnoreCase));
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
                // porcelain format: "XY <path>" (or "XY <path>\0<orig>" for renames, not applicable here).
                .Select(line => line.Length > 3 ? line[3..].Trim().Replace('\\', '/') : string.Empty)
                .Where(path => path.Length > 0),
            StringComparer.OrdinalIgnoreCase);
        return ReleaseDocSurfaces.Where(dirty.Contains).ToList();
    }

    private static BugReleaseDocCommitOutcome NoChange(IReadOnlyList<CycleReleaseTrainFeature> eligible) =>
        new(DotiCommitStatus.NoChange, null, [], eligible, "no release-documentation change to commit.");

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
