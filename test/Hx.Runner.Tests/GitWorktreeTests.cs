using Hx.Runner.Core.Git;
using Hx.Runner.Core.Process;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>022 T040 (FR-013/014): the worktree primitive creates at HEAD, captures the change set produced inside,
/// applies it back to the source; an un-applied (dry-run) capture leaves the source untouched; a non-git target
/// fails hard.</summary>
public sealed class GitWorktreeTests
{
    [Fact]
    public void Captures_and_applies_back_a_change_set()
    {
        string repo = InitRepo();
        try
        {
            IReadOnlyList<GitWorktreeChange> changes;
            using (GitWorktree worktree = GitWorktree.Create(repo))
            {
                File.WriteAllText(Path.Combine(worktree.WorktreePath, "added.txt"), "new");
                File.WriteAllText(Path.Combine(worktree.WorktreePath, "seed.txt"), "changed");

                changes = worktree.CaptureChanges();
                Assert.Contains(changes, c => c.Path == "added.txt" && c.Kind == GitWorktreeChangeKind.Added);
                Assert.Contains(changes, c => c.Path == "seed.txt" && c.Kind == GitWorktreeChangeKind.Modified);

                worktree.ApplyBack(changes);
            }

            Assert.Equal("new", File.ReadAllText(Path.Combine(repo, "added.txt")));
            Assert.Equal("changed", File.ReadAllText(Path.Combine(repo, "seed.txt")));
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    [Fact]
    public void Dry_run_leaves_source_untouched_when_not_applied()
    {
        string repo = InitRepo();
        try
        {
            using (GitWorktree worktree = GitWorktree.Create(repo))
            {
                File.WriteAllText(Path.Combine(worktree.WorktreePath, "added.txt"), "new");
                IReadOnlyList<GitWorktreeChange> changes = worktree.CaptureChanges();
                Assert.Contains(changes, c => c.Path == "added.txt");
                // No ApplyBack — the source must be untouched (the --dry-run contract).
            }

            Assert.False(File.Exists(Path.Combine(repo, "added.txt")));
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    [Fact]
    public void Ensure_git_available_throws_on_non_git_directory()
    {
        string dir = NewTempDir();
        try
        {
            Assert.Throws<GitUnavailableException>(() => GitWorktree.EnsureGitAvailable(dir));
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    private static string InitRepo()
    {
        string dir = NewTempDir();
        Git(dir, "init", "-q");
        Git(dir, "config", "user.email", "t@example.com");
        Git(dir, "config", "user.name", "Test");
        Git(dir, "config", "commit.gpgsign", "false");
        File.WriteAllText(Path.Combine(dir, "seed.txt"), "seed");
        Git(dir, "add", "-A");
        Git(dir, "commit", "-q", "-m", "seed");
        return dir;
    }

    private static void Git(string dir, params string[] args)
    {
        ProcessRunResult r = ProcessRunner.Run(new ToolCommand("git", args, dir));
        if (r.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {r.StandardError.Trim()}");
        }
    }

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-worktree-test-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void ForceDelete(string dir)
    {
        if (!Directory.Exists(dir))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            try { File.SetAttributes(file, FileAttributes.Normal); }
            catch { /* best-effort */ }
        }

        try { Directory.Delete(dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
