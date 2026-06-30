using Hx.Tooling.Contracts.Setup;
using Xunit;

namespace Hx.Doti.Tests.Setup;

/// <summary>029 T003 (FR-001/FR-002, SC-001/SC-002): the provenance resolver layers flag ▸ config-file ▸ derived ▸
/// default per key and records the winning source; the install audience drops new-only keys.</summary>
public sealed class SetupConfigResolverTests
{
    [Fact]
    public void Omitted_field_reads_default_with_default_source()
    {
        // SC-002: an omitted value reads source == default and value == default.
        ResolvedSetupConfig resolved = SetupConfigResolver.Resolve(new SetupConfig(), flags: null, SetupAudience.New);

        ResolvedSetupField license = resolved.Find(SetupKeys.IdentityLicense)!;
        Assert.Equal(ConfigSource.Default, license.Field.Source);
        Assert.Equal(SetupConfigDefaults.License, license.Field.Value);
        Assert.Equal(SetupConfigDefaults.License, license.Field.Default);
    }

    [Fact]
    public void Config_supplied_field_reads_config_file_source()
    {
        // SC-002: an operator-supplied value reads source != default.
        var config = new SetupConfig { Identity = new SetupIdentityConfig { License = "Apache-2.0" } };
        ResolvedSetupConfig resolved = SetupConfigResolver.Resolve(config, flags: null, SetupAudience.New);

        ResolvedSetupField license = resolved.Find(SetupKeys.IdentityLicense)!;
        Assert.Equal(ConfigSource.ConfigFile, license.Field.Source);
        Assert.Equal("Apache-2.0", license.Field.Value);
        Assert.True(resolved.IsCustom(SetupKeys.IdentityLicense));
    }

    [Fact]
    public void Flag_overrides_config_file()
    {
        // FR-008: an explicit flag wins over the matching --config field.
        var config = new SetupConfig { Identity = new SetupIdentityConfig { Name = "FromConfig", Company = "FromConfig" } };
        var flags = new SetupFlagOverrides(Name: "FromFlag", Company: "FromFlag");
        ResolvedSetupConfig resolved = SetupConfigResolver.Resolve(config, flags, SetupAudience.New);

        Assert.Equal("FromFlag", resolved.ValueOrDefault(SetupKeys.IdentityName));
        Assert.Equal(ConfigSource.Flag, resolved.Find(SetupKeys.IdentityName)!.Field.Source);
    }

    [Fact]
    public void Authors_derive_from_company_when_absent()
    {
        var config = new SetupConfig { Identity = new SetupIdentityConfig { Company = "Acme" } };
        ResolvedSetupConfig resolved = SetupConfigResolver.Resolve(config, flags: null, SetupAudience.New);

        ResolvedSetupField authors = resolved.Find(SetupKeys.IdentityAuthors)!;
        Assert.Equal("Acme", authors.Field.Value);
        Assert.Equal(ConfigSource.Derived, authors.Field.Source);
    }

    [Fact]
    public void Install_audience_excludes_new_only_keys()
    {
        ResolvedSetupConfig resolved = SetupConfigResolver.Resolve(new SetupConfig(), flags: null, SetupAudience.Install);

        Assert.Null(resolved.Find(SetupKeys.IdentityOutput));      // new-only
        Assert.Null(resolved.Find(SetupKeys.IdentityDescription)); // new-only
        Assert.NotNull(resolved.Find(SetupKeys.VersioningNextVersion)); // Both — present
        Assert.NotNull(resolved.Find(SetupKeys.Agents));                // Both — present
    }

    [Fact]
    public void Interactive_source_is_recorded_distinctly()
    {
        var config = new SetupConfig { Versioning = new SetupVersioningConfig { NextVersion = "1.2.3" } };
        ResolvedSetupConfig resolved = SetupConfigResolver.Resolve(
            config, flags: null, SetupAudience.New, ConfigSource.Interactive);

        Assert.Equal(ConfigSource.Interactive, resolved.Find(SetupKeys.VersioningNextVersion)!.Field.Source);
    }
}
