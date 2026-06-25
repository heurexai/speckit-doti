using Hx.Doti.Core;
using Hx.Doti.Core.ManagedAssets;
using Hx.Scaffold.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Scaffold.Tests;

public sealed class DotiInstallScaffoldSourceTests
{
    [Fact]
    public void Scaffold_source_installs_dot_doti_payload_and_not_root_doti_payload()
    {
        string source = ScaffoldRoot.Resolve(Directory.GetCurrentDirectory());
        string parent = NewTempDir("hx-scaffold-doti-install-");
        string target = Path.Combine(parent, "missing-target");
        try
        {
            DotiInstallResult result = DotiInstaller.Install(source, target, DotiAgentTarget.All, "missing-target");

            Assert.Equal(StageOutcome.Pass, result.Outcome);
            Assert.Equal(DotiInstallClassification.InstalledNewTarget, result.Classification);
            Assert.True(File.Exists(Path.Combine(target, ".doti", "core", "skills.json")));
            Assert.True(File.Exists(Path.Combine(target, ".doti", "profiles", "dotnet-cli", "profile.json")));
            Assert.True(File.Exists(Path.Combine(target, ".agents", "skills", "01-doti-specify", "SKILL.md")));
            Assert.True(File.Exists(Path.Combine(target, ".claude", "skills", "09-doti-release", "SKILL.md")));
            Assert.False(Directory.Exists(Path.Combine(target, "doti")));
            Assert.Contains(result.Installed, e => e.Path == ".doti/managed-assets.json");

            string driftSkill = File.ReadAllText(Path.Combine(target, ".agents", "skills", "08-doti-drift-review", "SKILL.md"));
            Assert.Contains("Run `/09-doti-release` to release, or `/01-doti-specify` to add another feature to this release train.", driftSkill);

            string agentContext = File.ReadAllText(Path.Combine(target, ".doti", "agent-context.md"));
            Assert.Contains("Commits are owned by coded Doti workflow transitions and release paths", agentContext);
            Assert.Contains("hx.config.json", agentContext);
        }
        finally
        {
            ForceDelete(parent);
        }
    }

    [Fact]
    public void Scaffold_source_install_preserves_live_config_and_blocks_unproven_legacy_root()
    {
        string source = ScaffoldRoot.Resolve(Directory.GetCurrentDirectory());
        string target = NewTempDir("hx-scaffold-doti-upgrade-");
        string release = "{ \"schemaVersion\": 1, \"packageName\": \"Existing.App\" }\n";
        Directory.CreateDirectory(Path.Combine(target, ".doti"));
        Directory.CreateDirectory(Path.Combine(target, "doti", "custom"));
        File.WriteAllText(Path.Combine(target, ".doti", "release.json"), release);
        File.WriteAllText(Path.Combine(target, "doti", "custom", "keep.txt"), "repo-owned\n");
        try
        {
            DotiInstallResult result = DotiInstaller.Install(source, target, DotiAgentTarget.All, "existing-target");

            Assert.Equal(DotiInstallClassification.UpgradedExistingDotiRepo, result.Classification);
            Assert.Equal(release, File.ReadAllText(Path.Combine(target, ".doti", "release.json")));
            Assert.True(File.Exists(Path.Combine(target, "doti", "custom", "keep.txt")));
            Assert.Contains(result.Blocked, e => e.Path == "doti/" && e.Reason.Contains("managed baseline", StringComparison.OrdinalIgnoreCase));
            ManagedAssetManifest manifest = ManagedAssetManifestStore.Read(target)!;
            Assert.DoesNotContain(manifest.Assets, e => e.Path == ".doti/release.json");
        }
        finally
        {
            ForceDelete(target);
        }
    }

    private static string NewTempDir(string prefix)
    {
        string dir = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void ForceDelete(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try { Directory.Delete(path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
