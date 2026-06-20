using Hx.Security.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class SecurityTests
{
    private const string SampleVulnJson = """
{"version":1,"projects":[{"path":"X","frameworks":[{"framework":"net10.0","topLevelPackages":[{"id":"Vuln.Pkg","resolvedVersion":"1.0.0","vulnerabilities":[{"severity":"High","advisoryurl":"https://github.com/advisories/GHSA-x"}]}],"transitivePackages":[{"id":"Trans.Pkg","resolvedVersion":"2.0.0","vulnerabilities":[{"severity":"Critical","advisoryurl":"https://github.com/advisories/GHSA-y"}]}]}]}]}
""";

    private const string CleanJson = """
{"version":1,"projects":[{"path":"X","frameworks":[{"framework":"net10.0","topLevelPackages":[],"transitivePackages":[]}]}]}
""";

    [Fact]
    public void Parse_extracts_top_level_and_transitive_vulnerabilities()
    {
        var findings = PackageVulnerabilityScanner.Parse(SampleVulnJson);
        Assert.Equal(2, findings.Count);
        Assert.Contains(findings, f => f.PackageId == "Vuln.Pkg" && f.Severity == "High" && !f.Transitive);
        Assert.Contains(findings, f => f.PackageId == "Trans.Pkg" && f.Severity == "Critical" && f.Transitive);
    }

    [Fact]
    public void Parse_clean_returns_no_findings() => Assert.Empty(PackageVulnerabilityScanner.Parse(CleanJson));

    [Fact]
    public void Evaluate_fails_closed_on_a_finding_at_or_above_floor()
    {
        var result = SecurityScanner.Evaluate(PackageVulnerabilityScanner.Parse(SampleVulnJson), null, new SecurityPolicy(1, "low", []), sastEnforced: true, "enforced");
        Assert.Equal(StageOutcome.Fail, result.Outcome);
        Assert.Equal(2, result.Vulnerabilities.Count);
    }

    [Fact]
    public void Evaluate_clean_passes_when_sast_enforced()
    {
        var result = SecurityScanner.Evaluate([], null, SecurityPolicy.Default, sastEnforced: true, "enforced");
        Assert.Equal(StageOutcome.Pass, result.Outcome);
    }

    [Fact]
    public void Evaluate_fails_when_sast_not_enforced()
    {
        var result = SecurityScanner.Evaluate([], null, SecurityPolicy.Default, sastEnforced: false, "AnalysisModeSecurity=unset");
        Assert.Equal(StageOutcome.Fail, result.Outcome);
    }

    [Fact]
    public void Evaluate_blocks_on_scan_error()
    {
        var result = SecurityScanner.Evaluate([], "network failure", SecurityPolicy.Default, sastEnforced: true, "enforced");
        Assert.Equal(StageOutcome.Blocked, result.Outcome);
    }

    [Fact]
    public void Evaluate_unexpired_suppression_drops_the_finding()
    {
        var policy = new SecurityPolicy(1, "low",
            [new SecuritySuppression("Vuln.Pkg", "tracked in #123", "2999-01-01"), new SecuritySuppression("Trans.Pkg", "tracked", null)]);
        var result = SecurityScanner.Evaluate(PackageVulnerabilityScanner.Parse(SampleVulnJson), null, policy, sastEnforced: true, "enforced");
        Assert.Equal(StageOutcome.Pass, result.Outcome);
        Assert.Empty(result.Vulnerabilities);
        Assert.Contains(result.Reasons, r => r.Contains("suppressed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_expired_suppression_does_not_apply()
    {
        var policy = new SecurityPolicy(1, "low",
            [new SecuritySuppression("Vuln.Pkg", "old", "2000-01-01"), new SecuritySuppression("Trans.Pkg", "old", "2000-01-01")]);
        var result = SecurityScanner.Evaluate(PackageVulnerabilityScanner.Parse(SampleVulnJson), null, policy, sastEnforced: true, "enforced");
        Assert.Equal(StageOutcome.Fail, result.Outcome);
        Assert.Contains(result.Reasons, r => r.Contains("expired", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("low", 1)]
    [InlineData("Moderate", 2)]
    [InlineData("HIGH", 3)]
    [InlineData("critical", 4)]
    [InlineData("weird", 3)]
    public void SeverityRank_is_case_insensitive_and_conservative(string severity, int expected) =>
        Assert.Equal(expected, SeverityLevels.Rank(severity));

    [Fact]
    public void AnalyzerEnforcement_is_true_for_the_scaffold_build_props()
    {
        (bool enforced, _) = SecurityScanner.CheckAnalyzerEnforcement(FindRepoRoot());
        Assert.True(enforced);
    }

    [Fact]
    public void SecurityPolicy_defaults_to_low_floor_when_absent()
    {
        string temp = Directory.CreateTempSubdirectory("hx-sec-").FullName;
        try { Assert.Equal("low", SecurityPolicyLoader.Load(temp).AuditFloor); }
        finally { Directory.Delete(temp, recursive: true); }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "scaffold-dotnet.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("scaffold-dotnet.slnx not found above the test output.");
    }
}
