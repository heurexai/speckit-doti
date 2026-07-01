using Hx.Runner.Core.Process;

namespace Hx.Runner.Core.Git;

/// <summary>One file's change inside a worktree, relative to the source repo root.</summary>
public sealed record GitWorktreeChange(string Path, GitWorktreeChangeKind Kind);

public enum GitWorktreeChangeKind
{
    Added,
    Modified,
    Deleted,
}

/// <summary>
/// 032 D1(a): one cleanup attempt's outcome for a single leaked temp worktree path, surfaced instead of the prior
/// bare <c>catch{}</c> swallow so a stuck leak is diagnosable rather than silently retried forever.
/// </summary>
public sealed record GitWorktreePruneEntry(string Path, bool Removed, string? Reason);

/// <summary>032 D1(a): the aggregate result of one <see cref="GitWorktree.PruneLeakedTemps"/> sweep.</summary>
public sealed record GitWorktreePruneResult(IReadOnlyList<GitWorktreePruneEntry> Entries)
{
    public int RemovedCount => Entries.Count(e => e.Removed);

    public bool AnyFailed => Entries.Any(e => !e.Removed);
}

/// <summary>
/// 022 T041 (FR-013/014): an isolated git worktree at the source repo's HEAD for safe, previewable mutation
/// (operator decision Q2). A mutating action runs inside the worktree; <see cref="CaptureChanges"/> reports the
/// resulting change set (<c>git status --porcelain</c> — tracked edits + untracked-but-not-ignored new files, so
/// gitignored runtime state is never applied); <see cref="ApplyBack"/> copies that set into the real repo (a
/// <c>--dry-run</c> simply skips this). Git is REQUIRED — <see cref="EnsureGitAvailable"/> fails hard (no silent
/// fallback) when the git binary is missing or the target is not a git work tree. Disposing removes the worktree.
/// </summary>
public sealed class GitWorktree : IDisposable
{
    private GitWorktree(string sourceRepoRoot, string worktreePath)
    {
        SourceRepoRoot = sourceRepoRoot;
        WorktreePath = worktreePath;
    }

    public string SourceRepoRoot { get; }

    public string WorktreePath { get; }

    // 032 D1(a): the exact temp-dir name prefix `Create` uses (below). `PruneLeakedTemps` sweeps ONLY this tool's own
    // prefix — never a blind sweep of Path.GetTempPath() — so an operator's unrelated temp content is never touched.
    private const string TempDirPrefix = "hx-doti-worktree-";

    // 032 D1(a): a candidate younger than this is NEVER swept, regardless of live/husk status — it is far more
    // likely to be a concurrently-running `hx doti update`'s own brand-new worktree (mid-reconcile) than a genuine
    // leak from a crashed/abandoned PRIOR process. A worktree-scoped reconcile completes in low seconds; ten minutes
    // is a wide safety margin before something is treated as "prior" rather than "in flight right now."
    private static readonly TimeSpan MinLeakAge = TimeSpan.FromMinutes(10);

    /// <summary>Fail hard (FR-014) when git is unavailable or <paramref name="repoRoot"/> is not a git work tree.</summary>
    public static void EnsureGitAvailable(string repoRoot)
    {
        ProcessRunResult version = TryGit(repoRoot, "--version");
        if (version.ExitCode != 0)
        {
            throw new GitUnavailableException(
                "git is required for `hx doti update` but the git executable was not found or failed to run.");
        }

        ProcessRunResult inside = TryGit(repoRoot, "rev-parse", "--is-inside-work-tree");
        if (inside.ExitCode != 0 || !inside.StandardOutput.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            throw new GitUnavailableException(
                $"`hx doti update` requires a git repository, but '{repoRoot}' is not inside a git work tree.");
        }
    }

    /// <summary>Add a detached worktree at the source repo's HEAD in a fresh temp directory.</summary>
    public static GitWorktree Create(string repoRoot)
    {
        EnsureGitAvailable(repoRoot);
        string root = Path.GetFullPath(repoRoot);
        string worktreePath = Path.Combine(Path.GetTempPath(), TempDirPrefix + Guid.NewGuid().ToString("n"));
        ProcessRunResult add = TryGit(root, "worktree", "add", "--detach", "--quiet", worktreePath, "HEAD");
        if (add.ExitCode != 0)
        {
            throw new GitUnavailableException(
                "git worktree add failed: " + Prefer(add.StandardError, add.StandardOutput));
        }

        return new GitWorktree(root, worktreePath);
    }

