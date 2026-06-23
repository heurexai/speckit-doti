using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

public static class CycleRecoveryVerdict
{
    public const string None = "none";
    public const string Completed = "completed";
    public const string RetryableActive = "retryable-active";
    public const string Ambiguous = "ambiguous";
}

/// <summary>Recovery verdict for a pending sanctioned commit intent, reported by cycle commands.</summary>
public sealed record CycleRecoveryReport(string Verdict, string? Reason, CycleCompletionRecord? Completion = null);

/// <summary>The <c>doti cycle status</c> output: the persisted state plus a freshness verdict per stamped stage.</summary>
public sealed record CycleStatusReport(
    int SchemaVersion,
    CycleState State,
    IReadOnlyList<StageFreshnessResult> Freshness,
    CycleCompletionRecord? Completion = null,
    CycleRecoveryReport? Recovery = null);

/// <summary>One prerequisite's verdict in a <c>cycle check</c>: the stage, its status (fresh|stale|missing|invalid), and a reason.</summary>
public sealed record StagePrereqResult(string Stage, string Status, bool Ok, string? Reason);

/// <summary>The <c>doti cycle check</c> output: the checked stage, whether all prerequisites passed, and the per-prereq detail.</summary>
public sealed record CycleCheckReport(
    int SchemaVersion,
    string Stage,
    bool Passed,
    IReadOnlyList<StagePrereqResult> Prerequisites,
    CycleCompletionRecord? Completion = null,
    CycleRecoveryReport? Recovery = null);

/// <summary>The <c>doti cycle commit</c> output: whether the commit was performed, the sha if so, and the refusal reasons if not.</summary>
public sealed record CycleCommitResult(
    int SchemaVersion,
    bool Committed,
    string? CommitSha,
    IReadOnlyList<string> Reasons,
    bool AlreadyCompleted = false,
    bool CompletionPersistenceFailed = false,
    CycleCompletionRecord? Completion = null,
    CycleRecoveryReport? Recovery = null);
