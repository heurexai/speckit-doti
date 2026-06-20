using System.Text.Json;

namespace Hx.Runner.Core.ArchitectureGate;

/// <summary>Reads the family ids from a repo's <c>rules/architecture.json</c> (the human contract +
/// cross-engine reference). The runner does not interpret the rules — it reports the declared families
/// alongside the test results.</summary>
internal static class ArchitectureContract
{
    public const string RelativePath = "rules/architecture.json";

    public static IReadOnlyList<string> LoadFamilyIds(string repositoryRoot)
    {
        string path = Path.Combine(repositoryRoot, "rules", "architecture.json");
        if (!File.Exists(path))
        {
            return [];
        }

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("families", out JsonElement families))
        {
            return [];
        }

        var ids = new List<string>();
        foreach (JsonElement family in families.EnumerateArray())
        {
            if (family.TryGetProperty("id", out JsonElement id) && id.GetString() is string value)
            {
                ids.Add(value);
            }
        }

        return ids;
    }
}
