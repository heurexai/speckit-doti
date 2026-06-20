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
    IReadOnlyList<string> AdvisoryGaps);
