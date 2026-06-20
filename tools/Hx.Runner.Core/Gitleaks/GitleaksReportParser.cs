using System.Text.Json;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Core.Gitleaks;

/// <summary>
/// Parses Gitleaks JSON report output into redacted <see cref="HygieneFinding"/>
/// records. Secret values (<c>Secret</c>, <c>Match</c>) are never carried forward;
/// only rule id, description, line, fingerprint, and repo-relative path remain.
/// </summary>
public static class GitleaksReportParser
{
    /// <param name="pathRemap">
    /// Maps a Gitleaks-reported file path (which may point at a temporary scan
    /// root) back to a repo-relative <c>/</c> path.
    /// </param>
    public static IReadOnlyList<HygieneFinding> Parse(string reportJson, Func<string, string> pathRemap)
    {
        if (string.IsNullOrWhiteSpace(reportJson))
        {
            return [];
        }

        using JsonDocument document = JsonDocument.Parse(reportJson);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<HygieneFinding> findings = [];
        foreach (JsonElement element in document.RootElement.EnumerateArray())
        {
            string ruleId = GetString(element, "RuleID") ?? "gitleaks";
            string description = GetString(element, "Description") ?? "Potential secret detected by Gitleaks.";
            string file = GetString(element, "File") ?? string.Empty;
            string? fingerprint = GetString(element, "Fingerprint");
            int? line = GetInt(element, "StartLine");

            findings.Add(new HygieneFinding(
                HygieneFindingCategory.Secret,
                HygieneSeverity.Error,
                ruleId,
                pathRemap(file.Replace('\\', '/')),
                line,
                $"{description} (secret value redacted)",
                fingerprint));
        }

        return findings;
    }

    private static string? GetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? GetInt(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;
    }
}
