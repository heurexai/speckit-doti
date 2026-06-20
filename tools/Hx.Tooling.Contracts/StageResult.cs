namespace Hx.Tooling.Contracts;

public sealed record StageResult(
    int SchemaVersion,
    string Name,
    StageOutcome Outcome,
    IReadOnlyList<GateEvidence> Evidence,
    string? Message = null)
{
    public static StageResult Pass(string name, params GateEvidence[] evidence)
    {
        return new StageResult(JsonContractDefaults.SchemaVersion, name, StageOutcome.Pass, evidence);
    }
}
