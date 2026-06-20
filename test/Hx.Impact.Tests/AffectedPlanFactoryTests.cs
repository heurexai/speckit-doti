using Hx.Impact.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Impact.Tests;

public sealed class AffectedPlanFactoryTests
{
    [Fact]
    public void BootstrapPlanIsAConservativeFullGate()
    {
        var plan = AffectedPlanFactory.BootstrapFullPlan();

        Assert.Equal(AffectedOutcome.FullGateRequired, plan.Outcome);
        Assert.NotEmpty(plan.Reasons);
        Assert.Empty(plan.SelectedTests);
    }
}
