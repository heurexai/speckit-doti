namespace Hx.Sentrux.Core;

/// <summary>The three-state quality-signal regression verdict (FR-030): within tolerance (<see cref="Pass"/>), above
/// the hard tolerance but within the escalation band — two optimization tries (<see cref="EscalationBand"/>), or
/// beyond the band (<see cref="Fail"/>).</summary>
public enum SentruxRegressionVerdict
{
    Pass,
    EscalationBand,
    Fail,
}

public static class SentruxRegression
{
    /// <summary>
    /// Evaluate a quality-signal regression against the operator tolerance band (FR-030). The band measures the
    /// quality-signal DEVIATION (the drop magnitude — the operator's "complexity deviation"); an absolute structural
    /// degradation is a separate, harder failure handled by the caller (never band-eligible — BL-1). A drop within
    /// tolerance is <see cref="SentruxRegressionVerdict.Pass"/>; above tolerance but within
    /// <c>toleranceBand * escalationMultiplier</c> (the 130 band) is <see cref="SentruxRegressionVerdict.EscalationBand"/>;
    /// beyond is <see cref="SentruxRegressionVerdict.Fail"/>. An unknown signal is not a regression.
    /// </summary>
    public static (SentruxRegressionVerdict Verdict, int? Delta) Evaluate(
        int? before, int? after, int toleranceBand, double escalationMultiplier)
    {
        if (before is null || after is null)
        {
            return (SentruxRegressionVerdict.Pass, null);
        }

        int delta = after.Value - before.Value;
        int drop = delta < 0 ? -delta : 0;
        if (drop <= toleranceBand)
        {
            return (SentruxRegressionVerdict.Pass, delta);
        }

        double escalationLimit = toleranceBand * escalationMultiplier;
        return drop <= escalationLimit
            ? (SentruxRegressionVerdict.EscalationBand, delta)
            : (SentruxRegressionVerdict.Fail, delta);
    }

    /// <summary>The integer escalation limit (the "130" for a 100 band at 1.3×).</summary>
    public static int EscalationLimit(int toleranceBand, double escalationMultiplier) =>
        (int)Math.Ceiling(toleranceBand * escalationMultiplier);

    /// <summary>The kebab id recorded in the gate proof / cycle log.</summary>
    public static string Id(SentruxRegressionVerdict verdict) => verdict switch
    {
        SentruxRegressionVerdict.Pass => "pass",
        SentruxRegressionVerdict.EscalationBand => "escalation-band",
        _ => "fail",
    };
}
