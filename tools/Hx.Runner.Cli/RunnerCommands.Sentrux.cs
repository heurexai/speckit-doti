using Hx.Cli.Kernel;
using Hx.Sentrux.Core;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    public static CliResult SentruxVerify(CliMeta meta, string repo)
    {
        SentruxPolicy policy = SentruxPolicyLoader.Load(repo, out _);
        return Verify(meta, "sentrux verify", SentruxManifestValidator.Verify(repo, Rid(), policy));
    }

    public static CliResult SentruxBaseline(CliMeta meta, string repo)
    {
        SentruxBaselineResult result = SentruxBaselineRunner.Save(repo);
        return CliResults.FromStage(meta, "sentrux baseline", result.Outcome, "Sentrux baseline.", result);
    }

    public static CliResult SentruxCheck(CliMeta meta, string repo)
    {
        SentruxCheckResult result = SentruxChecker.Check(repo);
        return CliResults.FromStage(meta, "sentrux check", result.Outcome,
            $"signal={result.QualitySignal}; regression={result.RegressionOutcome}.", result);
    }
}
