using Hx.Doti.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Doti.Tests;

public sealed class DotiPayloadParityTests
{
    [Fact]
    public void Payload_check_reproduces_managed_doti_assets_from_source_repo()
    {
        string repo = FindRepoRoot();

        DotiPayloadCheckResult result = DotiPayloadParityChecker.Check(repo);

        Assert.Equal(StageOutcome.Pass, result.Outcome);
        Assert.True(result.CheckedCount > 20);
        Assert.Empty(result.Drifted);
        Assert.Contains(result.Files, file => file.Kind == "static-doti" && file.SourcePath == ".doti/core/skills.json");
        Assert.Contains(result.Files, file => file.Kind == "rendered-doti" && file.InstalledPath == ".agents/skills/01-doti-specify/SKILL.md");
        Assert.Contains(result.Files, file => file.Kind == "rendered-doti" && file.InstalledPath == ".doti/agent-context.md");

        string installedSkill = Path.Combine(repo, ".agents", "skills", "08-doti-drift-review", "SKILL.md");
        string skillText = File.ReadAllText(installedSkill);
        Assert.Contains("Run `/09-doti-release` to release, or `/01-doti-specify` to add another feature to this release train.", skillText);
        Assert.Contains("Doti workflow transitions and release paths", File.ReadAllText(Path.Combine(repo, ".doti", "agent-context.md")));
        Assert.Contains("hx.config.json", File.ReadAllText(Path.Combine(repo, ".doti", "agent-context.md")));
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, ".doti", "core", "skills.json")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root with .doti/core/skills.json.");
    }
}
