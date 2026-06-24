using Hx.Cli.Kernel;
using Hx.Cycle.Core;
using Hx.Scaffold.Cli;
using Hx.Scaffold.Core.Update;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Scaffold.Tests;

public sealed partial class ScaffoldCommandsTests
{
    [Fact]
    public void Update_dryRun_succeeds_for_clean_versioned_git_repo()
    {
        string repo = NewVersionedGitRepo();
        string cache = NewTempDir("hx-update-cache-");
        string release = NewReleaseArchive("1.0.0", out string checksum);
        try
        {
            ScaffoldUpdateReport report = ScaffoldUpdateService.Plan(
                new ScaffoldUpdateRequest(repo, DryRun: true, Force: false, NoWorktree: false, RunningVersion: "1.0.0"),
                FakeReleaseServices(cache, release, checksum));

            Assert.True(report.DryRun);
            Assert.Contains(report.PlannedActions, a => a.Contains("create backup worktree", StringComparison.Ordinal));
            Assert.NotNull(report.Hook);
            Assert.Equal(HookInstaller.VerdictMissing, report.Hook.Verdict);
            Assert.Contains(report.PlannedActions, a => a.Contains("install the Doti insurance", StringComparison.Ordinal));
            Assert.Contains(report.FollowUpCommands, f => f.Contains("open, unimplemented unnumbered spec", StringComparison.Ordinal));
            Assert.Contains(report.FollowUpCommands, f => f.Contains("implemented/completed legacy specs unchanged", StringComparison.Ordinal));
            Assert.Empty(report.Blockers);
        }
        finally
        {
            ForceDelete(repo);
            ForceDelete(cache);
            ForceDelete(Path.GetDirectoryName(release)!);
        }
    }

    [Fact]
    public void Update_refuses_modified_managed_assets_unless_force()
    {
        string repo = NewVersionedGitRepo();
        string cache = NewTempDir("hx-update-cache-");
        string release = NewReleaseArchive("1.0.0", out string checksum);
        try
        {
            File.AppendAllText(Path.Combine(repo, ".agents", "skills", "doti-specify", "SKILL.md"), "\nlocal change\n");
            Git(repo, "add", ".agents/skills/doti-specify/SKILL.md");
            Git(repo, "commit", "-q", "-m", "customize skill");

            ScaffoldUpdateReport blocked = ScaffoldUpdateService.Plan(
                new ScaffoldUpdateRequest(repo, DryRun: true, Force: false, NoWorktree: false, RunningVersion: "1.0.0"),
                FakeReleaseServices(cache, release, checksum));
            Assert.Contains(blocked.Blockers, b => b.Contains("modified skill/generated-instruction", StringComparison.Ordinal));

            ScaffoldUpdateReport report = ScaffoldUpdateService.Plan(
                new ScaffoldUpdateRequest(repo, DryRun: true, Force: true, NoWorktree: true, RunningVersion: "1.0.0"),
                FakeReleaseServices(cache, release, checksum));
            Assert.Empty(report.Blockers);
            Assert.Contains(report.PlannedActions, a => a.Contains("--noworktree", StringComparison.Ordinal));
            Assert.Contains(report.PlannedActions, a => a.Contains("--force", StringComparison.Ordinal));
        }
        finally
        {
            ForceDelete(repo);
            ForceDelete(cache);
            ForceDelete(Path.GetDirectoryName(release)!);
        }
    }

    [Fact]
    public void Update_force_does_not_bypass_dirty_planned_paths()
    {
        string repo = NewVersionedGitRepo();
        string cache = NewTempDir("hx-update-cache-");
        string release = NewReleaseArchive("1.0.0", out string checksum);
        try
        {
            File.AppendAllText(Path.Combine(repo, ".agents", "skills", "doti-specify", "SKILL.md"), "\nunstaged local change\n");

            ScaffoldUpdateReport report = ScaffoldUpdateService.Plan(
                new ScaffoldUpdateRequest(repo, DryRun: true, Force: true, NoWorktree: true, RunningVersion: "1.0.0"),
                FakeReleaseServices(cache, release, checksum));

            Assert.Contains(report.Blockers, b => b.Contains("dirty managed path", StringComparison.Ordinal));
        }
        finally
        {
            ForceDelete(repo);
            ForceDelete(cache);
            ForceDelete(Path.GetDirectoryName(release)!);
        }
    }

