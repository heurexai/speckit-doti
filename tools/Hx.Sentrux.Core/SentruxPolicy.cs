namespace Hx.Sentrux.Core;

/// <summary>
/// Runner-owned Sentrux policy (`rules/sentrux.json`) — invocation/verification
/// only. The native architecture config (`.sentrux/rules.toml`) is authored
/// directly and is NOT rendered from this.
/// </summary>
public sealed record SentruxPolicy(
    int SchemaVersion,
    bool SentruxEnabled,
    string BaselinePath,
    string RulesConfigPath,
    int SignalToleranceBand,
    string ForkStamp,
    bool FirstSmokeBaseline,
    IReadOnlyList<string> RequiredFeatures,
    IReadOnlyList<string> RequiredGrammars)
{
    public static SentruxPolicy Default()
    {
        return new SentruxPolicy(
            SchemaVersion: 1,
            SentruxEnabled: true,
            BaselinePath: ".sentrux/baseline.json",
            RulesConfigPath: ".sentrux/rules.toml",
            SignalToleranceBand: 100,
            ForkStamp: "Heurex fork",
            FirstSmokeBaseline: true,
            RequiredFeatures: ["check-include-untracked", "gate-save"],
            RequiredGrammars: ["csharp"]);
    }
}
