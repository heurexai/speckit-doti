using System.Text.Json;
using Hx.Cli.Kernel;
using Hx.Doti.Core;
using Hx.Doti.Core.Setup;
using Hx.Scaffold.Cli;
using Hx.Scaffold.Core;
using Hx.Tooling.Contracts;
using Hx.Tooling.Contracts.Setup;
using Xunit;

namespace Hx.Scaffold.Tests;

/// <summary>
/// 029 T019 (SC-005/SC-007/SC-008, D8/D10): end-to-end integration for the REAL <c>hx doti install</c> command
/// (<see cref="ScaffoldCommands.DotiInstall"/>) — the install-subset <c>--config</c> projection, the FR-007 checklist
/// surfaced on the result and proven inert (no nuget/git side effects), and the no-config no-op fence. These run a real
/// install into a temp dir (file copies + render — no <c>dotnet build</c>), sourcing the payload from the scaffold root.
/// </summary>
public sealed class SetupConfigInstallCommandTests
{
    private static readonly CliMeta Meta = new("hx", "0.0.0-test");

    // Resolve the scaffold payload source from the (repo-rooted) test output dir, so it is stable even when a test
    // temporarily changes the current directory to a sandbox for relative --config resolution.
    private static string Source => ScaffoldRoot.Find(AppContext.BaseDirectory);

    // ---- SC-008: the checklist is surfaced on the install result and is inert ----

    [Fact]
    public void Install_with_publish_config_surfaces_the_inert_checklist_and_runs_no_side_effects()
    {
        string sandbox = NewTempDir("hx-install-checklist-");
        string target = Path.Combine(sandbox, "widget");
        try
        {
            string configPath = WriteConfig(sandbox,
                """
                {
                  "schemaVersion": 1,
                  "publish": { "enabled": true, "owner": "acme", "repo": "widget" },
                  "versioning": { "nextVersion": "2.3.4" }
                }
                """);

            CliResult result = RunInstallFromSandbox(target, configPath, sandbox);

            Assert.True(result.Ok, string.Join("\n", result.Errors.Select(e => e.Message)));

            // SC-008: the checklist appears as next-actions, names the operator-only OIDC steps + the 030 git/CI steps.
            Assert.Contains(result.NextActions, a => a.Label.Contains("Trusted-Publishing", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.NextActions, a => a.Why.Contains("owner: acme", StringComparison.Ordinal));
            Assert.Contains(result.NextActions, a => a.Label.Contains("branch protection", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.NextActions, a => a.Label.Contains(".github/workflows", StringComparison.Ordinal));

            // Inert: hx performed NO nuget/git side effect — no .git, no .github/workflows, no tag, no commit.
            Assert.False(Directory.Exists(Path.Combine(target, ".git")), "install must not init a git repo");
            Assert.False(Directory.Exists(Path.Combine(target, ".github")), "install must not emit CI workflows (deferred to 030)");

            // The install-subset projection still landed (the version seed is a doti-layer field).
            SetupConfig? persisted = SetupConfigStore.Read(target);
            Assert.Equal("2.3.4", persisted!.Versioning!.NextVersion);
        }
        finally
        {
            ForceDelete(sandbox);
        }
    }

    // ---- SC-007 / D10: install with no --config / --interactive is byte-identical (no setup writes, no checklist) ----

    [Fact]
    public void Install_with_no_config_writes_no_setup_json_and_surfaces_no_checklist()
    {
        string target = NewTempDir("hx-install-noconfig-");
        try
        {
            CliResult result = RunInstall(target, configPath: null, sourceDirectory: Source);

            Assert.True(result.Ok, string.Join("\n", result.Errors.Select(e => e.Message)));
            // D10: the no-config path performs no setup projection → no tracked setup.json, no checklist next-actions.
            Assert.False(File.Exists(Path.Combine(target, ".doti", "setup.json")));
            Assert.DoesNotContain(result.NextActions, a => a.Label.Contains("Trusted-Publishing", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(result.NextActions, a => a.Label.Contains(".github/workflows", StringComparison.Ordinal));

            // The setup-config effect on the JSON envelope is null/absent on the no-config path.
            using JsonDocument doc = JsonDocument.Parse(result.Data!.ToJsonString());
            JsonElement install = doc.RootElement.GetProperty("install");
            bool hasSetupEffect = install.TryGetProperty("setup", out JsonElement setup)
                && setup.ValueKind != JsonValueKind.Null;
            Assert.False(hasSetupEffect, "no-config install must carry no setup effect");
        }
        finally
        {
            ForceDelete(target);
        }
    }

    [Fact]
    public void Install_no_config_install_subset_is_a_provable_projector_no_op()
    {
        // D10 at the installer seam: an install with Setup == null touches no setup writer (the install-subset set).
        string target = NewTempDir("hx-install-fence-");
        try
        {
            SetupProjectionResult result = SetupConfigProjector.Project(
                null, target, SetupTargetWriters.ForInstall());
            Assert.Empty(result.Written);
            Assert.Empty(result.Ignored);
        }
        finally
        {
            ForceDelete(target);
        }
    }

    // ---- SC-005 (install): a new-only field reached on install is reported as ignored, never silently dropped ----

    [Fact]
    public void Install_reports_new_only_csproj_field_as_ignored()
    {
        // identity.description is a New-only csproj field; on the install audience it must be reported ignored.
        var fields = new List<ResolvedSetupField>
        {
            new(SetupKeys.IdentityDescription, SetupGroup.Identity, SetupAudience.New, SetupTarget.CsprojMetadata,
                new ConfigField("Acme widget", ConfigSource.ConfigFile, SetupConfigDefaults.Description)),
        };
        var resolved = new ResolvedSetupConfig(1, SetupAudience.Install, fields);

        string target = NewTempDir("hx-install-ignored-");
        try
        {
            SetupProjectionResult result = SetupConfigProjector.Project(
                resolved, target, SetupTargetWriters.ForInstall());
            Assert.Contains(result.Ignored, i => i.Key == SetupKeys.IdentityDescription);
            Assert.Empty(result.Written);
        }
        finally
        {
            ForceDelete(target);
        }
    }

    private static CliResult RunInstall(string target, string? configPath, string sourceDirectory) =>
        ScaffoldCommands.DotiInstall(
            Meta, targetRepo: target, agentsCsv: "codex,claude", force: false,
            sourceDirectory: sourceDirectory, configPath: configPath, interactive: false);

    private static CliResult RunInstallFromSandbox(string target, string configPath, string sandbox)
    {
        // The CLI contains the --config path against the CURRENT directory; run from the sandbox so a relative path resolves.
        string previousCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(sandbox);
            return ScaffoldCommands.DotiInstall(
                Meta, targetRepo: target, agentsCsv: "codex,claude", force: false,
                sourceDirectory: Source, configPath: Path.GetFileName(configPath), interactive: false);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCwd);
        }
    }

    private static string WriteConfig(string dir, string json)
    {
        string path = Path.Combine(dir, "install-setup.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static string NewTempDir(string prefix)
    {
        string dir = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void ForceDelete(string dir)
    {
        if (!Directory.Exists(dir))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            try { File.SetAttributes(file, FileAttributes.Normal); }
            catch { /* best-effort temp cleanup */ }
        }

        try { Directory.Delete(dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
