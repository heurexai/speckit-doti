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
            HookInstaller.Install(dir);
            File.WriteAllText(Path.Combine(dir, "change.txt"), "x");
            Git(dir, "add", "-A");

            ProcessRunResult bare = ProcessRunner.Run(new ToolCommand("git", ["commit", "-m", "bare"], dir));
            Assert.NotEqual(0, bare.ExitCode);

            ProcessRunResult sanctioned = ProcessRunner.Run(new ToolCommand(
                "git", ["commit", "-m", "sanctioned"], dir,
                new Dictionary<string, string> { [PrecommitGuard.SentinelEnvVar] = "1" }));
            Assert.Equal(0, sanctioned.ExitCode);
        }
        finally
        {
            ForceDelete(dir);
        }
    }
}
