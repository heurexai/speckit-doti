using System.Text.Json;

namespace Hx.Cycle.Core;

/// <summary>
/// FR-030 bypass-safety: when a repo's tier <b>enforces</b> an opinionated gate, that gate's configuration MUST
/// be present and parseable. A missing or malformed config is a <c>Fail</c> — not a silent <c>Skip</c> — so an
/// enforced gate cannot be bypassed by deleting or corrupting its config (the delete-config / malformed-config
/// bypass). The <c>gate run</c> step maps this to <c>Integrity_ProfileGateMissingConfig</c>. A gate the tier
/// does not enforce (Advisory/Skip) is unaffected — this guard only applies to the Enforced mode.
/// </summary>
public static class GateConfigRequirements
{
    // The config each opinionated gate requires when its tier enforces it.
    private static readonly IReadOnlyDictionary<string, string[]> Required =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["sentrux-verify"] = [".sentrux/rules.toml"],
            ["sentrux-check"] = [".sentrux/rules.toml"],
            ["architecture-test"] = ["rules/architecture.json"],
        };

    /// <summary>Returns the offending config (with a reason) when an enforced gate's config is missing or
    /// malformed, or null when the gate is satisfied (or carries no required config).</summary>
    public static string? MissingOrMalformedConfig(string repositoryRoot, string step)
    {
        if (!Required.TryGetValue(step, out string[]? configs))
        {
            return null;
        }

        foreach (string relativePath in configs)
        {
            string full = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
            {
                return $"{relativePath} (missing)";
            }

            if (new FileInfo(full).Length == 0)
            {
                return $"{relativePath} (empty)";
            }

            if (relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && !IsParseableJson(full))
            {
                return $"{relativePath} (malformed)";
            }
        }

        return null;
    }

    private static bool IsParseableJson(string path)
    {
        try
        {
            using JsonDocument _ = JsonDocument.Parse(File.ReadAllText(path));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
