using Hx.Runner.Core.Process;

namespace Hx.Runner.Core.Gitleaks;

/// <summary>
/// Builds Gitleaks process invocations via <see cref="ToolCommand"/>
/// (<see cref="System.Diagnostics.ProcessStartInfo.ArgumentList"/>). Exact flags
/// must be re-verified against the pinned Gitleaks version before vendoring.
/// </summary>
public static class GitleaksProcessAdapter
{
    public static ToolCommand BuildDirScan(string executablePath, string configPath, string scanRoot, string reportPath)
    {
        return new ToolCommand(
            executablePath,
            [
                "dir",
                "--config", configPath,
                "--report-format", "json",
                "--report-path", reportPath,
                "--exit-code", "1",
                "--no-banner",
                scanRoot
            ],
            scanRoot);
    }

    public static ToolCommand BuildGitScan(string executablePath, string configPath, string repositoryRoot, string reportPath)
    {
        return new ToolCommand(
            executablePath,
            [
                "git",
                "--config", configPath,
                "--report-format", "json",
                "--report-path", reportPath,
                "--exit-code", "1",
                "--no-banner",
                repositoryRoot
            ],
            repositoryRoot);
    }
}
