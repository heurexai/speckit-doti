namespace Hx.Tooling.Contracts;

/// <summary>
/// Result of creating/refreshing the Sentrux baseline (`sentrux gate --save`).
/// Allowed automatically only on the first scaffold smoke; otherwise explicit.
/// </summary>
public sealed record SentruxBaselineResult(
    int SchemaVersion,
    StageOutcome Outcome,
    ToolVerificationResult Verification,
    int? QualitySignal,
    string BaselinePath,
    IReadOnlyList<string> Notes);
