using Hx.Cli.Kernel;
using Hx.Doti.Core;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    public static CliResult DotiPayloadCheck(CliMeta meta, string repo)
    {
        try
        {
            DotiPayloadCheckResult result = DotiPayloadParityChecker.Check(repo);
            string summary = result.Outcome == StageOutcome.Pass
                ? $"Doti payload parity passed for {result.CheckedCount} managed file(s)."
                : $"Doti payload parity failed for {result.Drifted.Count} managed file(s).";
            return result.Outcome == StageOutcome.Pass
                ? CliResults.Ok(meta, "doti payload check", summary, result)
                : CliResults.Fail(meta, "doti payload check", ExitClass.Integrity,
                    result.Drifted.Select(path => Diag.Of(ErrorCodes.Integrity_DotiPayloadDrift, "Doti payload drift: " + path, target: path)).ToArray(),
                    summary,
                    result);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or DirectoryNotFoundException)
        {
            return CliResults.Fail(meta, "doti payload check", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, ex.Message)]);
        }
    }
}
