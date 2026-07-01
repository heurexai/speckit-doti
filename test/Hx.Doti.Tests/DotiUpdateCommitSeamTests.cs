using System.Diagnostics;
using Hx.Doti.Core;
using Hx.Runner.Core.Tools;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Doti.Tests;

/// <summary>
/// 031 T014 (FR-007/008/009/010, SC-007/SC-009/SC-010/SC-011/SC-012): end-to-end seam over the real worktree update
/// path. After a successful reconcile in a git repo the command makes ONE sanctioned commit of exactly the touched
/// managed paths (the operator's unrelated work stays uncommitted); <c>--no-commit</c> leaves the reconcile
/// uncommitted; a no-change re-run makes no commit; <c>update-all</c> applies the same per-repo. The reconcile runs in
/// a worktree and applies back to the REAL repo, so the commit must land on the real repo's HEAD.
/// </summary>
public sealed class DotiUpdateCommitSeamTests
{
    [Fact]
    public void Update_commits_the_reconcile_and_leaves_operator_work_uncommitted()
    {
        string source = DotiVersionTestSupport.NewSource("2.0.0");
        string repo = NewInstalledGitRepo(source, "1.0.0");
        try
        {
            // An operator's unrelated in-flight change in the working tree.
            File.WriteAllText(Path.Combine(repo, "operator-unrelated.txt"), "in flight");

            DotiUpdateOutcome outcome = DotiWorktreeUpdate.Run(
                source, repo, DotiAgentTarget.All, "2.0.0", force: false, dryRun: false,
                commit: true, sourceOrigin: "bundled");

            Assert.Equal(DotiUpdateStatus.Updated, outcome.Status);
            Assert.NotNull(outcome.Commit);
            Assert.Equal(DotiCommitStatus.Committed, outcome.Commit!.Status);

            // SC-008: the committed set is the touched managed paths — the .doti payload stamp landed, the operator
            // file did NOT.
            string committed = Git(repo, "show", "--name-only", "--format=", "HEAD").Replace('\\', '/');
            Assert.Contains(".doti/payload.json", committed);
            Assert.DoesNotContain("operator-unrelated.txt", committed);
            // SC-007: the operator's unrelated change is still present + uncommitted.
            Assert.Contains("operator-unrelated.txt", Git(repo, "status", "--porcelain"));
            // The repo moved to the new version.
            Assert.Equal("2.0.0", RepoPayloadStore.ReadPayloadVersion(repo));
        }
        finally { Cleanup(source, repo); }
    }

    [Fact]
    public void Update_with_no_commit_leaves_the_reconcile_uncommitted()
    {
        string source = DotiVersionTestSupport.NewSource("2.0.0");
        string repo = NewInstalledGitRepo(source, "1.0.0");
        try
        {
            string headBefore = Git(repo, "rev-parse", "HEAD");

            DotiUpdateOutcome outcome = DotiWorktreeUpdate.Run(
                source, repo, DotiAgentTarget.All, "2.0.0", force: false, dryRun: false,
                commit: false, sourceOrigin: "bundled");

            Assert.Equal(DotiUpdateStatus.Updated, outcome.Status);
            Assert.Equal(DotiCommitStatus.Disabled, outcome.Commit!.Status);
            Assert.Equal(headBefore, Git(repo, "rev-parse", "HEAD")); // no commit
            // The reconcile is applied to the working tree (payload stamped) but uncommitted.
            Assert.Equal("2.0.0", RepoPayloadStore.ReadPayloadVersion(repo));
            Assert.NotEqual(string.Empty, Git(repo, "status", "--porcelain"));
        }
        finally { Cleanup(source, repo); }
    }

