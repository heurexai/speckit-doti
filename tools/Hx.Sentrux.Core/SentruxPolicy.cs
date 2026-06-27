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
    IReadOnlyList<string> RequiredGrammars,
    // 008 FR-029: the source scope is CODE — these extensions (matching `requiredGrammars: ["csharp"]`) plus an
    // explicit "configured as code" path list; everything else (prose, docs) is out of scope. Null on a pre-FR-029
    // policy ⇒ the built-in defaults below.
    IReadOnlyList<string>? CodeExtensions = null,
    IReadOnlyList<string>? ConfiguredAsCode = null,
    // 008 FR-030: the escalation band multiplier (1.3 = the 130 band): a quality-signal deviation above the hard
    // tolerance but within (toleranceBand * multiplier) is EscalationBand (two optimization tries) rather than Fail.
    double? EscalationBandMultiplier = null)
{
    public IReadOnlyList<string> EffectiveCodeExtensions => CodeExtensions is { Count: > 0 } ? CodeExtensions : [".cs", ".csproj"];

    public IReadOnlyList<string> EffectiveConfiguredAsCode => ConfiguredAsCode ?? [];

    public double EffectiveEscalationBandMultiplier => EscalationBandMultiplier is > 1.0 ? EscalationBandMultiplier.Value : 1.3;

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
