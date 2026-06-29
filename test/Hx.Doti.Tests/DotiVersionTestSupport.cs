using System.Text.Json;
using Hx.Tooling.Contracts;

namespace Hx.Doti.Tests;

/// <summary>
/// 022: shared fixtures for the version-lifecycle tests — a temp bundled payload source (with a verbatim payload
/// descriptor) and a temp target repo, mirroring <see cref="DotiReconciliationTests"/>. A source can optionally
/// OMIT the constitution from its <c>.doti/memory</c> (the real shipped payload excludes the repo's own
/// constitution), so an update can be shown to never overwrite an operator-owned constitution.
/// </summary>
internal static class DotiVersionTestSupport
{
    public const string SkillsJson =
        """
        {
          "schemaVersion": 1,
          "maturity": "command-aware-advisory",
          "commandTemplateDir": ".doti/core/templates/commands",
          "agentContextRef": ".doti/agent-context.md",
          "introTemplate": "Read `{agentContextRef}`, then follow `{commandTemplate}`.",
          "skills": [
            { "name": "doti-specify", "description": "Spec.", "argumentHint": "[goal]", "highlights": [], "nextStage": "Run `/doti-clarify`." }
          ]
        }
        """;

    private const string ProfileJson =
        """
        { "selfHostingStatus": { "commandAvailabilityFootnote": "Footnote text.", "rootMaturityNote": "Maturity note." } }
        """;

    public static readonly string SkillsRelative = Path.Combine(".doti", "core", "skills.json");
    public static readonly string ConstitutionRelative = Path.Combine(".doti", "memory", "constitution.md");

    public static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-doti-ver-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string NewSource(string payloadVersion, bool includeConstitution = true)
    {
        string repo = NewTempDir();
        Directory.CreateDirectory(Path.Combine(repo, ".doti", "core", "templates", "commands"));
        Directory.CreateDirectory(Path.Combine(repo, ".doti", "profiles", "dotnet-cli"));
        Directory.CreateDirectory(Path.Combine(repo, ".doti", "workflows", "doti"));
        Directory.CreateDirectory(Path.Combine(repo, ".doti", "memory"));
        Directory.CreateDirectory(Path.Combine(repo, ".doti", "integrations"));
        File.WriteAllText(Path.Combine(repo, ".doti", "core", "skills.json"), SkillsJson);
        File.WriteAllText(Path.Combine(repo, ".doti", "profiles", "dotnet-cli", "profile.json"), ProfileJson);
        File.WriteAllText(Path.Combine(repo, ".doti", "core", "templates", "agent-context-template.md"), "context body\n");
        File.WriteAllText(Path.Combine(repo, ".doti", "core", "templates", "commands", "doti-specify.md"), "# specify\n");
        File.WriteAllText(Path.Combine(repo, ".doti", "workflows", "doti", "workflow.yml"),
            "schemaVersion: 2\nstages:\n  - id: specify\n    prereqs: []\n");
        if (includeConstitution)
        {
            File.WriteAllText(Path.Combine(repo, ".doti", "memory", "constitution.md"), "# Constitution\n");
        }
        else
        {
            // Keep memory non-empty without shipping a constitution (mirrors the real payload).
            File.WriteAllText(Path.Combine(repo, ".doti", "memory", "memory.md"), "# Memory\n");
        }

        File.WriteAllText(Path.Combine(repo, ".doti", "integrations", "doti.manifest.json"), "{}\n");

        var descriptor = new PayloadDescriptor(
            PayloadDescriptor.CurrentSchemaVersion, payloadVersion, payloadVersion,
            DistributionChannelId.GlobalTool, CommandMode.Installed, []);
        File.WriteAllText(Path.Combine(repo, "payload.manifest.json"),
            JsonSerializer.Serialize(descriptor, JsonContractSerializerOptions.Create()));
        return repo;
    }

    public static void ForceDelete(string dir)
    {
        if (!Directory.Exists(dir))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            try { File.SetAttributes(file, FileAttributes.Normal); }
            catch { /* best-effort */ }
        }

        try { Directory.Delete(dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