    [Fact]
    public void A_no_change_update_makes_no_commit()
    {
        string source = DotiVersionTestSupport.NewSource("2.0.0");
        string repo = NewInstalledGitRepo(source, "2.0.0"); // already at the source version
        try
        {
            string headBefore = Git(repo, "rev-parse", "HEAD");

            DotiUpdateOutcome outcome = DotiWorktreeUpdate.Run(
                source, repo, DotiAgentTarget.All, "2.0.0", force: false, dryRun: false,
                commit: true, sourceOrigin: "bundled");

            Assert.Equal(DotiUpdateStatus.AlreadyCurrent, outcome.Status);
            Assert.Equal(DotiCommitStatus.NoChange, outcome.Commit!.Status);
            Assert.Equal(headBefore, Git(repo, "rev-parse", "HEAD")); // SC-011: no empty commit
        }
        finally { Cleanup(source, repo); }
    }

    /// <summary>
    /// 032 D1(b): the self-commit must run AFTER the worktree's `.git/worktrees` registration is fully torn down —
    /// the worktree-leak/commit-race this bug fixes. Proven by a pre-commit hook that captures `git worktree list
    /// --porcelain` to a side file the MOMENT the sanctioned `git commit` actually runs; on the OLD order (commit
    /// inside the worktree's still-registered lifetime) that capture would show TWO worktrees (the main repo +
    /// the still-registered worktree); on the FIXED order it shows exactly ONE (the main repo only).
    /// </summary>
    [Fact]
    public void Worktree_is_no_longer_registered_when_the_self_commit_runs()
    {
        string source = DotiVersionTestSupport.NewSource("2.0.0");
        string repo = NewInstalledGitRepo(source, "1.0.0");
        string captureFile = Path.Combine(Path.GetTempPath(), "hx-worktree-capture-" + Guid.NewGuid().ToString("n") + ".txt");
        try
        {
            ArmWorktreeObservingHook(repo, captureFile);

            DotiUpdateOutcome outcome = DotiWorktreeUpdate.Run(
                source, repo, DotiAgentTarget.All, "2.0.0", force: false, dryRun: false,
                commit: true, sourceOrigin: "bundled");

            Assert.Equal(DotiUpdateStatus.Updated, outcome.Status);
            Assert.Equal(DotiCommitStatus.Committed, outcome.Commit!.Status);
            Assert.True(File.Exists(captureFile), "the pre-commit hook must have run and captured the worktree listing");

            string captured = File.ReadAllText(captureFile);
            int worktreeEntries = captured
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Count(line => line.StartsWith("worktree ", StringComparison.Ordinal));
            Assert.Equal(1, worktreeEntries); // ONLY the main repo — the run's own worktree was already torn down.
        }
        finally
        {
            Cleanup(source, repo);
            if (File.Exists(captureFile))
            {
                File.Delete(captureFile);
            }
        }
    }

    /// <summary>
    /// 032 D1(d): a TRANSIENT lock failure (stderr carries the "index.lock" signature
    /// <see cref="DotiReconcileCommit"/> recognizes) on the FIRST commit attempt is retried; the SECOND attempt
    /// succeeds (the hook only fails once), proving the retry-then-succeed path — not just "retries exist" but that
    /// a transient failure does not become a permanently reported <see cref="DotiCommitStatus.Failed"/>.
    /// </summary>
    [Fact]
    public void A_transient_lock_failure_is_retried_and_the_commit_ultimately_succeeds()
    {
        string source = DotiVersionTestSupport.NewSource("2.0.0");
        string repo = NewInstalledGitRepo(source, "1.0.0");
        string attemptCounterFile = Path.Combine(Path.GetTempPath(), "hx-lock-attempts-" + Guid.NewGuid().ToString("n") + ".txt");
        try
        {
            ArmFlakyLockHook(repo, attemptCounterFile);

            DotiUpdateOutcome outcome = DotiWorktreeUpdate.Run(
                source, repo, DotiAgentTarget.All, "2.0.0", force: false, dryRun: false,
                commit: true, sourceOrigin: "bundled");

            Assert.Equal(DotiUpdateStatus.Updated, outcome.Status);
            Assert.Equal(DotiCommitStatus.Committed, outcome.Commit!.Status);
            Assert.NotNull(outcome.Commit.Sha);
            // The hook ran (and failed) at least once before the retry succeeded.
            Assert.True(File.Exists(attemptCounterFile));
            int attempts = int.Parse(File.ReadAllText(attemptCounterFile).Trim());
            Assert.True(attempts >= 2, $"expected at least 2 commit attempts (one failure + one success), saw {attempts}");
        }
        finally
        {
            Cleanup(source, repo);
            if (File.Exists(attemptCounterFile))
            {
                File.Delete(attemptCounterFile);
            }
        }
    }

