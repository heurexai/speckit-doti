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

    /// <summary>
    /// 032 D1(a): a PRIOR run's leaked-but-still-VALIDLY-registered worktree (the owning repo and the
    /// `.git/worktrees/&lt;name&gt;` registration both still exist; nobody ever disposed it) is removed cleanly via
    /// the normal `git worktree remove --force` path run against its TRUE owning repo. Backdated past the 10-minute
    /// `MinLeakAge` floor so the sweep does not skip it as "too young to be a prior leak."
    /// </summary>
    [Fact]
    public void PruneLeakedTemps_removes_a_valid_orphaned_worktree_and_its_registration()
    {
        string owner = InitRepo();
        string orphan = CreateBackdatedLeakedWorktree(owner);
        try
        {
            GitWorktreePruneResult result = GitWorktree.PruneLeakedTemps(owner);

            Assert.False(Directory.Exists(orphan), "the orphaned worktree directory should be gone after the sweep");
            Assert.Contains(result.Entries, e => e.Path == orphan && e.Removed);
            // The registration is gone too (not just the directory) — `worktree list` no longer carries it.
            string listing = GitOutput(owner, "worktree", "list", "--porcelain");
            Assert.DoesNotContain(orphan.Replace('\\', '/'), listing.Replace('\\', '/'));
        }
        finally
        {
            ForceDelete(owner);
            ForceDelete(orphan);
        }
    }

    /// <summary>
    /// 032 D1(a): a HUSK — the worktree directory survives but its `.git/worktrees/&lt;name&gt;` registration was
    /// deleted out-of-band (git reports exit 128 "not a git repository" from inside it) — is detected via the
    /// unresolvable-ownership signal and removed by a direct recursive directory delete, since `git worktree remove`
    /// can never recognize it. Also backdated past `MinLeakAge`.
    /// </summary>
    [Fact]
    public void PruneLeakedTemps_removes_a_husk_whose_registration_was_deleted_out_of_band()
    {
        string owner = InitRepo();
        string husk = CreateBackdatedLeakedWorktree(owner);
        try
        {
            // Simulate the husk: delete the registration directly, leaving the worktree directory behind.
            string registrationName = Path.GetFileName(husk);
            string registrationDir = Path.Combine(owner, ".git", "worktrees", registrationName);
            Assert.True(Directory.Exists(registrationDir), "fixture setup: the registration must exist before deleting it");
            Directory.Delete(registrationDir, recursive: true);

            GitWorktreePruneResult result = GitWorktree.PruneLeakedTemps(owner);

            Assert.False(Directory.Exists(husk), "the husk directory should be deleted directly after the sweep");
            Assert.Contains(result.Entries, e => e.Path == husk && e.Removed);
        }
        finally
        {
            ForceDelete(owner);
            ForceDelete(husk);
        }
    }

    /// <summary>
    /// 032 D1(a): a candidate younger than `MinLeakAge` is left COMPLETELY alone — not even attempted — because it is
    /// far more likely to be a concurrently-running invocation's own brand-new, still-in-flight worktree than a
    /// genuine leak. This is the fix for the cross-test/cross-process race the naive "sweep everything matching the
    /// prefix" version of this method introduced (it could rip out another process's live worktree mid-operation).
    /// </summary>
    [Fact]
    public void PruneLeakedTemps_leaves_a_freshly_created_worktree_untouched()
    {
        string owner = InitRepo();
        using GitWorktree fresh = GitWorktree.Create(owner);
        try
        {
            GitWorktreePruneResult result = GitWorktree.PruneLeakedTemps(owner);

            Assert.True(Directory.Exists(fresh.WorktreePath), "a freshly created worktree must never be swept");
            Assert.DoesNotContain(result.Entries, e => e.Path == fresh.WorktreePath);
        }
        finally
        {
            ForceDelete(owner);
        }
    }

    // A worktree of `owner`, backdated past MinLeakAge so PruneLeakedTemps considers it eligible. The directory's
    // own creation time is what the sweep reads (Directory.GetCreationTimeUtc), so backdating the worktree dir
    // itself (not the owner repo) is what matters. Deliberately NOT disposed (no `using`/Dispose call) — that is the
    // whole point of the fixture: model a PRIOR process that created a worktree and crashed/exited before tearing it
    // down, leaving it registered and on-disk for the sweep to find.
    private static string CreateBackdatedLeakedWorktree(string owner)
    {
        GitWorktree worktree = GitWorktree.Create(owner);
        string path = worktree.WorktreePath;
        DateTime backdated = DateTime.UtcNow.AddMinutes(-30);
        Directory.SetCreationTimeUtc(path, backdated);
        Directory.SetLastWriteTimeUtc(path, backdated);
        return path;
    }

    private static string GitOutput(string dir, params string[] args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("git")
        {
            WorkingDirectory = dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(psi)!;
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
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
