using System.Text.Json;
using Hx.Runner.Core.Gitleaks;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class GitleaksReportParserTests
{
    private static string FixturesDir => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    [Fact]
    public void ParsesFindingAndRedactsSecretValues()
    {
        string json = File.ReadAllText(Path.Combine(FixturesDir, "gitleaks-report.json"));

        IReadOnlyList<HygieneFinding> findings = GitleaksReportParser.Parse(json, path => path);

        HygieneFinding finding = Assert.Single(findings);
        Assert.Equal(HygieneFindingCategory.Secret, finding.Category);
        Assert.Equal(HygieneSeverity.Error, finding.Severity);
        Assert.Equal("aws-access-token", finding.RuleId);
        Assert.Equal("src/config.txt:aws-access-token:12", finding.Fingerprint);
        Assert.Equal(12, finding.Line);
        Assert.Equal("src/config.txt", finding.FilePath);

        string serialized = JsonSerializer.Serialize(finding, JsonContractSerializerOptions.Create());
        Assert.DoesNotContain("supersecretvalue123", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("AKIAIOSFODNN7EXAMPLE", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void RemapsScanRootPathsToRepoRelative()
    {
        string json = File.ReadAllText(Path.Combine(FixturesDir, "gitleaks-report.json"));

        IReadOnlyList<HygieneFinding> findings = GitleaksReportParser.Parse(json, _ => "templates/dotnet-cli/src/config.txt");

        Assert.Equal("templates/dotnet-cli/src/config.txt", findings[0].FilePath);
    }

    [Fact]
    public void EmptyReportYieldsNoFindings()
    {
        Assert.Empty(GitleaksReportParser.Parse(string.Empty, path => path));
    }
}
