using Hx.Sentrux.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class SentruxRegressionTests
{
    [Fact]
    public void DropWithinToleranceBandPasses()
    {
        (StageOutcome outcome, int? delta) = SentruxRegression.Evaluate(7342, 7300, 100);

        Assert.Equal(StageOutcome.Pass, outcome);
        Assert.Equal(-42, delta);
    }

    [Fact]
    public void DropBeyondToleranceBandFails()
    {
        (StageOutcome outcome, int? delta) = SentruxRegression.Evaluate(7342, 7100, 100);

        Assert.Equal(StageOutcome.Fail, outcome);
        Assert.Equal(-242, delta);
    }

    [Fact]
    public void ImprovementPasses()
    {
        (StageOutcome outcome, _) = SentruxRegression.Evaluate(7000, 7500, 100);

        Assert.Equal(StageOutcome.Pass, outcome);
    }

    [Fact]
    public void UnknownSignalIsNotARegression()
    {
        (StageOutcome outcome, int? delta) = SentruxRegression.Evaluate(null, 7000, 100);

        Assert.Equal(StageOutcome.Pass, outcome);
        Assert.Null(delta);
    }
}
