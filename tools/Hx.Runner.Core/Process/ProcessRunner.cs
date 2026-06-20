using System.Diagnostics;

namespace Hx.Runner.Core.Process;

public sealed record ProcessRunResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Runs a <see cref="ToolCommand"/> with redirected output. All arguments are
/// passed via <see cref="ProcessStartInfo.ArgumentList"/>; no shell strings.
/// </summary>
public static class ProcessRunner
{
    public static ProcessRunResult Run(ToolCommand command)
    {
        using var process = new System.Diagnostics.Process { StartInfo = command.ToStartInfo() };
        process.Start();

        // Drain stdout and stderr CONCURRENTLY. Reading one stream to the end before the other
        // deadlocks when the child fills the unread stream's pipe buffer (~4 KB). This is latent for
        // quiet native tools (gitleaks/sentrux) but real for verbose children like `dotnet test`.
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return new ProcessRunResult(process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }
}
