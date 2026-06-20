namespace Hx.Tooling.Contracts;

/// <summary>JSON proof for <c>architecture test</c>: per-`[Fact]` results plus the eight families
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

public sealed record ArchitectureTestCase(string Name, StageOutcome Outcome);
