namespace Hx.Tooling.Contracts.Setup;

/// <summary>029 FR-002/FR-008: the explicit flag overrides that win over a <c>--config</c> field (<c>--name</c>,
/// <c>--company</c>, <c>--output</c>, <c>--agents</c>). A null entry means the flag was not explicitly supplied.</summary>
public sealed record SetupFlagOverrides(
    string? Name = null,
    string? Company = null,
    string? Output = null,
    IReadOnlyList<string>? Agents = null);

/// <summary>
/// 029 FR-001/FR-002/D2: the SINGLE provenance resolver. Layers each key flag ▸ config-file/interactive ▸ derived ▸
/// default, recording the winning <see cref="ConfigSource"/>, and projects the result into a
/// <see cref="ResolvedSetupConfig"/> filtered to <paramref name="audience"/> (the install path drops new-only keys
/// from the resolved model; the projector reports any new-only key reached on install as ignored). Pure; no IO.
/// </summary>
public static class SetupConfigResolver
{
    /// <summary>
    /// Resolve <paramref name="config"/> (whose values carry <paramref name="fileSource"/> — config-file or
    /// interactive) against the documented defaults, with <paramref name="flags"/> winning. Only keys whose
    /// <see cref="SetupKey.Audience"/> includes <paramref name="audience"/> are emitted.
    /// </summary>
    public static ResolvedSetupConfig Resolve(
        SetupConfig? config,
        SetupFlagOverrides? flags,
        SetupAudience audience,
        ConfigSource fileSource = ConfigSource.ConfigFile)
    {
        config ??= new SetupConfig();
        flags ??= new SetupFlagOverrides();

        var fields = new List<ResolvedSetupField>();
        foreach (SetupKey key in SetupKeys.All)
        {
            if (!Applies(key.Audience, audience))
            {
                continue;
            }

            ConfigField field = ResolveKey(key, config, flags, fileSource);
            fields.Add(new ResolvedSetupField(key.Id, key.Group, key.Audience, key.Target, field));
        }

        return new ResolvedSetupConfig(ResolvedSetupConfig.CurrentSchemaVersion, audience, fields);
    }

    /// <summary>
    /// Whether a key declared for <paramref name="keyAudience"/> applies to the resolving <paramref name="audience"/>.
    /// A <c>Both</c> key always applies. A resolving audience of <c>Both</c> (e.g. <c>config show</c>) admits EVERY key.
    /// Otherwise the key's audience must match the resolving audience (New|Install).
    /// </summary>
    public static bool Applies(SetupAudience keyAudience, SetupAudience audience)
    {
        if (keyAudience == SetupAudience.Both || audience == SetupAudience.Both)
        {
            return true;
        }

        return keyAudience == audience;
    }

    private static ConfigField ResolveKey(SetupKey key, SetupConfig config, SetupFlagOverrides flags, ConfigSource fileSource)
    {
        // 1. Explicit flag (highest priority).
        string? flag = FlagValue(key.Id, flags);
        if (flag is not null)
        {
            return new ConfigField(flag, ConfigSource.Flag, key.Default);
        }

        // 2. Config-file / interactive value.
        string? fileValue = FileValue(key.Id, config);
        if (fileValue is not null)
        {
            return new ConfigField(fileValue, fileSource, key.Default);
        }

        // 3. Derived (authors → company).
        if (key.Id == SetupKeys.IdentityAuthors)
        {
            string? company = FlagValue(SetupKeys.IdentityCompany, flags) ?? FileValue(SetupKeys.IdentityCompany, config);
            if (company is not null)
            {
                return new ConfigField(company, ConfigSource.Derived, key.Default);
            }
        }

        // 4. Default.
        return new ConfigField(key.Default, ConfigSource.Default, key.Default);
    }

    private static string? FlagValue(string id, SetupFlagOverrides flags) => id switch
    {
        SetupKeys.IdentityName => NonEmpty(flags.Name),
        SetupKeys.IdentityCompany => NonEmpty(flags.Company),
        SetupKeys.IdentityOutput => NonEmpty(flags.Output),
        SetupKeys.Agents => flags.Agents is { Count: > 0 } a ? string.Join(',', a) : null,
        _ => null,
    };

    private static string? FileValue(string id, SetupConfig config) => id switch
    {
        SetupKeys.IdentityName => NonEmpty(config.Identity?.Name),
        SetupKeys.IdentityCompany => NonEmpty(config.Identity?.Company),
        SetupKeys.IdentityOutput => NonEmpty(config.Identity?.Output),
        SetupKeys.IdentityDescription => NonEmpty(config.Identity?.Description),
        SetupKeys.IdentityAuthors => NonEmpty(config.Identity?.Authors),
        SetupKeys.IdentityRepositoryUrl => NonEmpty(config.Identity?.RepositoryUrl),
        SetupKeys.IdentityLicense => NonEmpty(config.Identity?.License),
        SetupKeys.VersioningNextVersion => NonEmpty(config.Versioning?.NextVersion),
        SetupKeys.ReleaseEnvironmentVariable => NonEmpty(config.Release?.EnvironmentVariable),
        SetupKeys.ReleaseDirectory => NonEmpty(config.Release?.Directory),
        SetupKeys.ReleaseEnabled => config.Release?.Enabled is { } e ? SetupKeys.BoolText(e) : null,
        SetupKeys.PublishEnabled => config.Publish?.Enabled is { } pe ? SetupKeys.BoolText(pe) : null,
        SetupKeys.PublishOwner => NonEmpty(config.Publish?.Owner),
        SetupKeys.PublishRepo => NonEmpty(config.Publish?.Repo),
        SetupKeys.PublishWorkflow => NonEmpty(config.Publish?.Workflow),
        SetupKeys.PublishEnvironment => NonEmpty(config.Publish?.Environment),
        SetupKeys.PublishTarget => NonEmpty(config.Publish?.Target),
        SetupKeys.Agents => config.Agents is { Count: > 0 } a ? string.Join(',', a) : null,
        SetupKeys.ConstitutionDomainPrinciples => NonEmpty(config.Constitution?.DomainPrinciples),
        SetupKeys.ConstitutionTechStack => NonEmpty(config.Constitution?.TechStack),
        SetupKeys.ConstitutionCodingStyle => NonEmpty(config.Constitution?.CodingStyle),
        SetupKeys.ConstitutionSecurityCompliance => NonEmpty(config.Constitution?.SecurityCompliance),
        SetupKeys.ConstitutionPerformance => NonEmpty(config.Constitution?.Performance),
        _ => null,
    };

    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
