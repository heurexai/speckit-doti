using Hx.Tooling.Contracts;

namespace Hx.Sentrux.Core;

public static class SentruxRegression
{
    /// <summary>
    /// Evaluate a quality-signal regression against the operator tolerance band (Q3).
    /// A drop larger than the band fails; an unknown signal is not treated as a
    /// regression here (the caller handles structural degradation separately).
    /// </summary>
    public static (StageOutcome Outcome, int? Delta) Evaluate(int? before, int? after, int toleranceBand)
    {
        if (before is null || after is null)
        {
            return (StageOutcome.Pass, null);
        }

        int delta = after.Value - before.Value;
        return delta < 0 && -delta > toleranceBand
            ? (StageOutcome.Fail, delta)
            : (StageOutcome.Pass, delta);
    }
}
