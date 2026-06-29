using System.Diagnostics;
using Hx.Doti.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Doti.Tests;

/// <summary>022 T050 (FR-016/017/018): batch-update a mixed-version root — outdated repos updated, current skipped,
/// summary counts correct, and one repo's failure (a non-git Doti dir) does not abort the others (fail-soft).</summary>
public sealed class DotiBatchUpdaterTests
{
    [Fact]
    public void Updates_outdated_skips_current_and_is_fail_soft()
    {
        string source1 = DotiVersionTestSupport.NewSource("1.0.0");
        string source2 = DotiVersionTestSupport.NewSource("2.0.0");
        string root = DotiVersionTestSupport.NewTempDir();
        try
        {
            string outdated = GitRepoWithDoti(Path.Combine(root, "outdated"), source1);   // committed at 1.0.0
            string current = GitRepoWithDoti(Path.Combine(root, "current"), source2);      // committed at 2.0.0
            string broken = Path.Combine(root, "broken");                                  // Doti dir, NOT a git repo
            Directory.CreateDirectory(broken);
            RepoPayloadStore.Write(broken, "1.0.0", "1.0.0");

            DotiUpdateAllSummary summary = DotiBatchUpdater.Run(
                source2, root, DotiAgentTarget.All, "2.0.0", force: false, dryRun: false);

            Assert.Equal(3, summary.Total);
            Assert.Equal(1, summary.Updated);         // outdated → updated
            Assert.Equal(1, summary.AlreadyCurrent);  // current → skipped
            Assert.Equal(1, summary.Failed);          // broken (no git) → git-required → failed

            Assert.Contains(summary.Repos, r => r.RepoPath == Path.GetFullPath(outdated)
                && r.Status == DotiUpdateStatus.Updated && r.BeforeVersion == "1.0.0" && r.AfterVersion == "2.0.0");
            Assert.Contains(summary.Repos, r => r.RepoPath == Path.GetFullPath(current)
                && r.Status == DotiUpdateStatus.AlreadyCurrent);
            Assert.Contains(summary.Repos, r => r.Status == DotiUpdateStatus.GitRequired);
        }
        finally
        {
            DotiVersionTestSupport.ForceDelete(source1);
            DotiVersionTestSupport.ForceDelete(source2);
            DotiVersionTestSupport.ForceDelete(root);
        }
    }

    [Fact]
    public void Dry_run_reports_moves_without_applying()
    {
        string source1 = DotiVersionTestSupport.NewSource("1.0.0");
        string source2 = DotiVersionTestSupport.NewSource("2.0.0");
        string root = DotiVersionTestSupport.NewTempDir();
        try
        {
            string outdated = GitRepoWithDoti(Path.Combine(root, "outdated"), source1);

            DotiUpdateAllSummary summary = DotiBatchUpdater.Run(
                source2, root, DotiAgentTarget.All, "2.0.0", force: false, dryRun: true);

            Assert.True(summary.DryRun);
            Assert.Contains(summary.Repos, r => r.Status == DotiUpdateStatus.DryRun);
            // The real repo's recorded version is unchanged by a dry-run preview.
            Assert.Equal("1.0.0", RepoPayloadStore.ReadPayloadVersion(outdated));
        }
        finally
        {
            DotiVersionTestSupport.ForceDelete(source1);
            DotiVersionTestSupport.ForceDelete(source2);
            DotiVersionTestSupport.ForceDelete(root);
        }
    }

    private static string GitRepoWithDoti(string repo, string source)
    {
        Directory.CreateDirectory(repo);
        Git(repo, "init", "-q");
        Git(repo, "config", "user.email", "t@example.com");
        Git(repo, "config", "user.name", "Test");
        Git(repo, "config", "commit.gpgsign", "false");
        DotiInstaller.Install(source, repo, DotiAgentTarget.All, Path.GetFileName(repo));
        Git(repo, "add", "-A");
        Git(repo, "commit", "-q", "-m", "doti");
        return repo;
    }

    private static void Git(string dir, params string[] args)
    {
        var psi = new ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
        foreach (string a in args)
        {
            psi.ArgumentList.Add(a);
        }

        using var p = Process.Start(psi)!;
        string err = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {err.Trim()}");
        }
    }
}
