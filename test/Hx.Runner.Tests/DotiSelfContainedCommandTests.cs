using System.Diagnostics;
using System.Text.Json;
using Hx.Cli.Kernel;
using Hx.Doti.Core;
using Hx.Runner.Cli;
using Hx.Runner.Core.Tools;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>
/// 031 T014 (FR-001/002/011, SC-001): the CLI seam for the self-contained update commands. The source defaults to the
/// running tool's bundled payload and falls back to the working-directory dev walk — in-test the test bin has no
/// <c>.doti</c> beside it, so it resolves the repo root via the dev walk (proving the resolution does not regress).
/// The resolved source origin is threaded into the envelope, and <c>--no-commit</c> reaches the command. A non-Doti
/// <c>--repo</c> is reported (Validation), never mutated.
/// </summary>
public sealed class DotiSelfContainedCommandTests
{
    private static readonly CliMeta Meta = new("hx-runner", "0.0.0-test");

    [Fact]
    public void Update_requires_an_explicit_repo()
    {
        CliResult r = RunnerCommands.DotiUpdate(Meta, repo: null, "codex", force: false, dryRun: false, noCommit: false);
        Assert.False(r.Ok);
        Assert.Equal((int)ExitClass.Usage, r.ExitCode);
    }

    [Fact]
    public void Update_all_requires_an_explicit_root()
    {
        CliResult r = RunnerCommands.DotiUpdateAll(Meta, root: null, "codex", force: false, dryRun: false, noCommit: false);
        Assert.False(r.Ok);
        Assert.Equal((int)ExitClass.Usage, r.ExitCode);
    }

    [Fact]
    public void Update_on_a_non_doti_repo_is_reported_with_source_origin_and_not_mutated()
    {
        string target = Path.Combine(Path.GetTempPath(), "hx-runner-doti-update-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(target);
        try
        {
            // The source resolves via the dev-cwd fallback (the test runs under the repo). A non-Doti --repo is
            // reported NotARepo — proving the resolution threaded through and the command reached the reconciler.
            CliResult r = RunnerCommands.DotiUpdate(Meta, target, "codex", force: false, dryRun: false, noCommit: false);

            Assert.False(r.Ok);
            Assert.Equal((int)ExitClass.Validation, r.ExitCode);
            Assert.Equal(ErrorCodes.Validation_DotiNotARepo, Assert.Single(r.Errors).Code);
            // FR-011: the resolved source origin is in the envelope (dev-cwd in-test).
            using JsonDocument doc = JsonDocument.Parse(r.Data!.ToJsonString());
            Assert.Equal(DotiSourceOrigin.DevCwd, doc.RootElement.GetProperty("sourceOrigin").GetString());
            Assert.False(Directory.Exists(Path.Combine(target, ".doti")), "a non-Doti dir must not be mutated");
        }
        finally
        {
            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }
        }
    }

    /// <summary>
    /// 032 D1(c): a reconcile that SUCCEEDS but whose self-owned commit FAILS (a hostile pre-commit hook that exits
    /// 1 even with the sanctioned env var — modeling a real-world transient lock or a misconfigured local hook) must
    /// surface as <c>ok:false</c> / a non-zero exit / <see cref="CliOutcome.Failed"/> at the top level — never the
    /// silent ok:true the missing routing arm produced before this fix (DotiCommitStatus.Failed is documented
    /// "reported, never silently swallowed").
    /// </summary>
    [Fact]
    public void Update_reports_failure_when_the_reconcile_succeeds_but_the_commit_fails()
    {
        string repo = NewRepoPendingAnUpdateWithHostileCommitHook();
        try
        {
            CliResult r = RunnerCommands.DotiUpdate(Meta, repo, "codex", force: false, dryRun: false, noCommit: false);

            Assert.False(r.Ok, "a failed self-commit must surface as ok:false, not be silently swallowed");
            Assert.NotEqual(0, r.ExitCode);
            Assert.Equal((int)ExitClass.Integrity, r.ExitCode);
            Assert.Equal(CliOutcome.Failed, r.Outcome);
            Assert.Equal(ErrorCodes.Integrity_DotiUpdateFailed, Assert.Single(r.Errors).Code);
        }
        finally
        {
            ForceDeleteGit(repo);
        }
    }

