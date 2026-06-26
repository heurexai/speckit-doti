using System.Text.Json;
using Hx.Doti.Core;
using Hx.Doti.Core.ManagedAssets;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Doti.Tests;

public sealed class DotiInstallerTargetTests
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
    public void Missing_target_is_created_and_classified_as_new_install()
    {
        string source = NewSourceRepo();
        string parent = NewTempDir();
        string target = Path.Combine(parent, "missing-target");
        try
        {
            DotiInstallResult result = DotiInstaller.Install(source, target, DotiAgentTarget.All, "missing-target");

            Assert.Equal(StageOutcome.Pass, result.Outcome);
            Assert.Equal(DotiInstallClassification.InstalledNewTarget, result.Classification);
            Assert.True(result.TargetCreated);
            Assert.True(Directory.Exists(target));
            Assert.NotNull(result.NextStep);
            Assert.Contains(result.Installed, e => e.Path == ".doti/managed-assets.json");
        }
        finally
        {
            ForceDelete(source);
            ForceDelete(parent);
        }
    }

    [Fact]
    public void Bootstrapper_requires_explicit_target_repo()
    {
        string source = NewSourceRepo();
        try
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
                DotiInstallBootstrapper.Install(
                    source,
                    new DotiInstallBootstrapRequest(null, DotiAgentTarget.All)));

            Assert.Contains("explicit target", ex.Message);
        }
        finally
        {
            ForceDelete(source);
        }
    }

    [Fact]
    public void Bootstrapper_returns_same_classification_proof_for_installer_hosts()
    {
        string source = NewSourceRepo();
        string parent = NewTempDir();
        string target = Path.Combine(parent, "installer-target");
        try
        {
            DotiInstallResult result = DotiInstallBootstrapper.Install(
                source,
                new DotiInstallBootstrapRequest(target, DotiAgentTarget.All));

            Assert.Equal(DotiInstallClassification.InstalledNewTarget, result.Classification);
            Assert.True(result.TargetCreated);
            Assert.NotEmpty(result.Installed);
            Assert.True(File.Exists(Path.Combine(target, ".doti", "integration.json")));
        }
        finally
        {
            ForceDelete(source);
            ForceDelete(parent);
        }
    }

    [Fact]
    public void Empty_target_is_classified_without_reporting_creation()
    {
        string source = NewSourceRepo();
        string target = NewTempDir();
        try
        {
            DotiInstallResult result = DotiInstaller.Install(source, target, DotiAgentTarget.All, "empty-target");

            Assert.Equal(DotiInstallClassification.InstalledEmptyTarget, result.Classification);
            Assert.False(result.TargetCreated);
            Assert.NotNull(result.NextStep);
        }
        finally
        {
            ForceDelete(source);
            ForceDelete(target);
        }
    }

    [Fact]
    public void Non_empty_without_doti_is_first_time_install_and_preserves_existing_files()
    {
        string source = NewSourceRepo();
        string target = NewTempDir();
        string existing = Path.Combine(target, "README.md");
        File.WriteAllText(existing, "# existing project\n");
        try
        {
            DotiInstallResult result = DotiInstaller.Install(source, target, DotiAgentTarget.All, "existing-project");

            Assert.Equal(DotiInstallClassification.InstalledNonEmptyNonDotiTarget, result.Classification);
            Assert.Null(result.NextStep);
            Assert.Equal("# existing project\n", File.ReadAllText(existing));
            Assert.Contains(result.Installed, e => e.Path == ".doti/integration.json");
        }
        finally
        {
            ForceDelete(source);
            ForceDelete(target);
        }
    }

    [Fact]
    public void Non_doti_target_gets_the_non_imposing_workflow_only_tier_FR030()
    {
        string source = NewSourceRepo();
        string target = NewTempDir();
        File.WriteAllText(Path.Combine(target, "README.md"), "# existing foreign project\n");
        try
        {
            DotiInstallResult result = DotiInstaller.Install(source, target, DotiAgentTarget.All, "existing-project");

            Assert.Equal(DotiInstallClassification.InstalledNonEmptyNonDotiTarget, result.Classification);
            // FR-030: doti must not impose the Heurex (Tier-3) structure on existing foreign code.
            Assert.Equal("workflow-only", ReadInstalledProfile(target));
        }
        finally
        {
            ForceDelete(source);
            ForceDelete(target);
        }
    }

    [Fact]
    public void New_scaffold_target_gets_the_dotnet_cli_tier()
    {
        string source = NewSourceRepo();
        string parent = NewTempDir();
        string target = Path.Combine(parent, "new-target");
        try
        {
            DotiInstallResult result = DotiInstaller.Install(source, target, DotiAgentTarget.All, "new-target");

            Assert.Equal(DotiInstallClassification.InstalledNewTarget, result.Classification);
            Assert.Equal("dotnet-cli", ReadInstalledProfile(target));
        }
        finally
        {
            ForceDelete(source);
            ForceDelete(parent);
        }
    }

    private static string? ReadInstalledProfile(string target)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(target, ".doti", "integration.json")));
        return doc.RootElement.TryGetProperty("profile", out JsonElement p) ? p.GetString() : null;
    }

    [Fact]
    public void Existing_legacy_root_doti_without_baseline_is_blocked_from_removal_unless_forced()
    {
        string source = NewSourceRepo();
        string target = NewTempDir();
        Directory.CreateDirectory(Path.Combine(target, "doti", "custom"));
        File.WriteAllText(Path.Combine(target, "doti", "custom", "keep.txt"), "custom\n");
        try
        {
            DotiInstallResult result = DotiInstaller.Install(source, target, DotiAgentTarget.All, "legacy-project");

            Assert.Equal(DotiInstallClassification.UpgradedExistingDotiRepo, result.Classification);
            Assert.True(File.Exists(Path.Combine(target, "doti", "custom", "keep.txt")));
            Assert.Contains(result.Blocked, e => e.Path == "doti/" && e.Reason.Contains("managed baseline", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(result.Removed);
        }
        finally
        {
            ForceDelete(source);
            ForceDelete(target);
        }
    }

    [Fact]
    public void Existing_legacy_root_doti_with_clean_managed_baseline_is_removed_after_dot_doti_install()
    {
        string source = NewSourceRepo();
        string target = NewTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(target, "doti", "core"));
            string legacy = Path.Combine(target, "doti", "core", "skills.json");
            File.WriteAllText(legacy, SkillsJson);
            var manifest = new ManagedAssetManifest(JsonContractDefaults.SchemaVersion,
            [
                new ManagedAssetHashEntry(
                    "doti/core/skills.json",
                    ManagedAssetCategory.DotiSource,
                    HashProfile.JsonSemantic,
                    CanonicalContentHasher.HashFile(legacy, HashProfile.JsonSemantic).Sha256,
                    "json",
                    "System.Text.Json+rfc8785-compatible/v1",
                    "canonical-content-hash",
                    "managed-replace-preserve-live-config"),
            ]);
            ManagedAssetManifestStore.Write(target, manifest);

            DotiInstallResult result = DotiInstaller.Install(source, target, DotiAgentTarget.All, "legacy-project");

            Assert.Equal(DotiInstallClassification.UpgradedExistingDotiRepo, result.Classification);
            Assert.False(Directory.Exists(Path.Combine(target, "doti")));
            Assert.Contains(result.Removed, e => e.Path == "doti/core/skills.json");
            Assert.Contains(result.Removed, e => e.Path == "doti/");
            Assert.Empty(result.Blocked);
            ManagedAssetManifest updated = ManagedAssetManifestStore.Read(target)!;
            Assert.Contains(updated.ObsoleteAssets!, e => e.Path == "doti/core/skills.json");
            Assert.DoesNotContain(updated.Assets, e => e.Path.StartsWith("doti/", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            ForceDelete(source);
            ForceDelete(target);
        }
    }

    [Fact]
    public void Install_preserves_live_doti_configuration_files()
    {
        string source = NewSourceRepo();
        string target = NewTempDir();
        string releaseJson = "{ \"schemaVersion\": 1, \"packageName\": \"Existing.App\" }\n";
        string cycleStateJson = "{ \"schemaVersion\": 1, \"activeFeature\": \"001-existing\" }\n";
        Directory.CreateDirectory(Path.Combine(target, ".doti"));
        File.WriteAllText(Path.Combine(target, ".doti", "release.json"), releaseJson);
        File.WriteAllText(Path.Combine(target, ".doti", "cycle-state.json"), cycleStateJson);
        try
        {
            DotiInstallResult result = DotiInstaller.Install(source, target, DotiAgentTarget.All, "existing-project");

            Assert.Equal(StageOutcome.Pass, result.Outcome);
            Assert.Equal(releaseJson, File.ReadAllText(Path.Combine(target, ".doti", "release.json")));
            Assert.Equal(cycleStateJson, File.ReadAllText(Path.Combine(target, ".doti", "cycle-state.json")));
            ManagedAssetManifest manifest = ManagedAssetManifestStore.Read(target)!;
            Assert.DoesNotContain(manifest.Assets, e => e.Path == ".doti/release.json");
            Assert.DoesNotContain(manifest.Assets, e => e.Path == ".doti/cycle-state.json");
        }
        finally
        {
            ForceDelete(source);
            ForceDelete(target);
        }
    }

    [Fact]
    public void Existing_legacy_root_doti_with_modified_managed_asset_is_blocked_without_force()
    {
        string source = NewSourceRepo();
        string target = NewTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(target, "doti", "core"));
            string legacy = Path.Combine(target, "doti", "core", "skills.json");
            File.WriteAllText(legacy, SkillsJson);
            var manifest = new ManagedAssetManifest(JsonContractDefaults.SchemaVersion,
            [
                new ManagedAssetHashEntry(
                    "doti/core/skills.json",
                    ManagedAssetCategory.DotiSource,
                    HashProfile.JsonSemantic,
                    CanonicalContentHasher.HashFile(legacy, HashProfile.JsonSemantic).Sha256),
            ]);
            ManagedAssetManifestStore.Write(target, manifest);
            File.WriteAllText(legacy, "{ \"changed\": true }\n");

            DotiInstallResult result = DotiInstaller.Install(source, target, DotiAgentTarget.All, "legacy-project");

            Assert.True(File.Exists(legacy));
            Assert.Contains(result.Blocked, e => e.Path == "doti/core/skills.json" && e.Reason.Contains("modified", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Preserved, e => e.Path == "doti/");
        }
        finally
        {
            ForceDelete(source);
            ForceDelete(target);
        }
    }

    [Fact]
    public void Existing_legacy_root_doti_force_removes_unknown_legacy_root_after_installing_dot_doti()
    {
        string source = NewSourceRepo();
        string target = NewTempDir();
        Directory.CreateDirectory(Path.Combine(target, "doti", "custom"));
        File.WriteAllText(Path.Combine(target, "doti", "custom", "delete.txt"), "custom\n");
        try
        {
            DotiInstallResult result = DotiInstaller.Install(source, target, DotiAgentTarget.All, "legacy-project", force: true);

            Assert.False(Directory.Exists(Path.Combine(target, "doti")));
            Assert.Contains(result.Removed, e => e.Path == "doti/" && e.Reason.Contains("force", StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(Path.Combine(target, ".doti", "core", "skills.json")));
        }
        finally
        {
            ForceDelete(source);
            ForceDelete(target);
        }
    }

    [Fact]
    public void Classifier_identifies_existing_doti_repo_from_dot_doti_directory()
    {
        string target = NewTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(target, ".doti"));

            DotiTargetClassification classification = DotiTargetClassifier.Classify(target);

            Assert.Equal(DotiInstallClassification.UpgradedExistingDotiRepo, classification.Classification);
            Assert.True(classification.HasDotiWorkflow);
        }
        finally
        {
            ForceDelete(target);
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
        string dir = Path.Combine(Path.GetTempPath(), "hx-doti-install-target-" + Guid.NewGuid().ToString("n"));
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
