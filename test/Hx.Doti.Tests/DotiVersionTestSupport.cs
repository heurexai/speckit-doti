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

    public static string NewSource(string payloadVersion, bool includeConstitution = true) =>
        NewSource(payloadVersion, includeConstitution, includeTools: false);

    /// <summary>
    /// 032 D2: same fixture, optionally also seeding a minimal <c>tools/{gitleaks,sentrux,gitversion}</c> vendored-tool
    /// tree (manifest + a grammar file under <c>sentrux</c>, mirroring the real shape) so the D2(e)/(f)/(g) tests can
    /// exercise the vendored-tool reconcile/parity/advisory paths without touching the network or this repo's real
    /// (much larger) tool trees. <paramref name="includeTools"/> defaults false so every EXISTING caller of the
    /// 2-arg overload is byte-for-byte unaffected.
    /// </summary>
    public static string NewSource(string payloadVersion, bool includeConstitution, bool includeTools)
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

        if (includeTools)
        {
            WriteVendoredTools(repo, sentruxReleaseTag: "v0.5.12");
        }

        return repo;
    }

    /// <summary>
    /// 032 D2: a minimal vendored-tool tree under <c>tools/{gitleaks,sentrux,gitversion}</c> — a version-stamped
    /// manifest per tool, a grammar file under <c>sentrux</c> (mirrors the real shape exactly: manifest + grammar
    /// outside <c>bin/</c>), and a fake <c>bin/win-x64/&lt;tool&gt;.exe</c> per tool so D2(e)'s "never touch bin/"
    /// fence has something real to prove negative against.
    /// </summary>
    public static void WriteVendoredTools(string repo, string sentruxReleaseTag)
    {
        foreach (string tool in new[] { "gitleaks", "sentrux", "gitversion" })
        {
            string toolDir = Path.Combine(repo, "tools", tool);
            Directory.CreateDirectory(toolDir);
            File.WriteAllText(Path.Combine(toolDir, "LICENSE"), "MIT\n");
            File.WriteAllText(Path.Combine(toolDir, $"{tool}.version.json"),
                $$"""{ "schemaVersion": 1, "tool": "{{tool}}", "releaseTag": "{{(tool == "sentrux" ? sentruxReleaseTag : "v1.0.0")}}" }""");

            string binDir = Path.Combine(toolDir, "bin", "win-x64");
            Directory.CreateDirectory(binDir);
            File.WriteAllText(Path.Combine(binDir, $"{tool}.exe"), "FAKE-EXE-BYTES-" + tool);
        }

        // sentrux additionally vendors an in-repo grammar (no downloadUrl, so it lives outside bin/ like the real one).
        string grammarDir = Path.Combine(repo, "tools", "sentrux", "grammars", "csharp", "grammars");
        Directory.CreateDirectory(grammarDir);
        File.WriteAllText(Path.Combine(grammarDir, "windows-x86_64.dll"), "FAKE-GRAMMAR-BYTES");
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
