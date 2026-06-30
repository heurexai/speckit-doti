using Hx.Doti.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Doti.Tests;

public sealed class DotiRendererTests
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

    private static string NewRepo()
    {
        string repo = Path.Combine(Path.GetTempPath(), "hx-doti-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(Path.Combine(repo, ".doti", "core", "templates", "commands"));
        Directory.CreateDirectory(Path.Combine(repo, ".doti", "profiles", "dotnet-cli"));
        Directory.CreateDirectory(Path.Combine(repo, ".doti", "workflows", "doti"));
        File.WriteAllText(Path.Combine(repo, ".doti", "core", "skills.json"), SkillsJson);
        File.WriteAllText(Path.Combine(repo, ".doti", "profiles", "dotnet-cli", "profile.json"), ProfileJson);
        File.WriteAllText(Path.Combine(repo, ".doti", "core", "templates", "agent-context-template.md"), "context body\n");
        // 028 FR-010: the renderer now projects the skill identity from the stage model — the minimal render repo
        // carries the canonical specify stage so `doti-specify` resolves to its `01-doti-specify` skill id.
        File.WriteAllText(Path.Combine(repo, ".doti", "workflows", "doti", "workflow.yml"),
            "schemaVersion: 2\nstages:\n  - id: specify\n    command: 01-doti-specify\n    kind: doc\n    prereqs: []\n");
        return repo;
    }

    [Fact]
    public void WriteThenCheckIsPassAndIdempotent()
    {
        string repo = NewRepo();
        try
        {
            DotiRenderResult write = DotiRenderer.Render(repo, DotiAgentTarget.All, check: false);
            Assert.Equal(StageOutcome.Pass, write.Outcome);
            // 1 skill x 2 agents + 2 root entrypoints + 1 agent-context = 5 files written on first run.
            Assert.Equal(5, write.Written.Count);
            Assert.True(File.Exists(Path.Combine(repo, "CLAUDE.md")));
            Assert.True(File.Exists(Path.Combine(repo, "AGENTS.md")));
            Assert.True(File.Exists(Path.Combine(repo, ".claude", "skills", "01-doti-specify", "SKILL.md")));
            Assert.True(File.Exists(Path.Combine(repo, ".agents", "skills", "01-doti-specify", "SKILL.md")));

            DotiRenderResult check = DotiRenderer.Render(repo, DotiAgentTarget.All, check: true);
            Assert.Equal(StageOutcome.Pass, check.Outcome);
            Assert.Empty(check.Drifted);
        }
        finally
        {
            if (Directory.Exists(repo)) Directory.Delete(repo, recursive: true);
        }
    }

    [Fact]
    public void CheckFailsClosedWhenAnInstalledFileIsHandEdited()
    {
        string repo = NewRepo();
        try
        {
            DotiRenderer.Render(repo, DotiAgentTarget.All, check: false);

            string claudeSkill = Path.Combine(repo, ".claude", "skills", "01-doti-specify", "SKILL.md");
            File.AppendAllText(claudeSkill, "hand edit\n");

            DotiRenderResult check = DotiRenderer.Render(repo, DotiAgentTarget.All, check: true);
            Assert.Equal(StageOutcome.Fail, check.Outcome);
            Assert.Contains(".claude/skills/01-doti-specify/SKILL.md", check.Drifted);
        }
        finally
        {
            if (Directory.Exists(repo)) Directory.Delete(repo, recursive: true);
        }
    }
}