    [Fact]
    public void Update_all_applies_the_same_commit_behavior_per_repo()
    {
        // SC-012: update-all applies the source/prune/commit behavior per repo with a per-repo summary.
        string source = DotiVersionTestSupport.NewSource("2.0.0");
        string root = DotiVersionTestSupport.NewTempDir();
        string repoA = NewInstalledGitRepoUnder(root, source, "1.0.0", "repoA");
        string repoB = NewInstalledGitRepoUnder(root, source, "1.0.0", "repoB");
        try
        {
            DotiUpdateAllSummary summary = DotiBatchUpdater.Run(
                source, root, DotiAgentTarget.All, "2.0.0", force: false, dryRun: false,
                commit: true, sourceOrigin: "bundled");

            Assert.Equal(2, summary.Total);
            Assert.Equal(2, summary.Updated);
            Assert.All(summary.Repos, r =>
            {
                Assert.NotNull(r.Commit);
                Assert.Equal(DotiCommitStatus.Committed, r.Commit!.Status);
            });
            Assert.Equal("2.0.0", RepoPayloadStore.ReadPayloadVersion(repoA));
            Assert.Equal("2.0.0", RepoPayloadStore.ReadPayloadVersion(repoB));
        }
        finally
        {
            DotiVersionTestSupport.ForceDelete(source);
            ForceDeleteGit(root);
        }
    }

    // A git repo with Doti installed at a given version + the insurance hook armed (so the test proves the
    // sanctioned auto-commit gets past it) + a sealed baseline commit.
    private static string NewInstalledGitRepo(string source, string installedVersion)
    {
        string repo = DotiVersionTestSupport.NewTempDir();
        return InstallAndSeal(repo, source, installedVersion);
    }

    private static string NewInstalledGitRepoUnder(string root, string source, string installedVersion, string name)
    {
        string repo = Path.Combine(root, name);
        Directory.CreateDirectory(repo);
        return InstallAndSeal(repo, source, installedVersion);
    }

    private static string InstallAndSeal(string repo, string source, string installedVersion)
    {
        Git(repo, "init", "-q");
        Git(repo, "config", "user.email", "t@example.com");
        Git(repo, "config", "user.name", "Test");
        Git(repo, "config", "commit.gpgsign", "false");
        ArmInsuranceHook(repo);
        // A stable solution file so ProjectNameResolver resolves the SAME project name whether Install runs against the
        // real repo or a worktree copy (else integration.json's name flips to the worktree dir name → spurious churn).
        // Real Doti repos always have a solution file, so this models reality (a same-version update is a true no-op).
        File.WriteAllText(Path.Combine(repo, "fixture.slnx"), "<Solution />");

        // Install the OLDER payload so the later update genuinely moves the version. Seed with the SAME project name
        // the worktree update will resolve (ProjectNameResolver over fixture.slnx) so integration.json does not flip
        // names between the seed and the re-render — the only difference under test is the payload version.
        string oldSource = DotiVersionTestSupport.NewSource(installedVersion);
        try
        {
            DotiInstaller.Install(oldSource, repo, DotiAgentTarget.All, ProjectNameResolver.Resolve(repo, null));
        }
        finally { DotiVersionTestSupport.ForceDelete(oldSource); }

        Git(repo, "add", "-A");
        GitWithEnv(repo, ["commit", "-q", "-m", "init doti"], "DOTI_SANCTIONED_COMMIT", "1");
        return repo;
    }

