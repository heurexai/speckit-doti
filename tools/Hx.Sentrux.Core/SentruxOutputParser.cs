using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hx.Sentrux.Core;

/// <summary>
/// Parses Sentrux CLI output. The exact `check --json` field set and `gate` text
/// format must be confirmed against the pinned fork binary (task T01); parsing is
/// deliberately defensive so a field-name change degrades to "unknown" rather
/// than a crash.
/// </summary>
public static class SentruxOutputParser
{
    public sealed record CheckReport(bool Passed, int? QualitySignal, IReadOnlyList<string> Violations);

    public sealed record GateReport(int? SignalBefore, int? SignalAfter, bool Degraded);

    public static CheckReport ParseCheck(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CheckReport(false, null, []);
        }

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        bool passed = TryBool(root, "passed") ?? TryBool(root, "pass") ?? false;
        int? quality = ReadSignal(root, "quality", "qualitySignal", "quality_signal", "signal");

        List<string> violations = [];
        if (root.TryGetProperty("violations", out JsonElement v) && v.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in v.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    violations.Add(item.GetString() ?? string.Empty);
                }
                else if (item.ValueKind == JsonValueKind.Object)
                {
                    string rule = TryString(item, "rule") ?? "rule";
                    string message = TryString(item, "message") ?? "violation";
                    violations.Add($"[{rule}] {message}");
                }
            }
        }

        return new CheckReport(passed, quality, violations);
    }

    private static readonly Regex QualityArrow = new(
        @"Quality:\s*(\d+)\s*->\s*(\d+)", RegexOptions.Compiled);

    public static GateReport ParseGate(string stdout)
    {
        int? before = null;
        int? after = null;
        Match m = QualityArrow.Match(stdout ?? string.Empty);
        if (m.Success)
        {
            before = int.Parse(m.Groups[1].Value);
            after = int.Parse(m.Groups[2].Value);
        }

        bool degraded = (stdout ?? string.Empty).Contains("DEGRADED", StringComparison.OrdinalIgnoreCase);
        return new GateReport(before, after, degraded);
    }

    // Sentrux's internal signal is a 0..1 fraction surfaced as 0..10000 in the CLI;
    // accept either and normalize to the 0..10000 integer scale.
    private static int? ReadSignal(JsonElement root, params string[] names)
    {
        foreach (string name in names)
        {
            if (root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number)
            {
                double raw = value.GetDouble();
                return raw <= 1.0 ? (int)Math.Round(raw * 10000.0) : (int)Math.Round(raw);
            }
        }

        return null;
    }

    private static bool? TryBool(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
            ? v.GetBoolean()
            : null;

    private static string? TryString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
