using System.Text.Json;
using System.Text.RegularExpressions;
using Hx.Tooling.Contracts;

namespace Hx.Sentrux.Core;

/// <summary>
/// Parses Sentrux CLI output. The exact `check --json` field set and `gate` text
/// format must be confirmed against the pinned fork binary (task T01); parsing is
/// deliberately defensive so a field-name change degrades to "unknown" rather
/// than a crash.
/// </summary>
public static class SentruxOutputParser
{
    // 014 (FR-003): the report carries BOTH the legacy flattened <see cref="Violations"/> string list (unchanged) and
    // the structured <see cref="ViolationDetails"/> projected from the SAME parsed objects — render-only offender
    // detail, never a proof input.
    public sealed record CheckReport(
        bool Passed,
        int? QualitySignal,
        IReadOnlyList<string> Violations,
        IReadOnlyList<SentruxViolation> ViolationDetails);

    public sealed record GateReport(int? SignalBefore, int? SignalAfter, bool Degraded);

    public static CheckReport ParseCheck(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CheckReport(false, null, [], []);
        }

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        bool passed = TryBool(root, "passed") ?? TryBool(root, "pass") ?? false;
        int? quality = ReadSignal(root, "quality", "qualitySignal", "quality_signal", "signal");
        (IReadOnlyList<string> flat, IReadOnlyList<SentruxViolation> structured) = ReadViolations(root);
        return new CheckReport(passed, quality, flat, structured);
    }

    private static (IReadOnlyList<string> Flat, IReadOnlyList<SentruxViolation> Structured) ReadViolations(JsonElement root)
    {
        List<string> violations = [];
        List<SentruxViolation> details = [];
        if (root.TryGetProperty("violations", out JsonElement v) && v.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in v.EnumerateArray())
            {
                string? violation = FormatViolation(item);
                if (violation is not null)
                {
                    violations.Add(violation);
                    details.Add(StructureViolation(item));
                }
            }
        }

        return (violations, details);
    }

    private static string? FormatViolation(JsonElement item) => item.ValueKind switch
    {
        JsonValueKind.String => item.GetString() ?? string.Empty,
        JsonValueKind.Object => FormatObjectViolation(item),
        _ => null,
    };

    // 014 (FR-003/005): project the SAME parsed fields into a structured offender. A string-only (non-object)
    // violation has no per-function attribution → UnknownReason "unstructured engine violation"; an object with no
    // path/file (e.g. a max_cc summary message) leaves File/Function null → UnknownReason "summary-level violation
    // without per-function location". Never zero/fabricated.
    private static SentruxViolation StructureViolation(JsonElement item) => item.ValueKind switch
    {
        JsonValueKind.String => new SentruxViolation(
            "rule", null, null, null, null, null, item.GetString() ?? string.Empty,
            "unstructured engine violation"),
        JsonValueKind.Object => StructureObjectViolation(item),
        _ => new SentruxViolation("rule", null, null, null, null, null, null, "unrecognized engine violation shape"),
    };

    private static SentruxViolation StructureObjectViolation(JsonElement item)
    {
        string rule = TryString(item, "rule") ?? "rule";
        string? message = TryString(item, "message");
        string? file = TryString(item, "path") ?? TryString(item, "file") ?? TryString(item, "source");
        string? function = TryString(item, "function") ?? TryString(item, "member") ?? TryString(item, "symbol");
        int? line = TryInt(item, "line") ?? TryInt(item, "startLine");
        string? measured = TryMeasure(item, "value", "measured", "actual");
        string? limit = TryMeasure(item, "limit", "threshold", "max");

        // FR-005: a summary-level structural rule (no path AND no function) has no offender location — surface the
        // unknown with its reason rather than implying a clean location.
        string? unknownReason = string.IsNullOrWhiteSpace(file) && string.IsNullOrWhiteSpace(function)
            ? "engine reported a summary-level violation without per-function location"
            : null;

        return new SentruxViolation(rule, file, function, line, measured, limit, message, unknownReason);
    }

    // A measured value or limit may arrive as a number or a string; surface it as a stable string either way.
    private static string? TryMeasure(JsonElement item, params string[] names)
    {
        foreach (string name in names)
        {
            if (!item.TryGetProperty(name, out JsonElement value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                return value.GetRawText();
            }
        }

        return null;
    }

    private static string FormatObjectViolation(JsonElement item)
    {
        string rule = TryString(item, "rule") ?? "rule";
        string message = TryString(item, "message") ?? "violation";
        string? path = TryString(item, "path") ?? TryString(item, "file") ?? TryString(item, "source");
        int? line = TryInt(item, "line") ?? TryInt(item, "startLine");
        string? detail = TryString(item, "detail") ?? TryString(item, "details") ?? TryString(item, "help");
        string? recommendation = TryString(item, "recommendation") ?? TryString(item, "remediation");

        List<string> parts = [$"[{rule}] {message}"];
        if (!string.IsNullOrWhiteSpace(path))
        {
            parts.Add(line is null ? path : $"{path}:{line}");
        }

        if (!string.IsNullOrWhiteSpace(detail))
        {
            parts.Add(detail);
        }

        if (!string.IsNullOrWhiteSpace(recommendation))
        {
            parts.Add("next: " + recommendation);
        }

        return string.Join(" - ", parts);
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

    private static int? TryInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int value)
            ? value
            : null;
}
