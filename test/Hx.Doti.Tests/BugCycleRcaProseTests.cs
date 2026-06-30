using Xunit;

namespace Hx.Doti.Tests;

/// <summary>
/// 031 T009/T014 (FR-013/014/015, SC-013): the bug-cycle RCA discipline is present in the authoritative prose AND its
/// rendered outputs — verifiable by content inspection. The assess command requires a reproduce-and-root-cause RCA;
/// the fix command requires a root-cause fix and explicitly forbids a bandaid/symptom patch and a bandaid-vs-root
/// choice; the rendered <c>/doti-bug</c> SKILL.md (single-sourced from <c>.doti/core/skills.json</c>) carries the
/// same. This is the source-of-truth → rendered-asset parity the cycle requires.
/// </summary>
public sealed class BugCycleRcaProseTests
{
    [Fact]
    public void Assess_command_requires_reproduce_and_root_cause_not_symptom()
    {
        string assess = ReadRepo("extensions", "bug", "commands", "speckit.bug.assess.md");

        Assert.Contains("root-cause analysis", assess, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reproduce", assess, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ROOT cause", assess, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Validate", assess, StringComparison.OrdinalIgnoreCase);
        // The discipline: do not stop at the symptom; the remediation fixes the root, never a symptom mask.
        Assert.Contains("stop at the symptom", assess, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("symptom mask", assess, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Fix_command_requires_root_cause_and_forbids_bandaid_and_options()
    {
        string fix = ReadRepo("extensions", "bug", "commands", "speckit.bug.fix.md");

        Assert.Contains("ROOT cause", fix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FORBIDDEN", fix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bandaid", fix, StringComparison.OrdinalIgnoreCase);
        // Must DO the fix and NOT present a bandaid-vs-root choice / ask which approach.
        Assert.Contains("do NOT", fix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("which fix", fix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("blocker", fix, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rendered_doti_bug_skill_carries_the_rca_and_no_bandaid_discipline_for_both_agents()
    {
        foreach (string agentRoot in new[] { ".claude", ".agents" })
        {
            string skill = ReadRepo(agentRoot, "skills", "doti-bug", "SKILL.md");

            Assert.Contains("root-cause analysis", skill, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("REPRODUCE", skill, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ROOT cause", skill, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("FORBIDDEN", skill, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("bandaid", skill, StringComparison.OrdinalIgnoreCase);
            // No bandaid-vs-root options / asking which fix to apply.
            Assert.Contains("MUST NOT present", skill, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Skills_json_source_of_truth_carries_the_rca_discipline()
    {
        string skillsJson = ReadRepo(".doti", "core", "skills.json");

        Assert.Contains("root-cause analysis", skillsJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FORBIDDEN", skillsJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bandaid", skillsJson, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadRepo(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { FindRepoRoot() }.Concat(parts).ToArray()));

    private static string FindRepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "scaffold-dotnet.slnx")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not locate the repo root (scaffold-dotnet.slnx).");
    }
}
