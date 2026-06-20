namespace Hx.Tooling.Contracts;

public static class AffectedOutcome
{
    /// <summary>A subset of test projects covers the change; run <see cref="AffectedPlan.SelectedTests"/>.</summary>
    public const string Affected = "affected";

    /// <summary>Only documentation/generated paths changed; no tests are required.</summary>
    public const string NoTestsRequired = "no-tests-required";

    /// <summary>The change could not be narrowed safely (broad/unattributed input, or graph drift); run the full suite.</summary>
    public const string FullGateRequired = "full-gate-required";
}

/// <summary>A test project selected for an affected change: its name, repo-relative path, and the exact command to run it.</summary>
public sealed record SelectedTest(string TestProject, string ProjectPath, string Command);

/// <summary>
/// The deterministic affected-test plan. <see cref="Outcome"/> is one of <see cref="AffectedOutcome"/>;
/// <c>full-gate-required</c> is the fail-closed escalation (never an under-selection). When
/// <c>affected</c>, <see cref="SelectedTests"/> are the only test projects that must run (plus any new
/// tests, which are themselves changed test files and so appear here via their owning test project).
/// </summary>
public sealed record AffectedPlan(
    int SchemaVersion,
    string Outcome,
    IReadOnlyList<string> AffectedSourceProjects,
    IReadOnlyList<SelectedTest> SelectedTests,
    IReadOnlyList<string> Reasons);
