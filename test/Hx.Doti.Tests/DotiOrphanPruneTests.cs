using Hx.Doti.Core;
using Hx.Doti.Core.ManagedAssets;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Doti.Tests;

/// <summary>
/// 027 T015 (W3, FR-008/009, SC-005): <c>hx doti update</c> / <c>doti install</c> must prune managed skill dirs the
/// new render no longer targets — a stage renumber renames the dir (e.g. <c>04-doti-tasks</c> → <c>05-doti-tasks</c>
/// with <c>04-doti-arch-review</c> taking the old ordinal) and the additive installer would otherwise leave the old
/// dir, confusing the agent (observed in agentx after the 0.15.x reorder). The prune is clean-baseline-gated: a
/// baseline-clean orphan is deleted, its dir pruned, and recorded in <c>ObsoleteAssets</c>; an operator-MODIFIED orphan
/// is preserved/blocked (never destroyed without <c>--force</c>). <c>doti payload check</c> flags a surplus
/// <c>*-doti-*</c> dir present in the repo but absent from the render targets.
///
/// The source repo renders only <c>01-doti-specify</c> (set B), so a manually-placed <c>04-doti-tasks</c> skill dir
/// (set A) is the renamed-away orphan; whether it is pruned turns ONLY on the prior baseline hash.
/// </summary>
public sealed class DotiOrphanPruneTests
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

    private const string OrphanSkillBody = "# 04-doti-tasks\n\nrendered from set A (ordinal 04, since renamed away)\n";

    // The orphan skill dir from set A: a managed *-doti-* dir under an agent SkillsRoot that the current render (set B,
    // which only targets 01-doti-specify) no longer produces.
    private const string OrphanDir = ".claude/skills/04-doti-tasks";
    private const string OrphanRel = ".claude/skills/04-doti-tasks/SKILL.md";

    [Fact]
    public void Baseline_clean_orphan_skill_dir_is_removed_pruned_and_recorded_obsolete()
    {
        string source = NewSourceRepo();
        string target = NewTempDir();
        try
        {
            // Set A on disk: a managed 04-doti-tasks skill dir whose baseline hash matches the file (clean).
            SeedOrphanWithBaseline(target, OrphanSkillBody, modifyAfterBaseline: false);

            DotiInstallResult result = DotiInstaller.Install(source, target, DotiAgentTarget.All, "renumber-repo");

            // The renamed-away dir is gone (file removed + empty dir pruned), recorded removed + obsolete.
            Assert.False(File.Exists(Path.Combine(target, OrphanRel.Replace('/', Path.DirectorySeparatorChar))));
            Assert.False(Directory.Exists(Path.Combine(target, OrphanDir.Replace('/', Path.DirectorySeparatorChar))));
            Assert.Contains(result.Removed, e => e.Path.Replace('\\', '/') == OrphanRel);
            Assert.Contains(result.Removed, e => e.Path.Replace('\\', '/') == OrphanDir + "/");
            Assert.DoesNotContain(result.Blocked, e => e.Path.Replace('\\', '/').StartsWith(OrphanDir, StringComparison.OrdinalIgnoreCase));

            ManagedAssetManifest manifest = ManagedAssetManifestStore.Read(target)!;
            Assert.Contains(manifest.ObsoleteAssets!, e => e.Path.Replace('\\', '/') == OrphanRel);
            // The current (set B) render still installed the kept skill dir alongside the prune.
            Assert.True(File.Exists(Path.Combine(target, ".claude", "skills", "01-doti-specify", "SKILL.md")));
        }
        finally
        {
            ForceDelete(source);
            ForceDelete(target);
        }
    }

    [Fact]
    public void Operator_modified_orphan_skill_dir_is_preserved_and_blocked_not_deleted()
    {
        string source = NewSourceRepo();
        string target = NewTempDir();
        try
        {
            // Set A on disk, but the operator edited the orphan AFTER its baseline was recorded (hash mismatch).
            SeedOrphanWithBaseline(target, OrphanSkillBody, modifyAfterBaseline: true);

            DotiInstallResult result = DotiInstaller.Install(source, target, DotiAgentTarget.All, "renumber-repo");

            // Operator edits are never destroyed without --force: the file + dir survive, the dir is preserved/blocked.
            Assert.True(File.Exists(Path.Combine(target, OrphanRel.Replace('/', Path.DirectorySeparatorChar))));
            Assert.True(Directory.Exists(Path.Combine(target, OrphanDir.Replace('/', Path.DirectorySeparatorChar))));
            Assert.Contains(result.Blocked, e => e.Path.Replace('\\', '/') == OrphanRel && e.Reason.Contains("modified", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Preserved, e => e.Path.Replace('\\', '/') == OrphanDir + "/");
            Assert.DoesNotContain(result.Removed, e => e.Path.Replace('\\', '/').StartsWith(OrphanDir, StringComparison.OrdinalIgnoreCase));

            ManagedAssetManifest manifest = ManagedAssetManifestStore.Read(target)!;
            Assert.DoesNotContain(manifest.ObsoleteAssets ?? [], e => e.Path.Replace('\\', '/') == OrphanRel);
        }
        finally
        {
            ForceDelete(source);
            ForceDelete(target);
        }
    }

    [Fact]
    public void Payload_check_flags_a_surplus_doti_skill_dir_absent_from_render_targets()
    {
        string source = NewSourceRepo();
        try
        {
            // Put a surplus *-doti-* skill dir in the SOURCE repo under an agent SkillsRoot — the render only targets
            // 01-doti-specify, so 04-doti-tasks is a surplus dir the parity check must flag (fail-closed).
            string surplus = Path.Combine(source, ".claude", "skills", "04-doti-tasks");
            Directory.CreateDirectory(surplus);
            File.WriteAllText(Path.Combine(surplus, "SKILL.md"), OrphanSkillBody);

            DotiPayloadCheckResult result = DotiPayloadParityChecker.Check(source);

            Assert.Equal(StageOutcome.Fail, result.Outcome);
            Assert.Contains(result.Files, f => f.Kind == "surplus-doti" && f.InstalledPath.Replace('\\', '/') == OrphanDir && !f.Matches);
            Assert.Contains(result.Drifted, d => d.Replace('\\', '/') == OrphanDir);
        }
        finally
        {
            ForceDelete(source);
        }
    }

    [Fact]
    public void Payload_check_has_no_surplus_rows_when_no_orphan_dir_is_present()
    {
        string source = NewSourceRepo();
        try
        {
            DotiPayloadCheckResult result = DotiPayloadParityChecker.Check(source);

            Assert.Equal(StageOutcome.Pass, result.Outcome);
            Assert.DoesNotContain(result.Files, f => f.Kind == "surplus-doti");
        }
        finally
        {
            ForceDelete(source);
        }
    }

    /// <summary>
    /// Establish "set A" on disk: write the orphan <c>04-doti-tasks/SKILL.md</c> and a managed-asset baseline whose
    /// recorded canonical hash matches it (clean). When <paramref name="modifyAfterBaseline"/> the file is edited after
    /// the baseline is recorded so the install reads it as operator-modified (preserved/blocked).
    /// </summary>
    private static void SeedOrphanWithBaseline(string target, string body, bool modifyAfterBaseline)
    {
        string full = Path.Combine(target, OrphanRel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, body);

        string profile = CanonicalContentHasher.ProfileForPath(OrphanRel);
        CanonicalHash hash = CanonicalContentHasher.HashFile(full, profile);
        var manifest = new ManagedAssetManifest(JsonContractDefaults.SchemaVersion,
        [
            new ManagedAssetHashEntry(
                OrphanRel,
                ManagedAssetCategory.SkillGeneratedInstruction,
                profile,
                hash.Sha256,
                hash.SourceFormat,
                hash.Canonicalizer,
                "canonical-content-hash",
                "managed-replace-preserve-live-config"),
        ]);
        ManagedAssetManifestStore.Write(target, manifest);

        if (modifyAfterBaseline)
        {
            File.WriteAllText(full, body + "\noperator hand-edit after the ordinal was renamed away\n");
        }
    }

    private static string NewSourceRepo()
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
        File.WriteAllText(Path.Combine(repo, ".doti", "memory", "constitution.md"), "# Constitution\n");
        File.WriteAllText(Path.Combine(repo, ".doti", "integrations", "doti.manifest.json"), "{}\n");
        return repo;
    }

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-doti-orphan-prune-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void ForceDelete(string dir)
    {
        if (!Directory.Exists(dir))
        {
            return;
        }

        try { Directory.Delete(dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
