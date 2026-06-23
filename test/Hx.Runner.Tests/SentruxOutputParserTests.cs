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
    public void ParsesRicherViolationObjectsWithoutDroppingLocationOrHelp()
    {
        SentruxOutputParser.CheckReport report = SentruxOutputParser.ParseCheck(
            """
            {
              "passed": false,
              "qualitySignal": 0.61,
              "violations": [
                {
                  "rule": "god_file",
                  "message": "file has too many responsibilities",
                  "path": "tools/Hx.Runner.Cli/RunnerCommands.cs",
                  "line": 42,
                  "details": "split command orchestration from parsing",
                  "remediation": "move the core logic behind a service"
                }
              ]
            }
            """);

        string violation = Assert.Single(report.Violations);
        Assert.Contains("god_file", violation, StringComparison.Ordinal);
        Assert.Contains("RunnerCommands.cs:42", violation, StringComparison.Ordinal);
        Assert.Contains("split command orchestration", violation, StringComparison.Ordinal);
        Assert.Contains("move the core logic", violation, StringComparison.Ordinal);
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
