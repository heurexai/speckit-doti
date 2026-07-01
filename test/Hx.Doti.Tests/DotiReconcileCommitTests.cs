using System.Diagnostics;
using Hx.Doti.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Doti.Tests;

/// <summary>
/// 031 T006/T014 (FR-007/008/009/010, D4, SC-007/008/010/011): the self-owned sanctioned reconcile commit. It stages
/// EXACTLY the touched managed paths (never <c>git add -A</c> — an operator's unrelated work is untouched), excludes
/// every <c>.new</c> merge-helper, makes ONE commit past the insurance pre-commit hook (it sets
/// <c>DOTI_SANCTIONED_COMMIT=1</c> on the child <c>git commit</c>), is idempotent (no staged change → no commit), and
/// skips a non-git target with no error. <c>--no-commit</c> (commit=false) leaves the changes uncommitted.
/// </summary>
public sealed class DotiReconcileCommitTests
{
    [Fact]
    public void Commits_exactly_the_touched_paths_and_leaves_operator_work_uncommitted()
    {
        string dir = NewGitRepo();
        try
        {
            ArmInsuranceHook(dir);
            Write(dir, "base.txt", "base");
            SanctionedCommit(dir, "init");

            // Two managed-asset paths the reconcile "touched", plus an operator's unrelated working-tree change.
            Write(dir, ".doti/core/skills.json", "rendered skills");
            Write(dir, ".claude/skills/01-doti-specify/SKILL.md", "rendered skill");
            Write(dir, "operator-unrelated.cs", "operator work in flight");

            DotiReconcileCommitOutcome outcome = DotiReconcileCommit.Commit(
                dir,
                [".doti/core/skills.json", ".claude/skills/01-doti-specify/SKILL.md"],
                beforeVersion: "1.0.0", afterVersion: "2.0.0", prunedPaths: [], commit: true);

            Assert.Equal(DotiCommitStatus.Committed, outcome.Status);
            Assert.NotNull(outcome.Sha);
            // Exactly the two managed paths were committed — never the operator's unrelated file.
            string committed = Git(dir, "show", "--name-only", "--format=", "HEAD").Replace('\\', '/');
            Assert.Contains(".doti/core/skills.json", committed);
            Assert.Contains(".claude/skills/01-doti-specify/SKILL.md", committed);
            Assert.DoesNotContain("operator-unrelated.cs", committed);
            // SC-007: the operator's unrelated change is still present + uncommitted.
            Assert.Contains("operator-unrelated.cs", Git(dir, "status", "--porcelain"));
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void Never_stages_a_new_merge_helper_even_if_passed()
    {
        string dir = NewGitRepo();
        try
        {
            ArmInsuranceHook(dir);
            Write(dir, "base.txt", "base");
            SanctionedCommit(dir, "init");

            Write(dir, ".doti/core/skills.json", "rendered");
            Write(dir, ".doti/core/skills.json.new", "bundled merge-helper");

            // The .new is defensively excluded by Commit even if the caller passes it.
            DotiReconcileCommitOutcome outcome = DotiReconcileCommit.Commit(
                dir, [".doti/core/skills.json", ".doti/core/skills.json.new"],
                beforeVersion: null, afterVersion: "2.0.0", prunedPaths: [], commit: true);

            Assert.Equal(DotiCommitStatus.Committed, outcome.Status);
            Assert.DoesNotContain(outcome.StagedPaths, p => p.EndsWith(".new", StringComparison.OrdinalIgnoreCase));
            string committed = Git(dir, "show", "--name-only", "--format=", "HEAD").Replace('\\', '/');
            Assert.DoesNotContain(".new", committed);
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void No_staged_change_makes_no_commit_idempotent()
    {
        string dir = NewGitRepo();
        try
        {
            ArmInsuranceHook(dir);
            Write(dir, ".doti/core/skills.json", "rendered");
            SanctionedCommit(dir, "init"); // the touched path is ALREADY committed and unchanged
            string headBefore = Git(dir, "rev-parse", "HEAD");

            DotiReconcileCommitOutcome outcome = DotiReconcileCommit.Commit(
                dir, [".doti/core/skills.json"], beforeVersion: "2.0.0", afterVersion: "2.0.0", prunedPaths: [], commit: true);

            Assert.Equal(DotiCommitStatus.NoChange, outcome.Status);
            Assert.Null(outcome.Sha);
            Assert.Equal(headBefore, Git(dir, "rev-parse", "HEAD")); // no new commit
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void No_commit_flag_leaves_changes_uncommitted()
    {
        string dir = NewGitRepo();
        try
        {
            ArmInsuranceHook(dir);
            Write(dir, "base.txt", "base");
            SanctionedCommit(dir, "init");
            Write(dir, ".doti/core/skills.json", "rendered");
            string headBefore = Git(dir, "rev-parse", "HEAD");

            DotiReconcileCommitOutcome outcome = DotiReconcileCommit.Commit(
                dir, [".doti/core/skills.json"], beforeVersion: "1.0.0", afterVersion: "2.0.0", prunedPaths: [], commit: false);

            Assert.Equal(DotiCommitStatus.Disabled, outcome.Status);
            Assert.Equal(headBefore, Git(dir, "rev-parse", "HEAD"));
            Assert.Contains(".doti/core/skills.json", Git(dir, "status", "--porcelain", "--untracked-files=all").Replace('\\', '/'));
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void Non_git_target_skips_with_no_error()
    {
        string dir = NewTempDir(); // NOT a git repo
        try
        {
            Write(dir, ".doti/core/skills.json", "rendered");

            DotiReconcileCommitOutcome outcome = DotiReconcileCommit.Commit(
                dir, [".doti/core/skills.json"], beforeVersion: null, afterVersion: "2.0.0", prunedPaths: [], commit: true);

            Assert.Equal(DotiCommitStatus.NonGit, outcome.Status);
            Assert.Null(outcome.Sha);
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void Auto_message_names_the_version_move_and_pruned_orphans()
    {
        string message = DotiReconcileCommit.BuildMessage("1.0.0", "2.0.0",
            [".claude/skills/04-doti-tasks/SKILL.md"]);

        Assert.StartsWith("chore(doti): reconcile Doti assets to 2.0.0", message);
        Assert.Contains("Doti payload 1.0.0 -> 2.0.0.", message);
        Assert.Contains("Pruned 1 orphaned managed asset(s)", message);
        Assert.Contains(".claude/skills/04-doti-tasks/SKILL.md", message);
    }

    // 035 (A / BLOCKER): the regression that was MISSING — the prior test used a working-tree-ONLY operator file
    // (never git add-ed), which the whole-index sweep never captured, so it proved the wrong property. Here the
    // operator has ALREADY STAGED an unrelated file; a bare `git commit` would fold it into the reconcile commit.
    [Fact]
    public void Does_not_sweep_a_pre_staged_operator_file_into_the_reconcile_commit()
    {
        string dir = NewGitRepo();
        try
        {
            ArmInsuranceHook(dir);
            Write(dir, "base.txt", "base");
            SanctionedCommit(dir, "init");

            Write(dir, ".doti/core/skills.json", "rendered skills");
            Write(dir, "operator-staged.cs", "operator work, already staged");
            Git(dir, "add", "operator-staged.cs"); // operator pre-stages their own unrelated work

            DotiReconcileCommitOutcome outcome = DotiReconcileCommit.Commit(
                dir, [".doti/core/skills.json"], beforeVersion: "1.0.0", afterVersion: "2.0.0", prunedPaths: [], commit: true);

            Assert.Equal(DotiCommitStatus.Committed, outcome.Status);
            string committed = Git(dir, "show", "--name-only", "--format=", "HEAD").Replace('\\', '/');
            Assert.Contains(".doti/core/skills.json", committed);
            Assert.DoesNotContain("operator-staged.cs", committed);          // NOT swept into the reconcile commit
            Assert.DoesNotContain("operator-staged.cs", outcome.StagedPaths); // and not reported as committed
            // The operator's staged work is preserved in the index, not lost.
            Assert.Contains("operator-staged.cs", Git(dir, "diff", "--cached", "--name-only").Replace('\\', '/'));
        }
        finally { DeleteDir(dir); }
    }

    // 035 (B / BLOCKER): the ergon failure — a consumer repo gitignores the materialized templates via a
    // TRAILING-SLASH pattern (`.doti/templates/`). The gitignored dir in the touched set must be SKIPPED, not fail
    // the whole sanctioned commit ("paths are ignored, use -f").
    [Fact]
    public void A_gitignored_candidate_is_skipped_and_does_not_fail_the_commit()
    {
        string dir = NewGitRepo();
        try
        {
            ArmInsuranceHook(dir);
            Write(dir, "base.txt", "base");
            Write(dir, ".gitignore", ".doti/templates/\n");
            SanctionedCommit(dir, "init");

            Write(dir, ".doti/core/skills.json", "rendered skills");
            Write(dir, ".doti/templates/commands/doti-bug.md", "materialized, gitignored runtime state");

            DotiReconcileCommitOutcome outcome = DotiReconcileCommit.Commit(
                dir, [".doti/core/skills.json", ".doti/templates"],
                beforeVersion: "1.0.0", afterVersion: "2.0.0", prunedPaths: [], commit: true);

            Assert.Equal(DotiCommitStatus.Committed, outcome.Status);
            string committed = Git(dir, "show", "--name-only", "--format=", "HEAD").Replace('\\', '/');
            Assert.Contains(".doti/core/skills.json", committed);
            Assert.DoesNotContain(".doti/templates", committed);
        }
        finally { DeleteDir(dir); }
    }

    // 035 (HIGH): a `removed` candidate that was NEVER git-tracked and is absent on disk (a renamed-skill orphan a
    // prior --no-commit / failed run left) → `git add` emits "pathspec did not match" (exit 128) and, unhandled,
    // fails the whole commit. It must be SKIPPED.
    [Fact]
    public void An_untracked_orphan_candidate_is_skipped_and_does_not_fail_the_commit()
    {
        string dir = NewGitRepo();
        try
        {
            ArmInsuranceHook(dir);
            Write(dir, "base.txt", "base");
            SanctionedCommit(dir, "init");

            Write(dir, ".doti/core/skills.json", "rendered skills");

            DotiReconcileCommitOutcome outcome = DotiReconcileCommit.Commit(
                dir, [".doti/core/skills.json", ".claude/skills/old-renamed-orphan/SKILL.md"],
                beforeVersion: "1.0.0", afterVersion: "2.0.0", prunedPaths: [], commit: true);

            Assert.Equal(DotiCommitStatus.Committed, outcome.Status);
            string committed = Git(dir, "show", "--name-only", "--format=", "HEAD").Replace('\\', '/');
            Assert.Contains(".doti/core/skills.json", committed);
            Assert.DoesNotContain("old-renamed-orphan", committed);
        }
        finally { DeleteDir(dir); }
    }

    // Arm the real insurance pre-commit hook so the test PROVES the sanctioned commit (DOTI_SANCTIONED_COMMIT=1) gets
    // past it — a bare commit would be blocked. The hook mirrors the shipped stub: sanctioned env → exit 0, else fail.
    private static void ArmInsuranceHook(string dir)
    {
        string hooks = Path.Combine(dir, ".git", "hooks");
        Directory.CreateDirectory(hooks);
        string hook = Path.Combine(hooks, "pre-commit");
        File.WriteAllText(hook,
            "#!/bin/sh\n" +
            "if [ \"$DOTI_SANCTIONED_COMMIT\" = \"1\" ]; then exit 0; fi\n" +
            "echo 'bare commit blocked by insurance hook' 1>&2\n" +
            "exit 1\n");
    }

    // A sanctioned commit for fixture setup (so the armed hook lets the init commit through).
    private static void SanctionedCommit(string dir, string message)
    {
        Git(dir, "add", "-A");
        GitWithEnv(dir, ["commit", "-q", "-m", message], "DOTI_SANCTIONED_COMMIT", "1");
    }

    private static string Write(string dir, string relative, string content)
    {
        string full = Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-doti-commit-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string NewGitRepo()
    {
        string dir = NewTempDir();
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
