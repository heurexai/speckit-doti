using System.Xml.Linq;
using Hx.Tooling.Contracts;

namespace Hx.Security.Core;

/// <summary>
/// The security gate engine. SCA via <see cref="PackageVulnerabilityScanner"/>; SAST is reported, not
/// re-run — the build is the SAST enforcement point (the .NET security analyzers as errors), so
/// <see cref="CheckAnalyzerEnforcement"/> verifies the props enforce it. <see cref="Evaluate"/> is pure
/// (the testable core): applies suppressions + the severity floor, fails closed on findings ≥ floor or
/// when SAST is not enforced, and Blocked when the SCA scan could not run.
/// </summary>
public static class SecurityScanner
{
    public static SecurityScanResult Scan(string repositoryRoot)
    {
        string? solution = DiscoverSolution(repositoryRoot, out string? solutionError);
        if (solution is null)
        {
            return new SecurityScanResult(JsonContractDefaults.SchemaVersion, StageOutcome.Blocked, [], "unknown", [solutionError!]);
        }

        SecurityPolicy policy = SecurityPolicyLoader.Load(repositoryRoot);
        (IReadOnlyList<SecurityFinding> raw, string? scaError) = PackageVulnerabilityScanner.Scan(repositoryRoot, solution);
        (bool sastEnforced, string sastStatus) = CheckAnalyzerEnforcement(repositoryRoot);
        return Evaluate(raw, scaError, policy, sastEnforced, sastStatus);
    }

    /// <summary>Pure policy evaluation — no IO. The testable core.</summary>
    public static SecurityScanResult Evaluate(
        IReadOnlyList<SecurityFinding> raw,
        string? scaError,
        SecurityPolicy policy,
        bool sastEnforced,
        string sastStatus)
    {
        if (scaError is not null)
        {
            return new SecurityScanResult(JsonContractDefaults.SchemaVersion, StageOutcome.Blocked, [], sastStatus,
                ["SCA scan could not run (fail closed): " + scaError]);
        }

        var reasons = new List<string>();
        var active = new List<SecurityFinding>();
        foreach (SecurityFinding finding in raw)
        {
            SecuritySuppression? suppression = policy.Suppressions.FirstOrDefault(s => Suppresses(s, finding));
            if (suppression is not null && !Expired(suppression))
            {
                reasons.Add($"suppressed {finding.PackageId}@{finding.Version} [{suppression.Id}: {suppression.Justification}]");
                continue;
            }

            if (suppression is not null)
            {
                reasons.Add($"suppression '{suppression.Id}' expired ({suppression.Expiry}) — {finding.PackageId} NOT suppressed");
            }

            active.Add(finding);
        }

        int floor = SeverityLevels.Rank(policy.AuditFloor);
        SecurityFinding[] failing = active.Where(f => SeverityLevels.Rank(f.Severity) >= floor).ToArray();
        foreach (SecurityFinding f in failing)
        {
            reasons.Add($"vulnerable: {f.PackageId}@{f.Version} ({f.Severity}{(f.Transitive ? ", transitive" : string.Empty)}) >= floor {policy.AuditFloor}");
        }

        if (!sastEnforced)
        {
            reasons.Add("SAST not enforced at build: " + sastStatus);
        }

        StageOutcome outcome = failing.Length > 0 || !sastEnforced ? StageOutcome.Fail : StageOutcome.Pass;
        return new SecurityScanResult(JsonContractDefaults.SchemaVersion, outcome, active, sastStatus, reasons);
    }

    /// <summary>Verify the build-integrated SAST is enforced (we do not re-run the analyzers — the build does).</summary>
    public static (bool Enforced, string Status) CheckAnalyzerEnforcement(string repositoryRoot)
    {
        string propsPath = Path.Combine(repositoryRoot, "Directory.Build.props");
        if (!File.Exists(propsPath))
        {
            return (false, "Directory.Build.props missing");
        }

        XDocument document = XDocument.Load(propsPath);
        string? security = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "AnalysisModeSecurity")?.Value.Trim();
        string? warningsAsErrors = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "TreatWarningsAsErrors")?.Value.Trim();
        bool enforced = string.Equals(security, "All", StringComparison.OrdinalIgnoreCase)
            && string.Equals(warningsAsErrors, "true", StringComparison.OrdinalIgnoreCase);
        return enforced
            ? (true, "enforced at build: .NET security analyzers (CA3xxx/CA5xxx) as errors (AnalysisModeSecurity=All + TreatWarningsAsErrors)")
            : (false, $"AnalysisModeSecurity={security ?? "unset"}, TreatWarningsAsErrors={warningsAsErrors ?? "unset"}");
    }

    private static bool Suppresses(SecuritySuppression suppression, SecurityFinding finding) =>
        string.Equals(suppression.Id, finding.AdvisoryUrl, StringComparison.OrdinalIgnoreCase)
        || string.Equals(suppression.Id, finding.PackageId, StringComparison.OrdinalIgnoreCase);

    private static bool Expired(SecuritySuppression suppression) =>
        !string.IsNullOrWhiteSpace(suppression.Expiry)
        && DateOnly.TryParse(suppression.Expiry, out DateOnly expiry)
        && expiry < DateOnly.FromDateTime(DateTime.UtcNow);

    private static string? DiscoverSolution(string repositoryRoot, out string? error)
    {
        string[] solutions = Directory.GetFiles(repositoryRoot, "*.slnx");
        if (solutions.Length == 1)
        {
            error = null;
            return Path.GetFileName(solutions[0]);
        }

        error = solutions.Length == 0
            ? $"No .slnx solution found in {repositoryRoot}."
            : $"Multiple .slnx solutions found in {repositoryRoot}; expected exactly one.";
        return null;
    }
}