    [Fact]
    public void Update_mutates_original_checkout_creates_backup_worktree_reuses_cache_and_preserves_live_state()
    {
        string repo = NewVersionedGitRepo();
        string cache = NewTempDir("hx-update-cache-");
        string worktrees = NewTempDir("hx-update-worktrees-");
        string release = NewReleaseArchive("1.0.0", out string checksum);
        int downloads = 0;
        try
        {
            Directory.CreateDirectory(Path.Combine(repo, ".sentrux"));
            File.WriteAllText(Path.Combine(repo, ".sentrux", "baseline.json"), "live baseline");
            Git(repo, "add", ".sentrux/baseline.json");
            Git(repo, "commit", "-q", "-m", "live baseline");
            File.WriteAllText(Path.Combine(repo, ".gitignore"), ".nomos/cycle-state.json\n");
            Git(repo, "add", ".gitignore");
            Git(repo, "commit", "-q", "-m", "nomos cycle ignore");

            ScaffoldUpdateServices services = FakeReleaseServices(cache, release, checksum, () => downloads++, worktrees);
            ScaffoldUpdateReport first = ScaffoldUpdateService.Plan(
                new ScaffoldUpdateRequest(repo, DryRun: false, Force: false, NoWorktree: false, RunningVersion: "1.0.0"),
                services);

            Assert.Empty(first.Blockers);
            Assert.NotNull(first.BackupWorktreePath);
            Assert.True(Directory.Exists(first.BackupWorktreePath));
            Assert.Contains(".doti/workflows/doti/workflow.yml", first.ChangedPaths);
            Assert.Contains(".gitignore", first.ChangedPaths);
            Assert.Contains(".gitignore", first.PlannedReplacePaths);
            Assert.NotNull(first.Hook);
            Assert.True(first.Hook.Changed);
            Assert.Equal(HookInstaller.VerdictExpected, first.Hook.Verdict);
            Assert.Equal(HookInstaller.HookScript, File.ReadAllText(Path.Combine(repo, ".git", "hooks", "pre-commit")));
            Assert.Contains(".sentrux", first.PreservedLivePaths);
            Assert.Equal("live baseline", File.ReadAllText(Path.Combine(repo, ".sentrux", "baseline.json")));
            string gitignore = File.ReadAllText(Path.Combine(repo, ".gitignore"));
            Assert.Contains(".nomos/cycle-state.json", gitignore);
            Assert.Contains(".doti/cycle-state.json", gitignore);
            Assert.Contains(".doti/gate-proof.json", gitignore);
            Assert.Contains("clarify", File.ReadAllText(Path.Combine(repo, ".doti", "workflows", "doti", "workflow.yml")));
            Assert.Contains("New Spec", File.ReadAllText(Path.Combine(repo, ".agents", "skills", "doti-specify", "SKILL.md")));
            Assert.Equal(2, downloads);

            ScaffoldUpdateReport second = ScaffoldUpdateService.Plan(
                new ScaffoldUpdateRequest(repo, DryRun: false, Force: false, NoWorktree: true, RunningVersion: "1.0.0"),
                services);

            Assert.Empty(second.Blockers);
            Assert.Equal("reuse-verified-cache", second.CacheAction);
            Assert.Empty(second.ChangedPaths);
            Assert.NotNull(second.Hook);
            Assert.False(second.Hook.Changed);
            Assert.Equal(HookInstaller.VerdictExpected, second.Hook.Verdict);
            Assert.Equal(2, downloads);

            if (first.BackupWorktreePath is not null)
            {
                Git(repo, "worktree", "remove", "--force", first.BackupWorktreePath);
            }
        }
        finally
        {
            ForceDelete(repo);
            ForceDelete(cache);
            ForceDelete(worktrees);
            ForceDelete(Path.GetDirectoryName(release)!);
        }
    }

    [Fact]
    public void Update_refuses_dirty_gitignore_when_runtime_state_entries_are_missing()
    {
        string repo = NewVersionedGitRepo();
        string cache = NewTempDir("hx-update-cache-");
        string release = NewReleaseArchive("1.0.0", out string checksum);
        try
        {
            File.WriteAllText(Path.Combine(repo, ".gitignore"), ".nomos/cycle-state.json\n");
            Git(repo, "add", ".gitignore");
            Git(repo, "commit", "-q", "-m", "nomos cycle ignore");
            File.AppendAllText(Path.Combine(repo, ".gitignore"), "# local edit\n");

            ScaffoldUpdateReport report = ScaffoldUpdateService.Plan(
                new ScaffoldUpdateRequest(repo, DryRun: true, Force: true, NoWorktree: true, RunningVersion: "1.0.0"),
                FakeReleaseServices(cache, release, checksum));

            Assert.Contains(report.Blockers, b => b.Contains("dirty managed path", StringComparison.Ordinal)
                && b.Contains(".gitignore", StringComparison.Ordinal));
            Assert.Contains(".gitignore", report.PlannedReplacePaths);
        }
        finally
        {
            ForceDelete(repo);
            ForceDelete(cache);
            ForceDelete(Path.GetDirectoryName(release)!);
        }
    }

