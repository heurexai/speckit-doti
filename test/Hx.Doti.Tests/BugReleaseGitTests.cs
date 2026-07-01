using System.Diagnostics;
using Hx.Doti.Core.Bug;
using Xunit;

namespace Hx.Doti.Tests;

/// <summary>
/// Bug 035 Fix J(b) + J(a)-deferred: <see cref="BugReleaseGit.IsReleased"/> treats a working-tree-dirty bug record as
/// unreleased (a re-opened shipped bug), and <see cref="BugReleaseGit.LatestReleaseTag"/> returns the highest-semver
/// <c>v*</c> tag REGARDLESS of reachability — because this project cuts the release tag on the main merge-commit
/// (a descendant of dev), a <c>--merged HEAD</c> constraint from dev would miss every recent release tag. Uses the
/// internal test-only seam via <c>InternalsVisibleTo</c> (granted to Hx.Doti.Tests).
/// </summary>
public sealed class BugReleaseGitTests
{
    [Fact]
    public void LatestReleaseTag_returns_the_highest_semver_tag_regardless_of_reachability()
    {
        // Bug 035 J(a) is DEFERRED: in this project's dev->main flow the release tag is cut on the main MERGE-commit,
        // a descendant of the dev working branch — so a `--merged HEAD` constraint from dev would see NONE of the
        // recent release tags. LatestReleaseTag therefore intentionally returns the highest-semver tag in the repo
        // regardless of branch/ancestry; IsReleased then verifies the fix commit is reachable FROM that tag.
        string dir = NewGitRepo();
        try
        {
            Write(dir, "base.txt", "base");
            Git(dir, "add", "-A");
            Git(dir, "commit", "-q", "-m", "init");
            Git(dir, "tag", "v1.0.0");

            // A higher-semver tag on a branch NOT merged into HEAD (mirrors a release tag cut on the main merge-commit,
            // which is not reachable from dev's HEAD).
            Git(dir, "checkout", "-q", "-b", "release-cut");
            Write(dir, "cut.txt", "release cut");
            Git(dir, "add", "-A");
            Git(dir, "commit", "-q", "-m", "release merge");
            Git(dir, "tag", "v2.0.0");
            Git(dir, "checkout", "-q", "-");

            Assert.Equal("v2.0.0", BugReleaseGit.LatestReleaseTag(dir)); // highest semver, even off HEAD's own history
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void LatestReleaseTag_returns_the_newest_tag_reachable_from_head_in_a_normal_linear_repo()
    {
        string dir = NewGitRepo();
        try
        {
            Write(dir, "base.txt", "base");
            Git(dir, "add", "-A");
            Git(dir, "commit", "-q", "-m", "init");
            Git(dir, "tag", "v1.0.0");

            Write(dir, "base.txt", "base v2");
            Git(dir, "add", "-A");
            Git(dir, "commit", "-q", "-m", "second release");
            Git(dir, "tag", "v2.0.0");

            string? latest = BugReleaseGit.LatestReleaseTag(dir);

            Assert.Equal("v2.0.0", latest); // both tags are on HEAD's own linear history — a normal repo is unaffected
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void IsReleased_treats_a_working_tree_dirty_bug_record_as_unreleased()
    {
        string dir = NewGitRepo();
        try
        {
            string bugDir = Path.Combine(dir, ".doti", "bugs", "b");
            Directory.CreateDirectory(bugDir);
            Write(dir, ".doti/bugs/b/assessment.json", "{}");
            Git(dir, "add", "-A");
            Git(dir, "commit", "-q", "-m", "shipped fix");
            Git(dir, "tag", "v1.0.0");

            // Re-open the bug: dirty the record in the working tree without committing.
            Write(dir, ".doti/bugs/b/assessment.json", "{\"reopened\":true}");

            bool released = BugReleaseGit.IsReleased(dir, bugDir, "v1.0.0");

            // Even though the last COMMITTED touch of this dir is tag-reachable, the dirty working tree means the
            // bug was re-opened and must not be reported as already shipped.
            Assert.False(released);
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void IsReleased_reports_true_for_a_clean_committed_record_reachable_from_the_tag()
    {
        string dir = NewGitRepo();
        try
        {
            string bugDir = Path.Combine(dir, ".doti", "bugs", "b");
            Directory.CreateDirectory(bugDir);
            Write(dir, ".doti/bugs/b/assessment.json", "{}");
            Git(dir, "add", "-A");
            Git(dir, "commit", "-q", "-m", "shipped fix");
            Git(dir, "tag", "v1.0.0");

            bool released = BugReleaseGit.IsReleased(dir, bugDir, "v1.0.0");

            Assert.True(released); // clean tree, fix commit reachable from the tag -> already shipped
        }
        finally { DeleteDir(dir); }
    }

    // ---- fixtures (mirrors BugReleaseDocCommitTests) ----

    private static string Write(string dir, string relative, string content)
    {
        string full = Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    private static string NewGitRepo()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-bug-release-git-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        Git(dir, "init", "-q");
        Git(dir, "config", "user.email", "t@example.com");
        Git(dir, "config", "user.name", "Test");
        Git(dir, "config", "commit.gpgsign", "false");
        return dir;
    }

    private static void DeleteDir(string dir)
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
