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
    CycleRecoveryReport? Recovery = null,
    CycleReleaseTrain? ReleaseTrain = null);

/// <summary>One prerequisite's verdict in a <c>cycle check</c>: the stage, its status (fresh|stale|missing|invalid),
/// a reason, and — when stale — the machine-readable <see cref="FreshnessEvaluator"/> category so a recovery
/// projection can classify its restamp-safety without re-evaluating. <see cref="ChangedPrereqPaths"/> (028 FR-002)
/// carries the prerequisite artifact paths whose content diverged (when the stale reason is prerequisite-driven), so
/// the self-describing recovery seam can surface the exact "what changed" set + a line-level diff. Null otherwise.</summary>
public sealed record StagePrereqResult(
    string Stage,
    string Status,
    bool Ok,
    string? Reason,
    StaleReason? StaleReason = null,
    IReadOnlyList<string>? ChangedPrereqPaths = null);

/// <summary>One stage in a <see cref="CycleRecoveryPlan"/>: its status + reason, the restamp-safety verdict (null
/// when the stage is not stale, e.g. missing/invalid), the stage's own re-run command, and the single recommended
/// next command (a safe refresh, the agent-gated reviewed-no-impact rebind, or re-running the stage).
/// <see cref="ChangedPrereqPaths"/> (028 FR-002) carries the changed prerequisite artifact paths — the self-describing
/// delta the CLI/recovery seam renders (and diffs line-level). Null when the step is not prerequisite-content-driven.</summary>
public sealed record StageRecoveryStep(
    string Stage,
    string Status,
    string? Reason,
    RestampSafety? Safety,
    string RequiredRerun,
    string NextCommand,
    IReadOnlyList<string>? ChangedPrereqPaths = null);

/// <summary>The <c>doti cycle refresh-plan</c> projection: the per-stage recovery steps for a target, and whether
/// <c>doti cycle refresh --apply-safe</c> would FULLY recover it (every blocking step is a safe re-interpret).</summary>
public sealed record CycleRecoveryPlan(
    int SchemaVersion,
    string Target,
    bool Recoverable,
    IReadOnlyList<StageRecoveryStep> Steps);

/// <summary>The <c>doti cycle refresh</c> outcome: which stale stages were safely re-stamped, and which steps still
/// require operator action (re-run / produce the artifact).</summary>
public sealed record CycleRefreshResult(
    int SchemaVersion,
    string Target,
    bool Applied,
    IReadOnlyList<string> Refreshed,
    IReadOnlyList<StageRecoveryStep> Remaining);

/// <summary>The <c>doti cycle check</c> output: the checked stage, whether all prerequisites passed, and the per-prereq detail.</summary>
public sealed record CycleCheckReport(
    int SchemaVersion,
    string Stage,
    bool Passed,
    IReadOnlyList<StagePrereqResult> Prerequisites,
    CycleCompletionRecord? Completion = null,
    CycleRecoveryReport? Recovery = null,
    CycleReleaseTrain? ReleaseTrain = null);
