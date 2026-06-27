using Hx.Cli.Kernel;
using Hx.Cycle.Core;
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

    public static CliResult SentruxBaseline(CliMeta meta, string repo, bool authorizeRebaseline)
    {
        // M-2 (FR-031): raising the committed baseline needs explicit operator intent AND a change-set-fresh
        // arch-review classifying the growth as functionality-driven. First-scaffold baseline creation runs through
        // FirstSmokeRunner (which calls SentruxBaselineRunner.Save directly), so it is NOT gated by this command.
        SentruxRebaselineAuthorization authorization = SentruxRebaselinePolicy.Authorize(repo, authorizeRebaseline);
        if (!authorization.Authorized)
        {
            return CliResults.Fail(meta, "sentrux baseline", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_SentruxRebaselineRefused, authorization.Reason, target: "--authorize-rebaseline")],
                "Sentrux rebaseline refused.", authorization);
        }

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
