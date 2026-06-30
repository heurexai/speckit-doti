using System.Text.Json;
using Hx.Cli.Kernel;
using Hx.Doti.Core.Setup;
using Hx.Tooling.Contracts;
using Hx.Tooling.Contracts.Setup;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>
/// 029 T019 (FR-004, SC-002/SC-003, D7): end-to-end integration for the REAL <c>hx doti config show</c> command
/// (<see cref="Hx.Runner.Cli.RunnerCommands.DotiConfigShow"/>). Covers the all-default view when no
/// <c>.doti/setup.json</c> is present (every key <c>source == default</c>), the provenance JSON shape on a persisted
/// intent, and that the command is non-mutating.
/// </summary>
public sealed class SetupConfigShowCommandTests
{
    private static readonly CliMeta Meta = new("hx", "0.0.0-test");

    [Fact]
    public void Config_show_with_no_setup_json_renders_the_all_default_view()
    {
        // D7: a repo with no .doti/setup.json is NOT an error — every key reads source == default, value == default.
        string repo = NewRepo();
        try
        {
            CliResult result = Hx.Runner.Cli.RunnerCommands.DotiConfigShow(Meta, repo);

            Assert.True(result.Ok);
            Assert.Contains("no .doti/setup.json", result.Summary, StringComparison.OrdinalIgnoreCase);

            using JsonDocument doc = JsonDocument.Parse(result.Data!.ToJsonString());
            Assert.Equal(0, doc.RootElement.GetProperty("custom").GetInt32());
            Assert.True(doc.RootElement.GetProperty("default").GetInt32() > 0);

            // every key in every group reads source == default.
            JsonElement groups = doc.RootElement.GetProperty("groups");
            foreach (JsonProperty group in groups.EnumerateObject())
            {
                foreach (JsonProperty key in group.Value.EnumerateObject())
                {
                    Assert.Equal("default", key.Value.GetProperty("source").GetString());
                    Assert.Equal(
                        key.Value.GetProperty("default").GetString(),
                        key.Value.GetProperty("value").GetString());
                }
            }

            // Non-mutating: the command created no .doti/setup.json.
            Assert.False(File.Exists(Path.Combine(repo, ".doti", "setup.json")));
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    [Fact]
    public void Config_show_reads_persisted_intent_with_value_source_default_provenance()
    {
        // SC-002: a persisted operator intent reads source != default for supplied keys; defaults for omitted ones.
        string repo = NewRepo();
        try
        {
            var intent = new SetupConfig
            {
                SchemaVersion = 1,
                Identity = new SetupIdentityConfig { Name = "Acme.Widget", License = "Apache-2.0" },
                Versioning = new SetupVersioningConfig { NextVersion = "9.9.9" },
            };
            SetupConfigStore.Write(repo, intent);

            CliResult result = Hx.Runner.Cli.RunnerCommands.DotiConfigShow(Meta, repo);

            Assert.True(result.Ok);
            using JsonDocument doc = JsonDocument.Parse(result.Data!.ToJsonString());
            JsonElement groups = doc.RootElement.GetProperty("groups");

            JsonElement nextVersion = groups.GetProperty("versioning").GetProperty("versioning.nextVersion");
            Assert.Equal("9.9.9", nextVersion.GetProperty("value").GetString());
            Assert.Equal("configFile", nextVersion.GetProperty("source").GetString());
            Assert.Equal(SetupConfigDefaults.NextVersion, nextVersion.GetProperty("default").GetString());

            // an omitted key (company) reads default + value == default.
            JsonElement company = groups.GetProperty("identity").GetProperty("identity.company");
            Assert.Equal("default", company.GetProperty("source").GetString());
            Assert.Equal(SetupConfigDefaults.Company, company.GetProperty("value").GetString());

            Assert.True(doc.RootElement.GetProperty("custom").GetInt32() > 0);
        }
        finally
        {
            ForceDelete(repo);
        }
    }

    private static string NewRepo()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-config-show-" + Guid.NewGuid().ToString("n"));
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
