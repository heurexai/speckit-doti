using Hx.Cycle.Core;
using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;

namespace Hx.Doti.Core;

/// <summary>
/// 035 (commit-hardening): the SHARED mechanics behind every coded Doti sanctioned commit
/// (<see cref="DotiReconcileCommit"/> + <see cref="Hx.Doti.Core.Bug.BugReleaseDocCommit"/>), so the two paths can
/// never drift again (the drift that left one path pathspec-scoped while the other swept the whole index, and left
/// <c>doti install</c>'s failed commit swallowed). Three invariants it guarantees for both callers:
/// <list type="number">
///   <item><b>No whole-index sweep.</b> It commits with an EXPLICIT pathspec of only the paths it staged, so an
///   operator's PRE-STAGED unrelated work is never folded into a Doti commit (a bare <c>git commit</c> commits the
///   entire index; this never does).</item>
///   <item><b>No hard-fail on a single unstageable candidate.</b> A gitignored path, or an untracked "removed" orphan
///   that would be a <c>pathspec did not match</c>, is SKIPPED (it is not committable) rather than failing the whole
///   sanctioned commit; the happy path is one batch <c>git add</c>, only a batch failure falls back to per-path adds.</item>
///   <item><b>Sentinel + transient-lock retry on BOTH add and commit</b> (the 032 retry previously wrapped only the
///   commit), under <see cref="PrecommitGuard.SentinelEnvVar"/> so the insurance hook permits it.</item>
/// </list>
/// Callers own their own pre-checks (a <c>--no-commit</c> opt-out, a release-readiness gate), the <c>--</c>candidate
/// preparation, the commit message, and the mapping of <see cref="Result"/> onto their outcome contract.
/// </summary>
public static class SanctionedGitCommit
{
    /// <summary>The outcome of the shared stage+commit: a <see cref="DotiCommitStatus"/>, the HEAD sha on a commit,
    /// the EXACT paths committed (never the whole index), and a reason for a non-committed status.</summary>
    public readonly record struct Result(string Status, string? Sha, IReadOnlyList<string> Staged, string? Reason);

    private const int MaxAttempts = 3;
    private static readonly TimeSpan RetryBackoff = TimeSpan.FromMilliseconds(200);

    /// <summary>True when <paramref name="root"/> is inside a git work tree. Exposed so a caller can order its own
    /// pre-checks (e.g. a fail-closed gate) around the git-ness check exactly as it needs.</summary>
    public static bool IsInsideWorkTree(string root)
    {
        ProcessRunResult result = Git(root, ["rev-parse", "--is-inside-work-tree"]);
        return result.ExitCode == 0 && result.StandardOutput.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Stage the stageable subset of <paramref name="candidates"/> and commit EXACTLY those paths (pathspec-scoped)
    /// with <paramref name="message"/>. <paramref name="candidates"/> must already be normalized by the caller
    /// (repo-relative, forward-slash, de-duped, no <c>.new</c>). Returns <see cref="DotiCommitStatus.NoChange"/> when
    /// none of the caller's candidates are staged (idempotent; never an empty commit), and
    /// <see cref="DotiCommitStatus.Failed"/> only on a real <c>git commit</c> failure — surfaced, never swallowed.
    /// </summary>
    public static Result Commit(string root, IReadOnlyList<string> candidates, string message)
    {
        if (candidates.Count == 0)
        {
            return new Result(DotiCommitStatus.NoChange, null, [], "nothing to commit.");
        }

        Stage(root, candidates);

        // The commit pathspec is EXACTLY the caller's candidates that git actually staged — never the whole index, so
        // any file the operator (or a co-running process) had already staged stays in the index, uncommitted.
        var mine = new HashSet<string>(candidates, StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<string> staged = StagedPaths(root).Where(mine.Contains).ToList();
        if (staged.Count == 0)
        {
            return new Result(DotiCommitStatus.NoChange, null, [], "nothing to commit.");
        }

        var env = Sentinel();
        ProcessRunResult commit = GitWithLockRetry(root, ["commit", "-m", message, "--", .. staged], env);
        if (commit.ExitCode != 0)
        {
            return new Result(DotiCommitStatus.Failed, null, staged,
                "git commit failed: " + Prefer(commit.StandardError, commit.StandardOutput));
        }

        return new Result(DotiCommitStatus.Committed, HeadSha(root), staged, null);
    }

    // Stage the candidates, tolerating any single path git refuses. The happy path is one batch `git add`; only on a
    // batch failure (a residual gitignored path → non-atomic partial add; or an untracked `removed` orphan → an atomic
    // `pathspec did not match`) does it fall back to per-path staging that SKIPS the offender instead of failing the
    // whole sanctioned commit. Both the batch and the per-path adds carry the sentinel + transient-lock retry.
    private static void Stage(string root, IReadOnlyList<string> candidates)
    {
        var env = Sentinel();
        ProcessRunResult batch = GitWithLockRetry(root, ["add", "--", .. candidates], env);
        if (batch.ExitCode == 0)
        {
            return;
        }

        foreach (string candidate in candidates)
        {
            _ = GitWithLockRetry(root, ["add", "--", candidate], env);
        }
    }

    private static Dictionary<string, string> Sentinel() => new() { [PrecommitGuard.SentinelEnvVar] = "1" };

    // 032 D1(d): a sanctioned commit can race a concurrent git lock holder (a worktree teardown's `.git/worktrees`
    // bookkeeping, an editor's git plugin, a co-running `git gc`) and fail with a transient lock a short retry
    // resolves. Up to two retries (three attempts) with a brief backoff; a NON-transient failure returns immediately.
    private static ProcessRunResult GitWithLockRetry(
        string root, IReadOnlyList<string> args, IReadOnlyDictionary<string, string>? env = null)
    {
        ProcessRunResult result = Git(root, args, env);
        for (int attempt = 2; attempt <= MaxAttempts && result.ExitCode != 0 && IsTransientLockFailure(result); attempt++)
        {
            Thread.Sleep(RetryBackoff);
            result = Git(root, args, env);
        }

        return result;
    }

    private static bool IsTransientLockFailure(ProcessRunResult result)
    {
        string combined = result.StandardError + "\n" + result.StandardOutput;
        return combined.Contains("index.lock", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("cannot lock ref", StringComparison.OrdinalIgnoreCase)
            || (combined.Contains("unable to create", StringComparison.OrdinalIgnoreCase)
                && combined.Contains(".lock", StringComparison.OrdinalIgnoreCase));
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

    private static string? HeadSha(string root)
    {
        ProcessRunResult result = Git(root, ["rev-parse", "HEAD"]);
        return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput)
            ? result.StandardOutput.Trim()
            : null;
    }

    private static ProcessRunResult Git(
        string root, IReadOnlyList<string> args, IReadOnlyDictionary<string, string>? env = null)
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
