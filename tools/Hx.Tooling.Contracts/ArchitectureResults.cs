namespace Hx.Tooling.Contracts;

/// <summary>JSON proof for <c>architecture test</c>: per-`[Fact]` results plus the families
/// declared in <c>rules/architecture.json</c> (the enforced contract).</summary>
public sealed record ArchitectureTestResult(
    int SchemaVersion,
    StageOutcome Outcome,
    int TestCount,
    int PassedCount,
    int FailedCount,
    IReadOnlyList<ArchitectureTestCase> Tests,
    IReadOnlyList<string> Families,
    IReadOnlyList<string> Notes);

/// <summary>014 (FR-002): per-`[Fact]` result. <see cref="Violations"/> carries the ArchUnitNET offender detail
/// (the failing rule's description + violating objects) for a FAILING case — additive nullable (M2), null/empty on a
/// pass and on a pre-014 proof. Render-only/visibility (FR-007): it lives on this standalone result + the
/// <c>GateTrace</c> envelope, NEVER on the hashed gate proof.</summary>
public sealed record ArchitectureTestCase(
    string Name,
    StageOutcome Outcome,
    IReadOnlyList<ArchitectureViolation>? Violations = null);

/// <summary>014 (FR-001/005): one failing ArchUnitNET rule with its description and the violating objects
/// (types/namespaces at the granularity the engine reports — never fabricated). When the runner cannot recover the
/// detail from a failing test's message, <see cref="UnknownReason"/> is set fail-closed (FR-005) rather than implying
/// "no violation".</summary>
public sealed record ArchitectureViolation(
    string Rule,
    string Description,
    IReadOnlyList<string> ViolatingObjects,
    string? UnknownReason = null);