    [Fact]
    public void Update_creates_release_manifest_when_single_executable_project_is_unambiguous()
    {
        string repo = NewVersionedGitRepo();
        string cache = NewTempDir("hx-update-cache-");
        string release = NewReleaseArchive("1.0.0", out string checksum);
        try
        {
            string project = Path.Combine(repo, "src", "Ergon.Cli", "Ergon.Cli.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(project)!);
            File.WriteAllText(project, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <AssemblyName>ergon</AssemblyName>
                  </PropertyGroup>
                </Project>
                """);
            Git(repo, "add", "src/Ergon.Cli/Ergon.Cli.csproj");
            Git(repo, "commit", "-q", "-m", "add cli project");

            ScaffoldUpdateReport report = ScaffoldUpdateService.Plan(
                new ScaffoldUpdateRequest(repo, DryRun: false, Force: false, NoWorktree: true, RunningVersion: "1.0.0"),
                FakeReleaseServices(cache, release, checksum));

            Assert.Empty(report.Blockers);
            Assert.Contains(".doti/release.json", report.ChangedPaths);
            Assert.Contains(".doti/release.json", report.PlannedCreatePaths);
            string manifest = File.ReadAllText(Path.Combine(repo, ".doti", "release.json"));
            Assert.Contains("\"packageName\":\"ergon\"", manifest);
            Assert.Contains("\"publishProject\":\"src/Ergon.Cli/Ergon.Cli.csproj\"", manifest);
            Assert.Contains("\"publishedExecutableName\":\"ergon\"", manifest);
            Assert.Contains("\"executableName\":\"ergon\"", manifest);
            Assert.Contains(".doti/release.json", report.PreservedLivePaths);
        }
        finally
        {
            ForceDelete(repo);
            ForceDelete(cache);
            ForceDelete(Path.GetDirectoryName(release)!);
        }
    }

    [Fact]
    public void Update_refuses_to_overwrite_external_precommit_hook()
    {
        string repo = NewVersionedGitRepo();
        string cache = NewTempDir("hx-update-cache-");
        string release = NewReleaseArchive("1.0.0", out string checksum);
        string hook = Path.Combine(repo, ".git", "hooks", "pre-commit");
        try
        {
            File.WriteAllText(hook, "#!/bin/sh\necho custom hook\n");

            ScaffoldUpdateReport report = ScaffoldUpdateService.Plan(
                new ScaffoldUpdateRequest(repo, DryRun: false, Force: true, NoWorktree: true, RunningVersion: "1.0.0"),
                FakeReleaseServices(cache, release, checksum));

            Assert.Contains(report.Blockers, b => b.Contains("not owned by Doti", StringComparison.Ordinal));
            Assert.Contains(report.Diagnostics, d => d.Code == "update.hook.external-precommit"
                && IsPreCommitHookPath(d.Path));
            Assert.NotNull(report.Hook);
            Assert.Equal(HookInstaller.VerdictExternal, report.Hook.Verdict);
            Assert.False(report.Hook.Changed);
            Assert.Contains("custom hook", File.ReadAllText(hook));
        }
        finally
        {
            ForceDelete(repo);
            ForceDelete(cache);
            ForceDelete(Path.GetDirectoryName(release)!);
        }
    }

    private static bool IsPreCommitHookPath(string? path)
    {
        if (path is null)
        {
            return false;
        }

        string normalized = path.Replace('\\', '/');
        return normalized.EndsWith("/.git/hooks/pre-commit", StringComparison.Ordinal);
    }

    [Fact]
    public void Update_refuses_doti_shaped_directory_without_git()
    {
        string repo = NewVersionedRepo();
        try
        {
            CliResult r = ScaffoldCommands.Update(Meta, repo, dryRun: true, force: false, noWorktree: false);

            Assert.False(r.Ok);
            Assert.Contains(r.Errors, e => e.Message.Contains("doti-shaped target has no Git", StringComparison.Ordinal));
        }
        finally
        {
            ForceDelete(repo);
        }
    }
}
