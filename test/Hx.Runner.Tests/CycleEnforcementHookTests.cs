using Hx.Cycle.Core;
using Hx.Runner.Core.Process;
using Xunit;

namespace Hx.Runner.Tests;

public sealed partial class CycleEnforcementTests
{
    [Fact]
    public void InsuranceHook_BlocksBareCommit_AllowsSanctioned()
    {
        string dir = InitRepo();
        try
        {
            string hookPath = HookInstaller.Install(dir);
            Assert.Equal(HookInstaller.HookScript, File.ReadAllText(hookPath));
            string? previousSentinel = Environment.GetEnvironmentVariable(PrecommitGuard.SentinelEnvVar);
            try
            {
                Environment.SetEnvironmentVariable(PrecommitGuard.SentinelEnvVar, null);
                Assert.False(PrecommitGuard.IsSanctioned());
                Environment.SetEnvironmentVariable(PrecommitGuard.SentinelEnvVar, "1");
                Assert.True(PrecommitGuard.IsSanctioned());
            }
            finally
            {
                Environment.SetEnvironmentVariable(PrecommitGuard.SentinelEnvVar, previousSentinel);
            }

            if (OperatingSystem.IsWindows())
            {
                return;
            }

            File.WriteAllText(Path.Combine(dir, "change.txt"), "x");
            Git(dir, "add", "-A");

            ProcessRunResult bare = ProcessRunner.Run(new ToolCommand("git", ["commit", "-m", "bare"], dir));
            Assert.NotEqual(0, bare.ExitCode);

            ProcessRunResult sanctioned = ProcessRunner.Run(new ToolCommand(
                "git", ["commit", "-m", "sanctioned"], dir,
                new Dictionary<string, string> { [PrecommitGuard.SentinelEnvVar] = "1" }));
            Assert.True(sanctioned.ExitCode == 0, sanctioned.StandardError + sanctioned.StandardOutput);
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void InsuranceHook_BlocksRawEmptyCommit_AllowsSanctionedEmptyReleaseCommit()
    {
        string dir = InitRepo();
        try
        {
            HookInstaller.Install(dir);
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            ProcessRunResult bareEmpty = ProcessRunner.Run(new ToolCommand(
                "git", ["commit", "--allow-empty", "-m", "raw empty"], dir));
            Assert.NotEqual(0, bareEmpty.ExitCode);
            Assert.Contains("Doti workflow transitions", bareEmpty.StandardError);

            ProcessRunResult sanctionedEmpty = ProcessRunner.Run(new ToolCommand(
                "git", ["commit", "--allow-empty", "-m", "release: sanctioned"], dir,
                new Dictionary<string, string> { [PrecommitGuard.SentinelEnvVar] = "1" }));
            Assert.True(sanctionedEmpty.ExitCode == 0, sanctionedEmpty.StandardError + sanctionedEmpty.StandardOutput);
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void InsuranceHook_RefusesToOverwriteExternalPreCommitHook()
    {
        string dir = InitRepo();
        try
        {
            string hook = Path.Combine(dir, ".git", "hooks", "pre-commit");
            File.WriteAllText(hook, "#!/bin/sh\necho custom hook\n");

            DotiHookInspection inspection = HookInstaller.Inspect(dir);
            Assert.Equal(HookInstaller.VerdictExternal, inspection.Verdict);
            Assert.False(inspection.CanInstallOrRefresh);

            DotiHookInstallResult result = HookInstaller.InstallIfSafe(dir);
            Assert.False(result.Success);
            Assert.False(result.Changed);
            Assert.Contains("refusing to overwrite", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("custom hook", File.ReadAllText(hook));
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void InsuranceHook_RefreshesDotiOwnedStaleHook()
    {
        string dir = InitRepo();
        try
        {
            string hook = Path.Combine(dir, ".git", "hooks", "pre-commit");
            File.WriteAllText(hook, "#!/bin/sh\n# doti insurance pre-commit hook\nexit 0\n");

            DotiHookInspection inspection = HookInstaller.Inspect(dir);
            Assert.Equal(HookInstaller.VerdictDotiOwned, inspection.Verdict);

            DotiHookInstallResult result = HookInstaller.InstallIfSafe(dir);
            Assert.True(result.Success);
            Assert.True(result.Changed);
            Assert.Equal(HookInstaller.VerdictExpected, result.Inspection.Verdict);
            Assert.Equal(HookInstaller.HookScript, File.ReadAllText(hook));
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void InsuranceHook_HonorsConfiguredGitHooksPath()
    {
        string dir = InitRepo();
        try
        {
            Git(dir, "config", "core.hooksPath", ".githooks");

            DotiHookInstallResult result = HookInstaller.InstallIfSafe(dir);

            string configuredHook = Path.Combine(dir, ".githooks", "pre-commit");
            Assert.True(result.Success);
            Assert.True(result.Changed);
            Assert.Equal(configuredHook, result.Inspection.HookPath);
            Assert.Equal(HookInstaller.HookScript, File.ReadAllText(configuredHook));
            Assert.False(File.Exists(Path.Combine(dir, ".git", "hooks", "pre-commit")));
        }
        finally
        {
            ForceDelete(dir);
        }
    }
}
