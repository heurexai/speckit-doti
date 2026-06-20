namespace Hx.Tooling.Contracts;

/// <summary>A package-vulnerability finding from the SCA scan (post-suppression entries are kept for the proof).</summary>
public sealed record SecurityFinding(
    string PackageId,
    string Version,
    string Severity,
    string? AdvisoryUrl,
    bool Transitive);

/// <summary>
/// Proof for the security gate. SCA findings come from `dotnet list package --vulnerable` (the GitHub
/// Advisory DB); <see cref="SastStatus"/> reports the build-integrated analyzer enforcement (the build is
/// the SAST enforcement point — this is not a re-scan). <see cref="Outcome"/> is Pass / Fail (findings ≥
/// the policy floor, or SAST not enforced) / Blocked (the scan could not run — fail closed).
/// </summary>
public sealed record SecurityScanResult(
    int SchemaVersion,
    StageOutcome Outcome,
    IReadOnlyList<SecurityFinding> Vulnerabilities,
    string SastStatus,
    IReadOnlyList<string> Reasons);