    /// <summary>
    /// The change set produced inside the worktree, relative to its HEAD: tracked modifications/deletions and
    /// untracked (non-ignored) additions. Gitignored files (runtime state) are excluded by <c>git status</c>.
    /// </summary>
    public IReadOnlyList<GitWorktreeChange> CaptureChanges()
    {
        ProcessRunResult status = TryGit(WorktreePath, "status", "--porcelain", "--untracked-files=all");
        if (status.ExitCode != 0)
        {
            throw new GitUnavailableException(
                "git status failed in the worktree: " + Prefer(status.StandardError, status.StandardOutput));
        }

        var changes = new List<GitWorktreeChange>();
        foreach (string raw in status.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = raw.TrimEnd('\r');
            if (line.Length < 4)
            {
                continue;
            }

            string code = line[..2];
            string path = UnquotePath(line[3..]);
            // A rename ("R  old -> new") is reported old->new; take the destination as an addition.
            int arrow = path.IndexOf(" -> ", StringComparison.Ordinal);
            if (arrow >= 0)
            {
                path = path[(arrow + 4)..];
            }

            changes.Add(new GitWorktreeChange(NormalizeSlashes(path), ClassifyStatus(code)));
        }

        return changes;
    }

    /// <summary>Copy each change from the worktree into the source repo (added/modified copied, deleted removed).</summary>
    public void ApplyBack(IReadOnlyList<GitWorktreeChange> changes)
    {
        foreach (GitWorktreeChange change in changes)
        {
            string relative = change.Path.Replace('/', Path.DirectorySeparatorChar);
            string from = Path.Combine(WorktreePath, relative);
            string to = Path.Combine(SourceRepoRoot, relative);

            if (change.Kind == GitWorktreeChangeKind.Deleted)
            {
                if (File.Exists(to))
                {
                    File.Delete(to);
                }

                continue;
            }

            if (!File.Exists(from))
            {
                continue; // a directory entry or already-removed transient; nothing to copy.
            }

            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(from, to, overwrite: true);
        }
    }

    public void Dispose()
    {
        RemoveWorktreeOrHusk(WorktreePath);
        TryGit(SourceRepoRoot, "worktree", "prune");
    }

    /// <summary>
    /// 032 D1(a): sweep <see cref="Path.GetTempPath"/> for THIS TOOL'S OWN prior <c>hx-doti-worktree-*</c> entries
    /// (the <see cref="TempDirPrefix"/> <see cref="Create"/> uses) and remove each one — never a blind prune of the
    /// temp dir, never an operator-created worktree, and never anything younger than <see cref="MinLeakAge"/> (which
    /// excludes a concurrently-running invocation's own brand-new, still-in-flight worktree — see that field's
    /// remarks). Each remaining candidate is handled by <see cref="RemoveWorktreeOrHusk"/>, which distinguishes a
    /// genuine husk (directory present, registration deleted out-of-band — git exits 128 "not a git
    /// repository"/"not a working tree") from a still-LIVE registered worktree (possibly owned by a different repo):
    /// only the former is ever directory-deleted; the latter is left alone rather than risk corrupting someone
    /// else's in-flight worktree. A single trailing <c>git worktree prune</c> drops any now-stale registrations.
    /// Every attempt is INSTRUMENTED (returned), never a bare swallow — call before <see cref="Create"/> so a prior
    /// run's leak cannot collide with this run's commit.
    /// </summary>
    public static GitWorktreePruneResult PruneLeakedTemps(string repoRoot)
    {
        string root = Path.GetFullPath(repoRoot);
        string tempRoot = Path.GetTempPath();
        var entries = new List<GitWorktreePruneEntry>();
        if (!Directory.Exists(tempRoot))
        {
            return new GitWorktreePruneResult(entries);
        }

        DateTime now = DateTime.UtcNow;
        foreach (string candidate in Directory.EnumerateDirectories(tempRoot, TempDirPrefix + "*"))
        {
            if (now - Directory.GetCreationTimeUtc(candidate) < MinLeakAge)
            {
                continue; // too young to be a "prior" leak — almost certainly a concurrently in-flight worktree.
            }

            entries.Add(RemoveWorktreeOrHusk(candidate));
        }

        if (entries.Count > 0)
        {
            TryGit(root, "worktree", "prune");
        }

        return new GitWorktreePruneResult(entries);
    }

