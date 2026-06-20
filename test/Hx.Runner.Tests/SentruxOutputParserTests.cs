using Hx.Sentrux.Core;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class SentruxOutputParserTests
{
    private static string FixturesDir => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    [Fact]
    public void ParsesPassingCheck()
    {
        SentruxOutputParser.CheckReport report =
            SentruxOutputParser.ParseCheck(File.ReadAllText(Path.Combine(FixturesDir, "sentrux-check-pass.json")));

        Assert.True(report.Passed);
        Assert.Equal(7342, report.QualitySignal);
        Assert.Empty(report.Violations);
    }

    [Fact]
    public void ParsesFailingCheckWithViolations()
    {
        SentruxOutputParser.CheckReport report =
            SentruxOutputParser.ParseCheck(File.ReadAllText(Path.Combine(FixturesDir, "sentrux-check-fail.json")));

        Assert.False(report.Passed);
        Assert.Equal(6100, report.QualitySignal);
        Assert.Contains(report.Violations, v => v.Contains("max_cycles", StringComparison.Ordinal));
    }

    [Fact]
    public void ParsesGateRegressionText()
    {
        SentruxOutputParser.GateReport report =
            SentruxOutputParser.ParseGate("sentrux gate\n\nQuality:      7342 -> 6891\n\nDEGRADED");

        Assert.Equal(7342, report.SignalBefore);
        Assert.Equal(6891, report.SignalAfter);
        Assert.True(report.Degraded);
    }

    [Fact]
    public void ParsesGateCleanText()
    {
        SentruxOutputParser.GateReport report =
            SentruxOutputParser.ParseGate("Quality:      7342 -> 7342\n\nNo degradation detected");

        Assert.False(report.Degraded);
    }
}
