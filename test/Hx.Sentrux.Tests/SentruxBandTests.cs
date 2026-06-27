using Hx.Sentrux.Core;
using Xunit;

namespace Hx.Sentrux.Tests;

/// <summary>T023 (FR-030): the three-state escalation band — Pass within tolerance, EscalationBand above tolerance
/// but within 1.3×, Fail beyond. (BL-1's "gate.Degraded is a hard fail, never band-eligible" lives in the
/// SentruxChecker wiring, which shells the native tool; the arithmetic is pinned here.)</summary>
public sealed class SentruxBandTests
{
    [Theory]
    [InlineData(80, SentruxRegressionVerdict.Pass)]          // within tolerance 100
    [InlineData(100, SentruxRegressionVerdict.Pass)]         // exactly tolerance
    [InlineData(120, SentruxRegressionVerdict.EscalationBand)] // > 100, <= 130
    [InlineData(130, SentruxRegressionVerdict.EscalationBand)] // exactly the limit
    [InlineData(131, SentruxRegressionVerdict.Fail)]          // beyond the band
    public void Evaluate_classifies_the_drop_against_the_band(int drop, SentruxRegressionVerdict expected)
    {
        (SentruxRegressionVerdict verdict, _) = SentruxRegression.Evaluate(1000, 1000 - drop, 100, 1.3);

        Assert.Equal(expected, verdict);
    }

    [Fact]
    public void EscalationLimit_is_band_times_multiplier()
    {
        Assert.Equal(130, SentruxRegression.EscalationLimit(100, 1.3));
    }

    [Fact]
    public void Id_maps_the_verdict_to_its_kebab_id()
    {
        Assert.Equal("escalation-band", SentruxRegression.Id(SentruxRegressionVerdict.EscalationBand));
        Assert.Equal("pass", SentruxRegression.Id(SentruxRegressionVerdict.Pass));
        Assert.Equal("fail", SentruxRegression.Id(SentruxRegressionVerdict.Fail));
    }

    [Fact]
    public void Policy_default_escalation_multiplier_is_one_point_three()
    {
        Assert.Equal(1.3, SentruxPolicy.Default().EffectiveEscalationBandMultiplier);
    }
}
