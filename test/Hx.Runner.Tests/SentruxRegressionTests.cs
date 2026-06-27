using Hx.Sentrux.Core;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class SentruxRegressionTests
{
    private const double Mult = 1.3; // a 100 tolerance band → a 130 escalation limit

    [Fact]
    public void DropWithinToleranceBandPasses()
    {
        (SentruxRegressionVerdict verdict, int? delta) = SentruxRegression.Evaluate(7342, 7300, 100, Mult);

        Assert.Equal(SentruxRegressionVerdict.Pass, verdict);
        Assert.Equal(-42, delta);
    }

    [Fact]
    public void DropAboveToleranceButWithinTheBandIsEscalationBand()
    {
        // drop 120 — above tolerance 100, within the 130 limit ⇒ two optimization tries, not a hard fail.
        (SentruxRegressionVerdict verdict, int? delta) = SentruxRegression.Evaluate(7342, 7222, 100, Mult);

        Assert.Equal(SentruxRegressionVerdict.EscalationBand, verdict);
        Assert.Equal(-120, delta);
    }

    [Fact]
    public void DropBeyondTheEscalationBandFails()
    {
        (SentruxRegressionVerdict verdict, int? delta) = SentruxRegression.Evaluate(7342, 7100, 100, Mult);

        Assert.Equal(SentruxRegressionVerdict.Fail, verdict);
        Assert.Equal(-242, delta);
    }

    [Fact]
    public void ImprovementPasses()
    {
        (SentruxRegressionVerdict verdict, _) = SentruxRegression.Evaluate(7000, 7500, 100, Mult);

        Assert.Equal(SentruxRegressionVerdict.Pass, verdict);
    }

    [Fact]
    public void UnknownSignalIsNotARegression()
    {
        (SentruxRegressionVerdict verdict, int? delta) = SentruxRegression.Evaluate(null, 7000, 100, Mult);

        Assert.Equal(SentruxRegressionVerdict.Pass, verdict);
        Assert.Null(delta);
    }
}
