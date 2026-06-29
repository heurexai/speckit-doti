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
        string worktreePath = Path.Combine(Path.GetTempPath(), "hx-doti-worktree-" + Guid.NewGuid().ToString("n"));
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
        try
        {
            TryGit(SourceRepoRoot, "worktree", "remove", "--force", WorktreePath);
        }
        catch
        {
            // best-effort cleanup; a leaked temp worktree is reclaimed by `git worktree prune` / the OS temp sweep.
        }

        if (Directory.Exists(WorktreePath))
        {
            try { Directory.Delete(WorktreePath, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
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
