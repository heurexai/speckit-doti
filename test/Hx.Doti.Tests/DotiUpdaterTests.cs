using Hx.Doti.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Doti.Tests;

/// <summary>
/// 022 T042 (FR-008/009/010/011/012/015): the updater reports before→after, preserves operator-modified managed
/// assets (or overwrites with --force), never touches operator-owned content (a constitution outside the payload),
/// and proceeds + warns when there is no managed-asset baseline. Reuses <see cref="DotiInstaller"/>'s reconcile, so
/// there is no second customization scheme.
/// </summary>
public sealed class DotiUpdaterTests
{
    private const string OperatorSkillsEdit =
        """
        {
          "schemaVersion": 1,
          "maturity": "command-aware-advisory",
          "commandTemplateDir": ".doti/core/templates/commands",
          "agentContextRef": ".doti/agent-context.md",
          "introTemplate": "Read `{agentContextRef}`, then follow `{commandTemplate}`.",
          "skills": [
            { "name": "doti-specify", "description": "Operator customized.", "argumentHint": "[goal]", "highlights": [], "nextStage": "Run `/doti-clarify`." }
          ]
        }
        """;

    [Fact]
    public void Reports_before_to_after_version()
    {
        string source1 = DotiVersionTestSupport.NewSource("1.0.0");
        string source2 = DotiVersionTestSupport.NewSource("2.0.0");
        string target = DotiVersionTestSupport.NewTempDir();
        try
        {
            DotiInstaller.Install(source1, target, DotiAgentTarget.All, "t");

            DotiUpdateOutcome outcome = DotiUpdater.Update(source2, target, DotiAgentTarget.All, "2.0.0", force: false);

            Assert.Equal(DotiUpdateStatus.Updated, outcome.Status);
            Assert.Equal("1.0.0", outcome.BeforeVersion);
            Assert.Equal("2.0.0", outcome.AfterVersion);
            Assert.Equal(DotiVersionRelation.Current, outcome.AfterRelation);
        }
        finally
        {
            Cleanup(source1, source2, target);
        }
    }

    [Fact]
    public void Equal_version_is_already_current()
    {
        string source = DotiVersionTestSupport.NewSource("2.0.0");
        string target = DotiVersionTestSupport.NewTempDir();
        try
        {
            DotiInstaller.Install(source, target, DotiAgentTarget.All, "t");

            DotiUpdateOutcome outcome = DotiUpdater.Update(source, target, DotiAgentTarget.All, "2.0.0", force: false);

            Assert.Equal(DotiUpdateStatus.AlreadyCurrent, outcome.Status);
            Assert.Equal("2.0.0", outcome.AfterVersion);
        }
        finally
        {
            Cleanup(source, target);
        }
    }

    [Fact]
    public void Customized_managed_asset_is_preserved_then_overwritten_with_force()
    {
        string source = DotiVersionTestSupport.NewSource("2.0.0");
        string target = DotiVersionTestSupport.NewTempDir();
        try
        {
            DotiInstaller.Install(source, target, DotiAgentTarget.All, "t");
            string skills = Path.Combine(target, DotiVersionTestSupport.SkillsRelative);
            File.WriteAllText(skills, OperatorSkillsEdit);

            DotiUpdateOutcome preserved = DotiUpdater.Update(source, target, DotiAgentTarget.All, "2.0.0", force: false);
            Assert.Equal(OperatorSkillsEdit, File.ReadAllText(skills));
            Assert.True(File.Exists(skills + ".new"), "bundled version staged as a .new sidecar");
            Assert.Contains(preserved.Customizations, c => c.Path.Replace('\\', '/') == ".doti/core/skills.json");

            DotiUpdater.Update(source, target, DotiAgentTarget.All, "2.0.0", force: true);
            Assert.Equal(DotiVersionTestSupport.SkillsJson, File.ReadAllText(skills).TrimEnd());
        }
        finally
        {
            Cleanup(source, target);
        }
    }

    [Fact]
    public void Operator_owned_constitution_is_untouched_even_with_force()
    {
        string source = DotiVersionTestSupport.NewSource("2.0.0", includeConstitution: false);
        string target = DotiVersionTestSupport.NewTempDir();
        try
        {
            DotiInstaller.Install(source, target, DotiAgentTarget.All, "t");
            string constitution = Path.Combine(target, DotiVersionTestSupport.ConstitutionRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(constitution)!);
            File.WriteAllText(constitution, "# My project constitution\nOperator-authored.\n");

            DotiUpdater.Update(source, target, DotiAgentTarget.All, "2.0.0", force: true);

            Assert.Equal("# My project constitution\nOperator-authored.\n", File.ReadAllText(constitution));
        }
        finally
        {
            Cleanup(source, target);
        }
    }

    [Fact]
    public void No_managed_baseline_proceeds_and_warns()
    {
        string source = DotiVersionTestSupport.NewSource("2.0.0");
        string target = DotiVersionTestSupport.NewTempDir();
        try
        {
            // A Doti repo with a recorded version but NO managed-asset baseline (brownfield).
            RepoPayloadStore.Write(target, "1.0.0", "1.0.0");

            DotiUpdateOutcome outcome = DotiUpdater.Update(source, target, DotiAgentTarget.All, "2.0.0", force: false);

            Assert.Equal(DotiUpdateStatus.Updated, outcome.Status);
            Assert.Equal("1.0.0", outcome.BeforeVersion);
            Assert.Equal("2.0.0", outcome.AfterVersion);
            Assert.NotNull(outcome.Reason);
            Assert.Contains("baseline", outcome.Reason!, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Cleanup(source, target);
        }
    }

    [Fact]
    public void Not_a_doti_directory_is_reported_not_mutated()
    {
        string source = DotiVersionTestSupport.NewSource("2.0.0");
        string target = DotiVersionTestSupport.NewTempDir();
        try
        {
            DotiUpdateOutcome outcome = DotiUpdater.Update(source, target, DotiAgentTarget.All, "2.0.0", force: false);

            Assert.Equal(DotiUpdateStatus.NotARepo, outcome.Status);
            Assert.False(Directory.Exists(Path.Combine(target, ".doti")), "a non-Doti dir must not be mutated");
        }
        finally
        {
            Cleanup(source, target);
        }
    }

    private static void Cleanup(params string[] dirs)
    {
        foreach (string dir in dirs)
        {
            DotiVersionTestSupport.ForceDelete(dir);
        }
    }
}
