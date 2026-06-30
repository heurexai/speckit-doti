using System.Diagnostics;
using Hx.Runner.Cli;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>
/// 028 T011 / SC-004: the self-describing diff surface at the CLI/recovery seam. It runs <c>git diff</c> of the changed
/// prerequisite paths, lazily; with no changed paths it returns empty; with a null stamp commit it falls back to a
/// worktree diff and LABELS it; it never throws.
/// </summary>
public sealed class CycleRecoveryDiffTests
{
    [Fact]
    public void No_changed_paths_yields_empty_no_git_invoked()
    {
        Assert.Equal(string.Empty, CycleRecoveryDiff.Surface(".", stampedAtCommit: "HEAD", changedPaths: null));
        Assert.Equal(string.Empty, CycleRecoveryDiff.Surface(".", stampedAtCommit: "HEAD", changedPaths: []));
    }

    [Fact]
    public void Null_stamp_commit_falls_back_to_a_labeled_worktree_diff()
    {
        string dir = NewGitRepo();
        try
        {
            string rel = "docs/specs/001-test.md";
            string full = Path.Combine(dir, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, "# spec\nFR-001 do X\n");
            Git(dir, "add", "-A");
            Git(dir, "commit", "-q", "-m", "init");

            // An uncommitted edit so the worktree diff is non-empty.
            File.WriteAllText(full, "# spec\nFR-001 do X DIFFERENTLY\n");

            string diff = CycleRecoveryDiff.Surface(dir, stampedAtCommit: null, changedPaths: [rel]);

            Assert.Contains(CycleRecoveryDiff.WorktreeFallbackLabel, diff);
            Assert.Contains("DIFFERENTLY", diff);
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void A_non_repo_path_degrades_gracefully_never_throws()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-nondiff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // Not a git repo — git diff exits non-zero; the surface degrades to a path list, never throws.
            string diff = CycleRecoveryDiff.Surface(dir, stampedAtCommit: null, changedPaths: ["docs/specs/001.md"]);
            Assert.Contains("docs/specs/001.md", diff);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    private static void DeleteDir(string dir)
    {
        foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            try { File.SetAttributes(file, FileAttributes.Normal); }
            catch { /* best-effort */ }
        }

        try { Directory.Delete(dir, recursive: true); }
        catch (IOException) { /* temp dir; the OS reclaims it */ }
        catch (UnauthorizedAccessException) { /* temp dir; the OS reclaims it */ }
    }

    private static string NewGitRepo()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-diff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Git(dir, "init", "-q");
        Git(dir, "config", "user.email", "t@example.com");
        Git(dir, "config", "user.name", "Test");
        Git(dir, "config", "commit.gpgsign", "false");
        return dir;
    }

    private static string Git(string dir, params string[] args)
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

        using Process process = Process.Start(psi)!;
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output.Trim();
    }
}
