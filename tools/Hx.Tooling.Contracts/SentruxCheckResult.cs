namespace Hx.Tooling.Contracts;

/// <summary>
/// Merged Sentrux gate proof: tool verification + absolute rule check +
/// quality-signal regression against the committed baseline (tolerance band).
/// </summary>
public sealed record SentruxCheckResult(
    int SchemaVersion,
    StageOutcome Outcome,
    ToolVerificationResult Verification,
    StageOutcome RulesOutcome,
    IReadOnlyList<string> RuleViolations,
    int? QualitySignal,
    int? BaselineSignal,
    int? SignalDelta,
    int SignalToleranceBand,
    StageOutcome RegressionOutcome,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> AdvisoryGaps,
    // 008 FR-030: the three-state quality-signal verdict (pass | escalation-band | fail). EscalationBand fails the
    // gate closed (RegressionOutcome=Fail) but the cycle's two-try log reads THIS to count optimization attempts.
    // "pass" on a pre-FR-030 proof.
    string RegressionVerdict = "pass",
    // 014 (FR-003): the STRUCTURED offender detail per rule violation (file/function/line/value where the engine
    // emits them), parallel to the flattened <see cref="RuleViolations"/> string list (UNCHANGED). Additive nullable
    // (M2) — null on a pre-014 proof. Render-only/visibility (FR-007): surfaced on this standalone result + the
    // <c>GateTrace</c> envelope, NEVER on the hashed gate proof.
    IReadOnlyList<SentruxViolation>? RuleViolationDetails = null);

/// <summary>014 (FR-003/005): one structured Sentrux rule violation. Fields present per what the engine emits — a
/// summary-level rule with no per-function attribution (the observed <c>max_cc</c> message, a whole-graph structural
/// signal) leaves <see cref="File"/>/<see cref="Function"/>/<see cref="Line"/> null and sets
/// <see cref="UnknownReason"/> (FR-005), never zero or a fabricated location.</summary>
public sealed record SentruxViolation(
    string Rule,
    string? File,
    string? Function,
    int? Line,
    string? MeasuredValue,
    string? Limit,
    string? Message,
    string? UnknownReason = null);
