using System.Diagnostics;
using Hx.Doti.Core.Bug;
using Hx.Runner.Core.Tools;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Doti.Tests;

/// <summary>
/// 034 (bug-only-release-doc-commit): the sanctioned, GATED commit for the release-documentation fix
/// (README.md/CHANGELOG.md) a bug-only release train's release-documentation gate demands. It is the only coded
/// commit path for that fix — no numbered feature cycle exists to own a workflow-transition commit, and
/// <c>hx doti bug</c> assess/fix/test never commit. The GATE (a release-ready bug member per
/// <see cref="BugCycleService.ReleaseReadyBugMembers(string)"/>) must find at least one <c>included</c> member before
/// ANY git mutation — this is never a generic "commit anything" backdoor. Stages EXACTLY the dirty subset of
/// README.md/CHANGELOG.md (never <c>git add -A</c>), is idempotent when nothing is staged, and the real insurance
/// pre-commit hook still blocks a bare, unrelated <c>git commit</c> (additive, not a hook weakening).
/// </summary>
public sealed class BugReleaseDocCommitTests
{
    [Fact]
    public void Happy_path_commits_exactly_readme_and_changelog_when_a_release_ready_bug_member_exists()
    {
        string dir = NewGitRepo();
        try
        {
            ArmInsuranceHook(dir);
            Write(dir, "README.md", "old readme");
            Write(dir, "CHANGELOG.md", "old changelog");
            Write(dir, "src/unrelated.cs", "unrelated production file");
            SanctionedCommit(dir, "init");
            string headBefore = Git(dir, "rev-parse", "HEAD");

            // A genuine, valid, test-passed bug release-train member (the real proof chain BugCycleService reads).
            ArmReleaseReadyBugMember(dir, "034-bug-only-release-doc-commit");

            // The release-documentation edit the operator makes: README/CHANGELOG mention the bug slug.
            Write(dir, "README.md", "old readme + 034-bug-only-release-doc-commit release note");
            Write(dir, "CHANGELOG.md", "old changelog + 034-bug-only-release-doc-commit");
            Write(dir, "src/unrelated.cs", "operator's unrelated in-flight edit"); // must NOT be swept in

            BugReleaseDocCommitOutcome outcome = BugReleaseDocCommit.Commit(dir, ["034-bug-only-release-doc-commit"]);

            Assert.Equal(DotiCommitStatus.Committed, outcome.Status);
            Assert.NotNull(outcome.Sha);
            Assert.NotEqual(headBefore, Git(dir, "rev-parse", "HEAD")); // HEAD advanced by exactly one commit
            Assert.Equal(headBefore, Git(dir, "rev-parse", "HEAD~1"));

            string committed = Git(dir, "log", "-1", "--name-only", "--format=").Replace('\\', '/');
            string[] committedFiles = committed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Assert.Equal(["CHANGELOG.md", "README.md"], committedFiles.OrderBy(f => f, StringComparer.Ordinal).ToArray());

            // The operator's unrelated production edit is untouched — still uncommitted.
            Assert.Contains("src/unrelated.cs", Git(dir, "status", "--porcelain").Replace('\\', '/'));
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void Fail_closed_refuses_before_any_git_mutation_when_no_release_ready_bug_member_exists()
    {
        string dir = NewGitRepo();
        try
        {
            ArmInsuranceHook(dir);
            Write(dir, "README.md", "old readme");
            Write(dir, "CHANGELOG.md", "old changelog");
            SanctionedCommit(dir, "init");
            string headBefore = Git(dir, "rev-parse", "HEAD");

            // NO bug record at all — the gate must find zero release-ready members.
            Write(dir, "README.md", "old readme + 999-nonexistent-bug");
            Write(dir, "CHANGELOG.md", "old changelog + 999-nonexistent-bug");

            BugReleaseDocCommitOutcome outcome = BugReleaseDocCommit.Commit(dir, ["999-nonexistent-bug"]);

            Assert.Equal(DotiCommitStatus.Refused, outcome.Status);
            Assert.Null(outcome.Sha);
            Assert.Empty(outcome.StagedPaths);
            Assert.Empty(outcome.EligibleBugMembers);
            Assert.NotNull(outcome.Reason);

            // No git mutation: HEAD unchanged, and the edit is still staged-uncommitted in the working tree.
            Assert.Equal(headBefore, Git(dir, "rev-parse", "HEAD"));
            string status = Git(dir, "status", "--porcelain").Replace('\\', '/');
            Assert.Contains("README.md", status);
            Assert.Contains("CHANGELOG.md", status);
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void An_unconfirmed_bug_does_not_satisfy_the_gate()
    {
        string dir = NewGitRepo();
        try
        {
            ArmInsuranceHook(dir);
            Write(dir, "README.md", "old readme");
            Write(dir, "CHANGELOG.md", "old changelog");
            SanctionedCommit(dir, "init");
            string headBefore = Git(dir, "rev-parse", "HEAD");

            // A bug dir exists but the proof chain is INCOMPLETE (assessment only, never fixed/tested) — mirrors a
            // bug still mid-mini-cycle, which must never justify a release-doc commit.
            string bugDir = Path.Combine(dir, ".doti", "bugs", "034-incomplete");
            Directory.CreateDirectory(bugDir);
            File.WriteAllText(Path.Combine(bugDir, "assessment.json"),
                """{"schemaVersion":1,"bugId":"034-incomplete","verdict":"confirmed","severity":"high","remediation":"r","summary":"s"}""");

            Write(dir, "README.md", "old readme + 034-incomplete");
            Write(dir, "CHANGELOG.md", "old changelog + 034-incomplete");

            BugReleaseDocCommitOutcome outcome = BugReleaseDocCommit.Commit(dir, ["034-incomplete"]);

            Assert.Equal(DotiCommitStatus.Refused, outcome.Status);
            Assert.Equal(headBefore, Git(dir, "rev-parse", "HEAD"));
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void The_real_insurance_hook_still_blocks_a_bare_commit_of_unrelated_content()
    {
        // Additive proof: this new sanctioned verb does not weaken the hook. A BARE `git commit` (no sentinel) of
        // unrelated staged content is still blocked by the same armed hook the sanctioned path gets past.
        string dir = NewGitRepo();
        try
        {
            ArmInsuranceHook(dir);
            Write(dir, "base.txt", "base");
            SanctionedCommit(dir, "init");
            string headBefore = Git(dir, "rev-parse", "HEAD");

            Write(dir, "unrelated.txt", "an unrelated bare-commit attempt");
            Git(dir, "add", "-A");

            var psi = new ProcessStartInfo("git")
            {
                WorkingDirectory = dir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("commit");
            psi.ArgumentList.Add("-m");
            psi.ArgumentList.Add("bare attempt, no sentinel");
            using Process process = Process.Start(psi)!;
            process.WaitForExit();

            Assert.NotEqual(0, process.ExitCode);
            Assert.Equal(headBefore, Git(dir, "rev-parse", "HEAD")); // no commit was made
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void Idempotent_no_op_when_nothing_is_staged()
    {
        string dir = NewGitRepo();
        try
        {
            ArmInsuranceHook(dir);
            Write(dir, "README.md", "readme already mentions 034-bug-only-release-doc-commit");
            Write(dir, "CHANGELOG.md", "changelog already mentions 034-bug-only-release-doc-commit");
            SanctionedCommit(dir, "init"); // docs are ALREADY committed and clean — nothing dirty to stage
            ArmReleaseReadyBugMember(dir, "034-bug-only-release-doc-commit");
            SanctionedCommit(dir, "add bug record"); // commit the bug record dir too so the tree is fully clean
            string headBefore = Git(dir, "rev-parse", "HEAD");

            BugReleaseDocCommitOutcome outcome = BugReleaseDocCommit.Commit(dir, ["034-bug-only-release-doc-commit"]);

            Assert.Equal(DotiCommitStatus.NoChange, outcome.Status);
            Assert.Null(outcome.Sha);
            Assert.Empty(outcome.StagedPaths);
            Assert.NotEmpty(outcome.EligibleBugMembers); // the gate DID pass; there was just nothing new to commit
            Assert.Equal(headBefore, Git(dir, "rev-parse", "HEAD")); // no empty commit
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void Non_git_target_skips_with_no_error_and_no_gate_evaluation()
    {
        string dir = NewTempDir(); // NOT a git repo
        try
        {
            Write(dir, "README.md", "old readme");
            Write(dir, "CHANGELOG.md", "old changelog");

            BugReleaseDocCommitOutcome outcome = BugReleaseDocCommit.Commit(dir, ["irrelevant"]);

            Assert.Equal(DotiCommitStatus.NonGit, outcome.Status);
            Assert.Null(outcome.Sha);
            Assert.Empty(outcome.EligibleBugMembers);
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void Only_dirty_release_doc_surfaces_are_staged_never_other_paths()
    {
        string dir = NewGitRepo();
        try
        {
            ArmInsuranceHook(dir);
            Write(dir, "README.md", "old readme");
            Write(dir, "CHANGELOG.md", "old changelog");
            SanctionedCommit(dir, "init");
            ArmReleaseReadyBugMember(dir, "034-bug-only-release-doc-commit");
            SanctionedCommit(dir, "add bug record");

            // Only README.md is dirty this time; CHANGELOG.md stays clean.
            Write(dir, "README.md", "old readme + 034-bug-only-release-doc-commit");

            BugReleaseDocCommitOutcome outcome = BugReleaseDocCommit.Commit(dir, ["034-bug-only-release-doc-commit"]);

            Assert.Equal(DotiCommitStatus.Committed, outcome.Status);
            Assert.Equal(["README.md"], outcome.StagedPaths);
            string committed = Git(dir, "log", "-1", "--name-only", "--format=").Replace('\\', '/');
            Assert.Contains("README.md", committed);
            Assert.DoesNotContain("CHANGELOG.md", committed);
        }
        finally { DeleteDir(dir); }
    }

    // 035 (A / BLOCKER): the release-doc commit must not sweep a PRE-STAGED operator file either — it shares the
    // SanctionedGitCommit helper with the reconcile path. (The happy-path test above uses a working-tree-only edit,
    // which the whole-index sweep never captured — the same gap the reconcile test had.)
    [Fact]
    public void Does_not_sweep_a_pre_staged_operator_file_into_the_release_doc_commit()
    {
        string dir = NewGitRepo();
        try
        {
            ArmInsuranceHook(dir);
            Write(dir, "README.md", "old readme");
            Write(dir, "CHANGELOG.md", "old changelog");
            SanctionedCommit(dir, "init");
            ArmReleaseReadyBugMember(dir, "034-bug-only-release-doc-commit");
            SanctionedCommit(dir, "add bug record");

            Write(dir, "README.md", "old readme + 034-bug-only-release-doc-commit");
            Write(dir, "CHANGELOG.md", "old changelog + 034-bug-only-release-doc-commit");
            Write(dir, "src/operator-staged.cs", "operator work, already staged");
            Git(dir, "add", "src/operator-staged.cs"); // operator pre-stages their own unrelated work

            BugReleaseDocCommitOutcome outcome = BugReleaseDocCommit.Commit(dir, ["034-bug-only-release-doc-commit"]);

            Assert.Equal(DotiCommitStatus.Committed, outcome.Status);
            string committed = Git(dir, "log", "-1", "--name-only", "--format=").Replace('\\', '/');
            Assert.Contains("README.md", committed);
            Assert.Contains("CHANGELOG.md", committed);
            Assert.DoesNotContain("operator-staged.cs", committed);           // NOT swept into the release-doc commit
            Assert.DoesNotContain("operator-staged.cs", outcome.StagedPaths);
            Assert.Contains("operator-staged.cs", Git(dir, "diff", "--cached", "--name-only").Replace('\\', '/'));
        }
        finally { DeleteDir(dir); }
    }

    // ---- fixtures ----

    // Write a genuine, complete, valid assess->fix->test proof chain for `bugId` — exactly the shape
    // BugCycleService.ReleaseReadyBugMembers reads (confirmed verdict, fix bound to the assessment hash, test bound
    // to the fix hash) — via the real BugCycleService writer so the test proves the ACTUAL gate, not a hand-rolled
    // JSON shape that happens to look right.
    private static void ArmReleaseReadyBugMember(string dir, string bugId)
    {
        BugCycleService.Assess(dir, new BugAssessment(BugCycleService.SchemaVersion, bugId, BugVerdict.Confirmed, "high", "root-cause remediation", "summary"));
        string assessmentSha = BugCycleService.CurrentAssessmentSha(dir, bugId)!;
        BugCycleService.Fix(dir, bugId, assessmentSha, "fix summary", []);
        BugCycleService.Test(dir, bugId, BugStageOutcome.Pass, "verified by re-running the reproduction");
    }

    // Arm the real insurance pre-commit hook so the tests PROVE the sanctioned commit (DOTI_SANCTIONED_COMMIT=1)
    // gets past it, and that a bare commit is still blocked. Mirrors DotiReconcileCommitTests' fixture, hardened
    // with ExecutableFileMode so a green Windows run does not mask a Linux/macOS CI failure (git SKIPS a
    // non-executable .sh hook on Unix, but always runs it on Windows regardless of the bit — 032's CI lesson).
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
        ExecutableFileMode.EnsureExecutable(hook);
    }

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
        string dir = Path.Combine(Path.GetTempPath(), "hx-bug-release-docs-" + Guid.NewGuid().ToString("n"));
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
