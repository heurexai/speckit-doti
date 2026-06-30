using System.Text.Json;
using Hx.Doti.Core.Setup;
using Hx.Tooling.Contracts;
using Hx.Tooling.Contracts.Setup;
using Xunit;

namespace Hx.Doti.Tests.Setup;

/// <summary>029 T006/T019 (FR-004, SC-002/SC-003): the config-show renderers — the human table grouping + footer, and
/// the machine JSON shape ({value, source, default} per key, grouped) with a golden fixture.</summary>
public sealed class SetupConfigShowTests
{
    [Fact]
    public void Human_table_groups_marks_and_footers()
    {
        var config = new SetupConfig { Identity = new SetupIdentityConfig { License = "Apache-2.0" } };
        ResolvedSetupConfig resolved = SetupConfigResolver.Resolve(config, null, SetupAudience.Both);

        string table = SetupConfigTableFormatter.FormatHuman(resolved);

        Assert.Contains("## Identity", table);                 // SC-003: grouped by what each setting drives
        Assert.Contains("[custom ] identity.license = Apache-2.0", table); // marked custom
        Assert.Contains("[default] identity.company", table);  // marked default
        Assert.Matches(@"\d+ custom · \d+ default", table);    // the footer count
    }

    [Fact]
    public void All_default_view_marks_every_key_default()
    {
        // D7: config show on a no-config repo → all-default; every key source == default.
        ResolvedSetupConfig resolved = SetupConfigResolver.Resolve(null, null, SetupAudience.Both);
        (int custom, int defaulted) = SetupConfigTableFormatter.Counts(resolved);

        Assert.Equal(0, custom);
        Assert.True(defaulted > 0);
        Assert.All(resolved.Fields, f => Assert.Equal(ConfigSource.Default, f.Field.Source));
    }

    [Fact]
    public void Json_view_carries_value_source_default_per_key()
    {
        // SC-002: every key reads {value, source, default}; a custom value reads source != default.
        var config = new SetupConfig { Versioning = new SetupVersioningConfig { NextVersion = "9.9.9" } };
        ResolvedSetupConfig resolved = SetupConfigResolver.Resolve(config, null, SetupAudience.Both);

        SetupConfigShowView view = SetupConfigTableFormatter.ToView(resolved);
        string json = JsonSerializer.Serialize(view, JsonContractSerializerOptions.Create());
        using JsonDocument doc = JsonDocument.Parse(json);

        JsonElement versioning = doc.RootElement.GetProperty("groups").GetProperty("versioning");
        JsonElement next = versioning.GetProperty("versioning.nextVersion");
        Assert.Equal("9.9.9", next.GetProperty("value").GetString());
        Assert.Equal("configFile", next.GetProperty("source").GetString());
        Assert.Equal("0.1.0", next.GetProperty("default").GetString());

        // an omitted key reads source default + value == default.
        JsonElement license = doc.RootElement.GetProperty("groups").GetProperty("identity").GetProperty("identity.license");
        Assert.Equal("default", license.GetProperty("source").GetString());
        Assert.Equal("MIT", license.GetProperty("value").GetString());
    }

    [Fact]
    public void Golden_setup_json_resolves_to_the_expected_show_view()
    {
        // A golden setup.json fixture (inline) → the exact config show --json projection it must produce.
        const string fixture = """
        {
          "schemaVersion": 1,
          "identity": { "name": "Acme.Widget", "company": "Acme", "license": "Apache-2.0" },
          "versioning": { "nextVersion": "1.2.3" },
          "agents": ["claude"]
        }
        """;
        SetupValidationResult validation = SetupConfigSchema.ValidateRaw(fixture, out SetupConfig? config);
        Assert.True(validation.Ok);

        ResolvedSetupConfig resolved = SetupConfigResolver.Resolve(config, null, SetupAudience.Both);
        SetupConfigShowView view = SetupConfigTableFormatter.ToView(resolved);

        // name, company, license, nextVersion, agents are config-supplied (5) + authors derived from company (1) = 6 custom.
        Assert.Equal(6, view.Custom);
        Assert.True(resolved.IsCustom(SetupKeys.IdentityAuthors));
        Assert.Equal("Acme", resolved.ValueOrDefault(SetupKeys.IdentityAuthors));
        Assert.Equal(ConfigSource.Derived, resolved.Find(SetupKeys.IdentityAuthors)!.Field.Source);
        Assert.Equal("1.2.3", resolved.ValueOrDefault(SetupKeys.VersioningNextVersion));
        Assert.Equal("claude", resolved.ValueOrDefault(SetupKeys.Agents));
    }
}
