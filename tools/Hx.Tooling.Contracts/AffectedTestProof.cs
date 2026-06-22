namespace Hx.Tooling.Contracts;

/// <summary>A test project actually executed by the gate's affected-test lane.</summary>
public sealed record ExecutedTestProject(
    string TestProject,
    string ProjectPath,
    string Command,
    int ExitCode,
    StageOutcome Outcome);

/// <summary>
/// The gate's affected-test proof. It stores the recomputable planner hash, selected test-scope hash,
/// and executed test hash so <c>doti cycle commit</c> can reject hand-written test transcripts or direct
/// <c>dotnet test</c> runs that did not come from the gate.
/// </summary>
public sealed record AffectedTestProof(
    int SchemaVersion,
    string BaseRef,
    string HeadRef,
    string Configuration,
    string PlanHash,
    string TestScopeHash,
    string ExecutedTestsHash,
    bool FullSuite,
    string? FullSuiteReason,
    AffectedPlan Plan,
    IReadOnlyList<ExecutedTestProject> ExecutedTests);