    /// <summary>
    /// Remove one candidate worktree path SAFELY. The CALLER's own repo is deliberately NEVER assumed to be the
    /// path's actual owning repo, because <see cref="PruneLeakedTemps"/> sweeps system-wide and a candidate may
    /// legitimately be registered against a DIFFERENT repo (or still be a live, in-use worktree of a
    /// concurrently-running process) — force-removing or directory-deleting a live worktree out from under its owner
    /// would corrupt that other process's work.
    /// <list type="number">
    /// <item>Ask the candidate itself who owns it: <c>git -C &lt;path&gt; rev-parse --git-common-dir</c>. This FAILS
    /// (exit 128, "not a git repository") exactly when the path has no valid worktree registration anywhere — the
    /// husk case (directory present, <c>.git/worktrees/&lt;name&gt;</c> registration deleted out-of-band) — so a
    /// failure here is the safe, unambiguous signal to delete the directory directly.</item>
    /// <item>When ownership resolves, the candidate is a LIVE, currently-registered worktree (possibly of another
    /// repo or process). Run <c>git worktree remove --force</c> FROM ITS TRUE OWNING REPO — the only way the remove
    /// is git-recognized as valid.</item>
    /// <item>If that still fails (e.g. genuinely concurrent use), the candidate is left ALONE — never
    /// directory-deleted — and the failure is instrumented rather than silently forcing a corrupting delete.</item>
    /// </list>
    /// </summary>
    private static GitWorktreePruneEntry RemoveWorktreeOrHusk(string worktreePath)
    {
        if (!Directory.Exists(worktreePath))
        {
            return new GitWorktreePruneEntry(worktreePath, Removed: true, Reason: null);
        }

        ProcessRunResult owner = TryGit(worktreePath, "rev-parse", "--git-common-dir");
        if (owner.ExitCode != 0)
        {
            // No resolvable registration anywhere — a genuine husk (or never a worktree). Safe to delete directly.
            return DeleteHuskDirectory(worktreePath, "git worktree remove failed; husk directory deleted directly");
        }

        // A LIVE, currently-registered worktree. Resolve its TRUE owning repo (the parent of --git-common-dir, which
        // is that repo's `.git`) and remove it from there — repoRoot is never assumed to be the owner.
        string ownerGitDir = owner.StandardOutput.Trim();
        string ownerRepoRoot = Path.GetFullPath(Path.Combine(worktreePath, ownerGitDir, "..")).TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        ProcessRunResult remove = TryGit(ownerRepoRoot, "worktree", "remove", "--force", worktreePath);
        if (remove.ExitCode == 0 && !Directory.Exists(worktreePath))
        {
            return new GitWorktreePruneEntry(worktreePath, Removed: true, Reason: null);
        }

        if (!Directory.Exists(worktreePath))
        {
            return new GitWorktreePruneEntry(worktreePath, Removed: true, Reason: null);
        }

        // Still registered and still present after a remove attempt from its OWN owning repo: it is genuinely in use
        // right now (a concurrent process, a lock, etc.) — leave it alone. Force-deleting here would corrupt the
        // owner's in-flight work; report the failure instead of masking it as a successful prune.
        return new GitWorktreePruneEntry(worktreePath, Removed: false,
            Reason: "git worktree remove failed (" + Prefer(remove.StandardError, remove.StandardOutput)
                + ") and the worktree is still live (owned by " + ownerRepoRoot + "); left in place");
    }

    private static GitWorktreePruneEntry DeleteHuskDirectory(string worktreePath, string reasonOnSuccess)
    {
        try
        {
            Directory.Delete(worktreePath, recursive: true);
            return new GitWorktreePruneEntry(worktreePath, Removed: true, Reason: reasonOnSuccess);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new GitWorktreePruneEntry(worktreePath, Removed: false,
                Reason: "husk directory could not be deleted: " + ex.Message);
        }
    }

    private static GitWorktreeChangeKind ClassifyStatus(string code) =>
        code.Contains('D', StringComparison.Ordinal) && !code.Contains('A', StringComparison.Ordinal)
            ? GitWorktreeChangeKind.Deleted
            : code.Contains('?', StringComparison.Ordinal) || code.Contains('A', StringComparison.Ordinal)
                ? GitWorktreeChangeKind.Added
                : GitWorktreeChangeKind.Modified;

    private static string UnquotePath(string path) =>
        path.Length >= 2 && path[0] == '"' && path[^1] == '"' ? path[1..^1] : path;

    private static string NormalizeSlashes(string path) => path.Replace('\\', '/');

    private static ProcessRunResult TryGit(string workingDirectory, params string[] args)
    {
        try
        {
            return ProcessRunner.Run(new ToolCommand("git", args, workingDirectory));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new ProcessRunResult(127, string.Empty, ex.Message);
        }
    }

    private static string Prefer(string primary, string fallback) =>
        string.IsNullOrWhiteSpace(primary) ? fallback.Trim() : primary.Trim();
}

/// <summary>022 (FR-014): git is required but unavailable, or the target is not a git work tree. Fail closed.</summary>
public sealed class GitUnavailableException(string message) : Exception(message);