    private static void ArmInsuranceHook(string repo)
    {
        string hooks = Path.Combine(repo, ".git", "hooks");
        Directory.CreateDirectory(hooks);
        string hookPath = Path.Combine(hooks, "pre-commit");
        File.WriteAllText(hookPath,
            "#!/bin/sh\nif [ \"$DOTI_SANCTIONED_COMMIT\" = \"1\" ]; then exit 0; fi\nexit 1\n");
        ExecutableFileMode.EnsureExecutable(hookPath);
    }

    // 032 D1(b): a pre-commit hook that captures `git worktree list --porcelain` to `captureFile` at the EXACT
    // moment the sanctioned `git commit` runs (the hook fires after staging, before the commit object is written) —
    // proving (or disproving) that the run's own worktree registration is already gone by then. Forward-slash the
    // path for POSIX `sh` portability (the hook shell runs under Git Bash on Windows).
    private static void ArmWorktreeObservingHook(string repo, string captureFile)
    {
        string hooks = Path.Combine(repo, ".git", "hooks");
        Directory.CreateDirectory(hooks);
        string hookPath = Path.Combine(hooks, "pre-commit");
        string posixCapturePath = captureFile.Replace('\\', '/');
        File.WriteAllText(hookPath,
            "#!/bin/sh\n"
            + $"git worktree list --porcelain > \"{posixCapturePath}\" 2>&1\n"
            + "if [ \"$DOTI_SANCTIONED_COMMIT\" = \"1\" ]; then exit 0; fi\n"
            + "exit 1\n");
        ExecutableFileMode.EnsureExecutable(hookPath);
    }

    // 032 D1(d): a pre-commit hook that fails with the "index.lock" transient-lock signature on its FIRST invocation
    // (incrementing a counter file each time it runs) and succeeds on every subsequent one — modeling a real,
    // momentary `.git/index.lock` collision that clears itself, which DotiReconcileCommit.Commit's retry-with-backoff
    // is meant to ride out. Still gated on the sanctioned env var so an UNSANCTIONED commit is never let through.
    private static void ArmFlakyLockHook(string repo, string attemptCounterFile)
    {
        string hooks = Path.Combine(repo, ".git", "hooks");
        Directory.CreateDirectory(hooks);
        string hookPath = Path.Combine(hooks, "pre-commit");
        string posixCounterPath = attemptCounterFile.Replace('\\', '/');
        File.WriteAllText(hookPath,
            "#!/bin/sh\n"
            + $"COUNTER=\"{posixCounterPath}\"\n"
            + "if [ -f \"$COUNTER\" ]; then N=$(cat \"$COUNTER\"); else N=0; fi\n"
            + "N=$((N + 1))\n"
            + "echo \"$N\" > \"$COUNTER\"\n"
            + "if [ \"$DOTI_SANCTIONED_COMMIT\" != \"1\" ]; then exit 1; fi\n"
            + "if [ \"$N\" -eq 1 ]; then\n"
            + "  echo \"fatal: Unable to create '$(pwd)/.git/index.lock': File exists.\" >&2\n"
            + "  exit 1\n"
            + "fi\n"
            + "exit 0\n");
        ExecutableFileMode.EnsureExecutable(hookPath);
    }

    private static void Cleanup(string source, string repo)
    {
        DotiVersionTestSupport.ForceDelete(source);
        ForceDeleteGit(repo);
    }

    private static void ForceDeleteGit(string dir)
    {
        if (!Directory.Exists(dir))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            try { File.SetAttributes(file, FileAttributes.Normal); } catch { /* best-effort */ }
        }

        try { Directory.Delete(dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string Git(string dir, params string[] args) => GitWithEnv(dir, args, null, null);

    private static string GitWithEnv(string dir, string[] args, string? envKey, string? envValue)
    {
        var psi = new ProcessStartInfo("git")
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

        if (envKey is not null)
        {
            psi.Environment[envKey] = envValue!;
        }

        using Process process = Process.Start(psi)!;
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output.Trim();
    }
}