    /// <summary>032 D1(c): the same failure-surfacing for the batch command — one repo's commit failure must be
    /// counted as a batch failure (DotiBatchUpdater.Run's Failed count), surfacing ok:false at the top level.</summary>
    [Fact]
    public void Update_all_reports_failure_when_a_repos_commit_fails()
    {
        string root = Path.Combine(Path.GetTempPath(), "hx-runner-doti-update-all-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(root);
        string repo = Path.Combine(root, "repo-with-hostile-hook");
        try
        {
            SeedRepoPendingAnUpdateWithHostileCommitHook(repo);

            CliResult r = RunnerCommands.DotiUpdateAll(Meta, root, "codex", force: false, dryRun: false, noCommit: false);

            Assert.False(r.Ok, "a per-repo commit failure must surface as ok:false for the whole batch");
            Assert.NotEqual(0, r.ExitCode);
            Assert.Equal((int)ExitClass.Integrity, r.ExitCode);
            Assert.Equal(CliOutcome.Failed, r.Outcome);
        }
        finally
        {
            ForceDeleteGit(root);
        }
    }

    // A real git repo with an EMPTY `.doti` dir (so it passes the NotARepo check but has NO pre-existing managed
    // content/baseline at all) sealed with a baseline commit, then armed with a HOSTILE pre-commit hook (exits 1
    // unconditionally — even with the sanctioned env var). Because nothing pre-exists, the LATER real update (sourced
    // from the dev-cwd-resolved repo) installs the ENTIRE managed payload fresh — status/installed, never
    // preserved-with-sidecar (which would stage only `.new` files the commit excludes) — so the touched-path set is
    // genuinely non-empty and the commit step deterministically fails on the hostile hook (never a no-op NoChange).
    private static string NewRepoPendingAnUpdateWithHostileCommitHook()
    {
        string repo = Path.Combine(Path.GetTempPath(), "hx-runner-doti-update-hook-" + Guid.NewGuid().ToString("n"));
        SeedRepoPendingAnUpdateWithHostileCommitHook(repo);
        return repo;
    }

    private static void SeedRepoPendingAnUpdateWithHostileCommitHook(string repo)
    {
        Directory.CreateDirectory(Path.Combine(repo, ".doti"));
        // git never tracks an empty directory — a placeholder file is REQUIRED so the seed commit has something to
        // commit (an empty `.doti/` alone makes `git add -A` stage nothing and the seed commit itself fails).
        File.WriteAllText(Path.Combine(repo, ".doti", ".keep"), "seed placeholder\n");
        // DotiRepoScanner (the update-all discovery walk) only recognizes a directory as a Doti repo when
        // `.doti/payload.json` exists — a bare `.doti/` is enough for the single-repo `update` command's NotARepo
        // check, but NOT enough for update-all's scan, which would otherwise silently skip this repo entirely. The
        // stamped version must compare OLDER than Meta.Version ("0.0.0-test") — equal core, an ordinally-lower
        // prerelease tag — so DotiUpdater never refuses the reconcile as DotiVersionRelation.Ahead before it even
        // reaches the commit step.
        RepoPayloadStore.Write(repo, "0.0.0-alpha", "0.0.0-alpha");
        Git(repo, "init", "-q");
        Git(repo, "config", "user.email", "t@example.com");
        Git(repo, "config", "user.name", "Test");
        Git(repo, "config", "commit.gpgsign", "false");
        Git(repo, "add", "-A");
        GitWithEnv(repo, ["commit", "-q", "-m", "seed"], "DOTI_SANCTIONED_COMMIT", "1");

        // Arm the HOSTILE hook only AFTER the seed commit — the seed itself must succeed.
        string hooks = Path.Combine(repo, ".git", "hooks");
        Directory.CreateDirectory(hooks);
        string hostileHook = Path.Combine(hooks, "pre-commit");
        File.WriteAllText(hostileHook, "#!/bin/sh\nexit 1\n");
        // Git SKIPS a non-executable .sh hook on Linux/macOS — without the exec bit the commit would SUCCEED there and
        // this "commit fails" test would false-pass on Windows only (which runs .sh hooks regardless of the bit). Mark
        // it executable so the hostile hook fires on all OSes. Previously masked: the reconcile used to fail EARLIER at
        // `git add` (the 035 (B) `.doti/templates` bug), so the hook was never reached; 035's fix now reaches it.
        ExecutableFileMode.EnsureExecutable(hostileHook);
    }

    private static void Git(string dir, params string[] args) => GitWithEnv(dir, args, null, null);

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

    private static void ForceDeleteGit(string dir)
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
