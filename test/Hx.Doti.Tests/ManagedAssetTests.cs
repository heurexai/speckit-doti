using Hx.Doti.Core;
using Hx.Doti.Core.ManagedAssets;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Doti.Tests;

public sealed class ManagedAssetTests
{
    private const string SkillsJson =
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

    [Fact]
    public void JsonSemanticHashIgnoresPropertyOrder_ButRejectsDuplicateKeys()
    {
        string dir = NewTempDir();
        try
        {
            string a = Path.Combine(dir, "a.json");
            string b = Path.Combine(dir, "b.json");
            File.WriteAllText(a, "{ \"b\": 2, \"a\": [true, null] }\n");
            File.WriteAllText(b, "{\n  \"a\": [ true, null ],\n  \"b\": 2\n}\n");

            Assert.Equal(
                CanonicalContentHasher.HashFile(a, HashProfile.JsonSemantic).Sha256,
                CanonicalContentHasher.HashFile(b, HashProfile.JsonSemantic).Sha256);

            string duplicate = Path.Combine(dir, "duplicate.json");
            File.WriteAllText(duplicate, "{ \"a\": 1, \"a\": 2 }");
            Assert.Throws<InvalidOperationException>(() =>
                CanonicalContentHasher.HashFile(duplicate, HashProfile.JsonSemantic));
        }
        finally
        {
            DeleteTemp(dir);
        }
    }

    [Fact]
    public void YamlSemanticHashIgnoresPresentationWhitespace_ButDetectsContentChange()
    {
        string dir = NewTempDir();
        try
        {
            string a = Path.Combine(dir, "a.yml");
            string b = Path.Combine(dir, "b.yml");
            string c = Path.Combine(dir, "c.yml");
            File.WriteAllText(a, "schemaVersion: 2\nstages:\n  - id: specify\n    prereqs: []\n");
            File.WriteAllText(b, "stages:\n- prereqs: []\n  id: specify\nschemaVersion: 2\n");
            File.WriteAllText(c, "schemaVersion: 2\nstages:\n  - id: clarify\n    prereqs: []\n");

            Assert.Equal(
                CanonicalContentHasher.HashFile(a, HashProfile.YamlSemantic).Sha256,
                CanonicalContentHasher.HashFile(b, HashProfile.YamlSemantic).Sha256);
            Assert.NotEqual(
                CanonicalContentHasher.HashFile(a, HashProfile.YamlSemantic).Sha256,
                CanonicalContentHasher.HashFile(c, HashProfile.YamlSemantic).Sha256);
        }
        finally
        {
            DeleteTemp(dir);
        }
    }

    [Fact]
    public void NormalizedTextHashIgnoresWhitespaceRuns_ButPreservesTokenBoundaries()
    {
        string dir = NewTempDir();
        try
        {
            string a = Path.Combine(dir, "a.md");
            string b = Path.Combine(dir, "b.md");
            string c = Path.Combine(dir, "c.md");
            File.WriteAllText(a, "# Skill\r\n\r\nRead the context.\r\n");
            File.WriteAllText(b, "#   Skill\nRead\t the   context.\n");
            File.WriteAllText(c, "# Skill\nReadthe context.\n");

            Assert.Equal(
                CanonicalContentHasher.HashFile(a, HashProfile.NormalizedText).Sha256,
                CanonicalContentHasher.HashFile(b, HashProfile.NormalizedText).Sha256);
            Assert.NotEqual(
                CanonicalContentHasher.HashFile(a, HashProfile.NormalizedText).Sha256,
                CanonicalContentHasher.HashFile(c, HashProfile.NormalizedText).Sha256);
        }
        finally
        {
            DeleteTemp(dir);
        }
    }

    [Fact]
    public void ScanReportsTemplateAndGeneratedSkillModificationsSeparately()
    {
        string repo = NewDotiRepo();
        try
        {
            DotiRenderer.Render(repo, DotiAgentTarget.All, check: false);
            ManagedAssetScanner.WriteBaseline(repo, DotiRenderer.BuildTargets(repo, DotiAgentTarget.All));

            string workflow = Path.Combine(repo, ".doti", "workflows", "doti", "workflow.yml");
            File.WriteAllText(workflow, "stages:\n- prereqs: []\n  id: specify\nschemaVersion: 2\n");
            ManagedAssetScanResult whitespaceOnly = ManagedAssetScanner.Scan(repo);
            Assert.Equal(StageOutcome.Pass.ToString().ToLowerInvariant(), whitespaceOnly.Outcome);

            File.WriteAllText(workflow, "schemaVersion: 2\nstages:\n  - id: clarify\n    prereqs: []\n");
            string codexSkill = Path.Combine(repo, ".agents", "skills", "01-doti-specify", "SKILL.md");
            File.AppendAllText(codexSkill, "\nlocal customization\n");

            ManagedAssetScanResult scan = ManagedAssetScanner.Scan(repo);

            Assert.Equal(StageOutcome.Fail.ToString().ToLowerInvariant(), scan.Outcome);
            Assert.Contains(scan.ModifiedWorkflowTemplates, s => s.Path == ".doti/workflows/doti/workflow.yml");
            Assert.Contains(scan.ModifiedSkillGeneratedInstructions, s => s.Path == ".agents/skills/01-doti-specify/SKILL.md");
        }
        finally
        {
            DeleteTemp(repo);
        }
    }

    private static string NewDotiRepo()
    {
        string repo = NewTempDir();
        Directory.CreateDirectory(Path.Combine(repo, ".doti", "core", "templates", "commands"));
        Directory.CreateDirectory(Path.Combine(repo, ".doti", "profiles", "dotnet-cli"));
        Directory.CreateDirectory(Path.Combine(repo, ".doti", "workflows", "doti"));
        File.WriteAllText(Path.Combine(repo, ".doti", "core", "skills.json"), SkillsJson);
        File.WriteAllText(Path.Combine(repo, ".doti", "profiles", "dotnet-cli", "profile.json"), ProfileJson);
        File.WriteAllText(Path.Combine(repo, ".doti", "core", "templates", "agent-context-template.md"), "context body\n");
        File.WriteAllText(Path.Combine(repo, ".doti", "core", "templates", "commands", "doti-specify.md"), "# command\n");
        File.WriteAllText(Path.Combine(repo, ".doti", "workflows", "doti", "workflow.yml"),
            "schemaVersion: 2\nstages:\n  - id: specify\n    prereqs: []\n");
        return repo;
    }

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-doti-managed-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void DeleteTemp(string dir)
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
