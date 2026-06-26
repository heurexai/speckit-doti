using System.Text.Json;
using Hx.Doti.Core;
using Hx.Doti.Core.ManagedAssets;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Doti.Tests;

/// <summary>
/// 007 T029 (test-first for T030): <c>doti install --repo</c> is a conflict-aware reconciliation, not a blind copy
/// (FR-015, SC-007, SC-018). It branches on the bundled payload version (from the payload descriptor) vs the repo's
/// recorded <c>.doti/payload.json</c>: absent (brownfield) / equal (repair) / older (migrate-forward) all run the
/// hash-preservation forward copy so operator edits are never clobbered (preserved + a <c>.new</c> sidecar), an
/// operator-deleted managed asset is not resurrected without <c>--force</c>, a re-run is idempotent, and a managed
/// path that escapes the repo root is refused. Repo NEWER than the bundled payload is refused outright.
///
/// These describe the behavior T030 builds; against today's blind-overwrite installer they are RED.
/// </summary>
public sealed class DotiReconciliationTests
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

    // A REALISTIC operator edit: a valid skills.json variant (still renderable — same skill name/template), but with
    // a changed description so its canonical hash differs from the bundled baseline (-> Modified -> preserved).
    private const string OperatorSkillsEdit =
        """
        {
          "schemaVersion": 1,
          "maturity": "command-aware-advisory",
          "commandTemplateDir": ".doti/core/templates/commands",
          "agentContextRef": ".doti/agent-context.md",
          "introTemplate": "Read `{agentContextRef}`, then follow `{commandTemplate}`.",
          "skills": [
            { "name": "doti-specify", "description": "Operator customized spec.", "argumentHint": "[goal]", "highlights": [], "nextStage": "Run `/doti-clarify`." }
          ]
        }
        """;

    private static readonly string SkillsRelative = Path.Combine(".doti", "core", "skills.json");
    private static readonly string ConstitutionRelative = Path.Combine(".doti", "memory", "constitution.md");

    [Fact]
    public void Repo_payload_newer_than_bundled_is_refused()
    {
        Fixture f = Fixture.Installed(bundled: "2.0.0");
        try
        {
            f.StampRepo("3.0.0"); // operator's repo carries a newer payload than this (older) tool bundles

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                DotiInstaller.Install(f.Source, f.Target, DotiAgentTarget.All, "ahead-repo"));

            Assert.Contains("ahead", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            f.Dispose();
        }
    }

    [Fact]
    public void Repo_payload_older_than_bundled_migrates_forward_and_restamps()
    {
        Fixture f = Fixture.Installed(bundled: "2.0.0");
        try
        {
            f.StampRepo("1.0.0"); // an older recorded payload -> migrate forward to the bundled version

            DotiInstallResult result = DotiInstaller.Install(f.Source, f.Target, DotiAgentTarget.All, "older-repo");

            Assert.Equal(StageOutcome.Pass, result.Outcome);
            Assert.Equal("2.0.0", f.RepoVersion()); // re-stamped to the bundled payload version
            Assert.Equal(SkillsJson, File.ReadAllText(Path.Combine(f.Target, SkillsRelative)).TrimEnd());
        }
        finally
        {
            f.Dispose();
        }
    }

    [Fact]
    public void Equal_version_repair_preserves_operator_modified_doti_source_via_new_sidecar()
    {
        Fixture f = Fixture.Installed(bundled: "2.0.0");
        try
        {
            f.StampRepo("2.0.0");
            string skills = Path.Combine(f.Target, SkillsRelative);
            File.WriteAllText(skills, OperatorSkillsEdit);

            DotiInstallResult result = DotiInstaller.Install(f.Source, f.Target, DotiAgentTarget.All, "equal-repo");

            // The operator's edit is preserved; the bundled version lands in a `.new` sidecar (never blind-overwritten).
            Assert.Equal(OperatorSkillsEdit, File.ReadAllText(skills));
            Assert.True(File.Exists(skills + ".new"), "bundled managed asset should be staged as a .new sidecar");
            Assert.Equal(SkillsJson, File.ReadAllText(skills + ".new").TrimEnd());
            Assert.Contains(result.Preserved, e => e.Path.Replace('\\', '/') == ".doti/core/skills.json");
        }
        finally
        {
            f.Dispose();
        }
    }

    [Fact]
    public void Brownfield_install_preserves_preexisting_operator_doti_content()
    {
        // No recorded version, no baseline: pre-existing operator .doti content must route through the conflict-aware
        // path, not be blind-overwritten.
        string source = NewSource("2.0.0");
        string target = NewTempDir();
        try
        {
            string skills = Path.Combine(target, SkillsRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(skills)!);
            File.WriteAllText(skills, OperatorSkillsEdit);

            DotiInstaller.Install(source, target, DotiAgentTarget.All, "brownfield-repo");

            Assert.Equal(OperatorSkillsEdit, File.ReadAllText(skills));
            Assert.True(File.Exists(skills + ".new"), "brownfield install should stage the bundled asset as .new, not clobber");
        }
        finally
        {
            ForceDelete(source);
            ForceDelete(target);
        }
    }

    [Fact]
    public void Operator_deleted_managed_asset_is_not_resurrected_without_force()
    {
        Fixture f = Fixture.Installed(bundled: "2.0.0");
        try
        {
            f.StampRepo("2.0.0");
            // A non-render-critical managed asset the operator intentionally removed.
            string constitution = Path.Combine(f.Target, ConstitutionRelative);
            File.Delete(constitution);

            DotiInstaller.Install(f.Source, f.Target, DotiAgentTarget.All, "deleted-repo");
            Assert.False(File.Exists(constitution), "an operator-deleted managed asset must not be resurrected without --force");

            DotiInstaller.Install(f.Source, f.Target, DotiAgentTarget.All, "deleted-repo", force: true);
            Assert.True(File.Exists(constitution), "--force re-installs the deleted managed asset");
        }
        finally
        {
            f.Dispose();
        }
    }

    [Fact]
    public void Idempotent_reinstall_is_stable_and_writes_no_sidecars()
    {
        Fixture f = Fixture.Installed(bundled: "2.0.0");
        try
        {
            f.StampRepo("2.0.0");

            DotiInstallResult result = DotiInstaller.Install(f.Source, f.Target, DotiAgentTarget.All, "stable-repo");

            Assert.Equal(StageOutcome.Pass, result.Outcome);
            Assert.Equal("2.0.0", f.RepoVersion());
            Assert.Empty(Directory.EnumerateFiles(Path.Combine(f.Target, ".doti"), "*.new", SearchOption.AllDirectories));
        }
        finally
        {
            f.Dispose();
        }
    }

    [Fact]
    public void Install_stamps_repo_payload_version_verbatim_from_the_bundled_descriptor()
    {
        // T030 invariant: .doti/payload.json's payloadVersion equals the bundled descriptor's, copied verbatim
        // (the pre-release suffix is preserved, not normalized away).
        string source = NewSource("7.3.1-rc.4");
        string target = NewTempDir();
        try
        {
            DotiInstaller.Install(source, target, DotiAgentTarget.All, "stamp-repo");
            Assert.Equal("7.3.1-rc.4", ReadRepoVersion(target));
        }
        finally
        {
            ForceDelete(source);
            ForceDelete(target);
        }
    }

    [Fact]
    public void Managed_asset_path_escaping_repo_root_is_refused()
    {
        string source = NewSource("2.0.0");
        string target = NewTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(target, ".doti"));
            // A baseline that names a managed asset OUTSIDE the repo root: reconciliation must refuse, never write out.
            var poisoned = new ManagedAssetManifest(JsonContractDefaults.SchemaVersion,
            [
                new ManagedAssetHashEntry("../escape.txt", ManagedAssetCategory.DotiSource, HashProfile.ByteExact, "deadbeef"),
            ]);
            ManagedAssetManifestStore.Write(target, poisoned);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
                DotiInstaller.Install(source, target, DotiAgentTarget.All, "escape-repo"));

            Assert.Contains("escape", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(target)!, "escape.txt")), "no write may escape the repo root");
        }
        finally
        {
            ForceDelete(source);
            ForceDelete(target);
        }
    }

    // --- fixtures ---

    private sealed class Fixture : IDisposable
    {
        public required string Source { get; init; }
        public required string Target { get; init; }

        public static Fixture Installed(string bundled)
        {
            string source = NewSource(bundled);
            string target = NewTempDir();
            // Establish a realistic previously-installed repo (managed baseline + rendered + copied .doti).
            DotiInstaller.Install(source, target, DotiAgentTarget.All, "fixture-repo");
            return new Fixture { Source = source, Target = target };
        }

        public void StampRepo(string version) =>
            WriteRepoStamp(Target, version);

        public string? RepoVersion() => ReadRepoVersion(Target);

        public void Dispose()
        {
            ForceDelete(Source);
            ForceDelete(Target);
        }
    }

    private static string? ReadRepoVersion(string target)
    {
        string path = Path.Combine(target, ".doti", "payload.json");
        if (!File.Exists(path))
        {
            return null;
        }

        RepoPayloadStamp? stamp = JsonSerializer.Deserialize<RepoPayloadStamp>(
            File.ReadAllText(path), JsonContractSerializerOptions.Create());
        return stamp?.PayloadVersion;
    }

    private static void WriteRepoStamp(string target, string version)
    {
        string dotiDir = Path.Combine(target, ".doti");
        Directory.CreateDirectory(dotiDir);
        var stamp = new RepoPayloadStamp(RepoPayloadStamp.CurrentSchemaVersion, version, "0.9.1");
        File.WriteAllText(Path.Combine(dotiDir, "payload.json"),
            JsonSerializer.Serialize(stamp, JsonContractSerializerOptions.Create()));
    }

    private static string NewSource(string payloadVersion)
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

        // The bundled payload descriptor beside the payload root (reconciliation reads its version).
        var descriptor = new PayloadDescriptor(
            PayloadDescriptor.CurrentSchemaVersion, payloadVersion, "0.9.1",
            DistributionChannelId.GlobalTool, CommandMode.Installed, []);
        File.WriteAllText(Path.Combine(repo, "payload.manifest.json"),
            JsonSerializer.Serialize(descriptor, JsonContractSerializerOptions.Create()));
        return repo;
    }

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-doti-reconcile-" + Guid.NewGuid().ToString("n"));
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
