using Hx.Tooling.Contracts;

namespace Hx.Sentrux.Core;

public static partial class SentruxChecker
{
    private static bool TryDisabled(
        SentruxPolicy policy,
        ToolVerificationResult verification,
        List<string> advisory,
        out SentruxCheckResult? result)
    {
        result = null;
        if (policy.SentruxEnabled)
        {
            return false;
        }

        advisory.Add("Sentrux is disabled in rules/sentrux.json.");
        result = Build(StageOutcome.Skipped, verification, StageOutcome.Skipped, [], null, null,
            policy.SignalToleranceBand, StageOutcome.Skipped, ["Sentrux disabled."], advisory);
        return true;
    }

    private static bool TryUnverified(
        SentruxPolicy policy,
        ToolVerificationResult verification,
        List<string> advisory,
        out SentruxCheckResult? result)
    {
        result = null;
        if (verification.Verified)
        {
            return false;
        }

        advisory.Add("Sentrux structural gating is unavailable (fail-closed): "
            + (verification.Message ?? string.Join("; ", verification.Problems)));
        result = Build(StageOutcome.Blocked, verification, StageOutcome.Skipped, [], null, null,
            policy.SignalToleranceBand, StageOutcome.Skipped, [], advisory);
        return true;
    }

    private static SentruxCheckResult Build(
        StageOutcome outcome, ToolVerificationResult verification, StageOutcome rulesOutcome,
        IReadOnlyList<string> violations, int? quality, int? baseline, int toleranceBand,
        StageOutcome regressionOutcome, IReadOnlyList<string> notes, IReadOnlyList<string> advisory,
        string regressionVerdict = "pass")
    {
        int? delta = quality is not null && baseline is not null ? quality - baseline : null;
        return new SentruxCheckResult(
            JsonContractDefaults.SchemaVersion, outcome, verification, rulesOutcome, violations,
            quality, baseline, delta, toleranceBand, regressionOutcome, notes, advisory, regressionVerdict);
    }

    private static StageOutcome Worst(params StageOutcome[] outcomes)
    {
        if (outcomes.Contains(StageOutcome.Blocked)) return StageOutcome.Blocked;
        if (outcomes.Contains(StageOutcome.Fail)) return StageOutcome.Fail;
        if (outcomes.Contains(StageOutcome.Pass)) return StageOutcome.Pass;
        return StageOutcome.Skipped;
    }
}
