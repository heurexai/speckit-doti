using System.Text.Json;
using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

/// <summary>One feature's Sentrux optimization-attempt tally (FR-030).</summary>
public sealed record SentruxOptimizationEntry(string Feature, int Attempts, string LastVerdict);

/// <summary>The two-try optimization log — gate-execution telemetry kept beside (not inside) the cycle state.</summary>
public sealed record SentruxOptimizationLog(int SchemaVersion, IReadOnlyList<SentruxOptimizationEntry> Entries);

public enum SentruxOptimizationVerdict
{
    Cleared,
    AttemptRecorded,
    StructuralReviewRequired,
}

public sealed record SentruxOptimizationResult(SentruxOptimizationVerdict Verdict, int Attempts, string? NextAction);

/// <summary>
/// FR-030/SC-014: the two-optimization-try diagnostic. When a gate run leaves Sentrux in the ESCALATION BAND (above
/// tolerance but within 1.3×), the agent gets two documented optimization attempts; a non-band verdict (pass, or a
/// hard fail) clears the tally. After the second band attempt the next action is a STRUCTURAL architecture review —
/// not another blind optimization pass. Kept as a separate store (gate-execution telemetry, not a stage proof —
/// M-6), keyed by the FEATURE (stable across optimization edits, unlike the working-tree change-set identity).
/// String verdict in/out so this stays free of a Hx.Sentrux.Core dependency.
/// </summary>
public static class SentruxOptimizationTracker
{
    public const int MaxOptimizationAttempts = 2;
    public const string EscalationBandVerdict = "escalation-band";

    public static SentruxOptimizationResult Record(string repositoryRoot, string feature, string regressionVerdict)
    {
        var store = new SentruxOptimizationLogStore(repositoryRoot);
        SentruxOptimizationLog log = store.Read() ?? new SentruxOptimizationLog(JsonContractDefaults.SchemaVersion, []);
        int prior = log.Entries.FirstOrDefault(e => string.Equals(e.Feature, feature, StringComparison.Ordinal))?.Attempts ?? 0;

        if (!string.Equals(regressionVerdict, EscalationBandVerdict, StringComparison.Ordinal))
        {
            // A non-band verdict (pass / hard fail / blocked) ends the two-try window — reset the tally.
            store.Write(log with { Entries = Without(log.Entries, feature) });
            return new SentruxOptimizationResult(SentruxOptimizationVerdict.Cleared, 0, null);
        }

        int attempts = prior + 1;
        store.Write(log with
        {
            Entries = [.. Without(log.Entries, feature), new SentruxOptimizationEntry(feature, attempts, regressionVerdict)],
        });

        return attempts >= MaxOptimizationAttempts
            ? new SentruxOptimizationResult(SentruxOptimizationVerdict.StructuralReviewRequired, attempts,
                $"{attempts} optimization attempts stayed in the Sentrux escalation band — STOP blind optimization; run a structural architecture review (/04-doti-arch-review) to decide: functionality-driven growth (evidence-gated rebaseline) vs wrong architecture (refactor).")
            : new SentruxOptimizationResult(SentruxOptimizationVerdict.AttemptRecorded, attempts,
                $"Sentrux escalation band, attempt {attempts}/{MaxOptimizationAttempts} — one more optimization attempt is allowed before a structural architecture review is required.");
    }

    private static IReadOnlyList<SentruxOptimizationEntry> Without(IReadOnlyList<SentruxOptimizationEntry> entries, string feature) =>
        entries.Where(e => !string.Equals(e.Feature, feature, StringComparison.Ordinal)).ToList();
}

/// <summary>Reads/writes the gitignored Sentrux optimization log at <c>.doti/sentrux-optimization-log.json</c>.</summary>
public sealed class SentruxOptimizationLogStore
{
    public const string RelativePath = ".doti/sentrux-optimization-log.json";

    private readonly string _path;

    public SentruxOptimizationLogStore(string repositoryRoot) =>
        _path = Path.GetFullPath(Path.Combine(repositoryRoot, ".doti", "sentrux-optimization-log.json"));

    public SentruxOptimizationLog? Read() =>
        File.Exists(_path)
            ? JsonSerializer.Deserialize<SentruxOptimizationLog>(File.ReadAllText(_path), JsonContractSerializerOptions.Create())
            : null;

    public void Write(SentruxOptimizationLog log)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        JsonSerializerOptions options = JsonContractSerializerOptions.Create();
        options.WriteIndented = true;
        File.WriteAllText(_path, JsonSerializer.Serialize(log, options));
    }
}
