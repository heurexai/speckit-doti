using System.Text.Json;
using Hx.Tooling.Contracts;
using Hx.Tooling.Contracts.Setup;

namespace Hx.Doti.Core.Setup;

/// <summary>
/// 029 FR-003/D6: the single read/write authority for the tracked <c>.doti/setup.json</c> — the persisted operator
/// INTENT (not the derived/default fill) so re-runs (install, upgrades) read the same intent. <b>Machine-local fields
/// are NEVER written to the tracked file</b> (D6): <c>release.directory</c> + <c>release.enabled</c> are stripped (they
/// belong in the executable-adjacent <c>hx.config.json</c>), so an absolute <c>localOutput.directory</c> can never leak
/// into source control. Reads are fail-soft (absent/malformed → <c>null</c>); the write is atomic (temp + move).
/// </summary>
public static class SetupConfigStore
{
    public const string RelativePath = ".doti/setup.json";

    /// <summary>The persisted intent, or <c>null</c> when the file is absent or malformed.</summary>
    public static SetupConfig? Read(string repositoryRoot)
    {
        string path = FullPath(repositoryRoot);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SetupConfig>(File.ReadAllText(path), JsonContractSerializerOptions.Create());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Persist the repo-portable operator intent. Machine-local fields are stripped (D6), <c>schemaVersion</c> is
    /// normalized to 1, and a config with no portable intent is NOT written (no spurious all-default file).
    /// Returns the relative path written, or <c>null</c> when nothing portable was supplied.
    /// </summary>
    public static string? Write(string repositoryRoot, SetupConfig intent)
    {
        SetupConfig portable = StripMachineLocal(intent);
        if (!HasPortableIntent(portable))
        {
            return null;
        }

        string path = FullPath(repositoryRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        JsonSerializerOptions options = JsonContractSerializerOptions.Create();
        options.WriteIndented = true;
        options.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        string json = JsonSerializer.Serialize(portable with { SchemaVersion = 1 }, options);

        string temp = path + ".tmp-" + Guid.NewGuid().ToString("n");
        File.WriteAllText(temp, json);
        File.Move(temp, path, overwrite: true);
        return RelativePath;
    }

    /// <summary>
    /// 029 FR-003: persist the operator intent reconstructed from a <see cref="ResolvedSetupConfig"/> — every key
    /// whose source is NOT default becomes a setup.json field (the derived <c>authors→company</c> and flag values are
    /// intent too). Machine-local fields are stripped (D6). Returns the relative path written, or <c>null</c> when no
    /// non-default field was supplied (the no-config path writes nothing).
    /// </summary>
    public static string? WriteFromResolved(string repositoryRoot, ResolvedSetupConfig resolved) =>
        Write(repositoryRoot, ToIntent(resolved));

    /// <summary>Reconstruct the persisted-intent <see cref="SetupConfig"/> from the resolved model's non-default fields.</summary>
    public static SetupConfig ToIntent(ResolvedSetupConfig resolved)
    {
        string? Custom(string id) => resolved.IsCustom(id) ? resolved.ValueOrDefault(id) : null;
        bool? CustomBool(string id) => Custom(id) is { } v ? v == "true" : null;

        var identity = new SetupIdentityConfig
        {
            Name = Custom(SetupKeys.IdentityName),
            Company = Custom(SetupKeys.IdentityCompany),
            Output = Custom(SetupKeys.IdentityOutput),
            Description = Custom(SetupKeys.IdentityDescription),
            Authors = Custom(SetupKeys.IdentityAuthors),
            RepositoryUrl = Custom(SetupKeys.IdentityRepositoryUrl),
            License = Custom(SetupKeys.IdentityLicense),
        };
        var versioning = new SetupVersioningConfig { NextVersion = Custom(SetupKeys.VersioningNextVersion) };
        var release = new SetupReleaseConfig
        {
            EnvironmentVariable = Custom(SetupKeys.ReleaseEnvironmentVariable),
            Directory = Custom(SetupKeys.ReleaseDirectory),
            Enabled = CustomBool(SetupKeys.ReleaseEnabled),
        };
        var publish = new SetupPublishConfig
        {
            Enabled = CustomBool(SetupKeys.PublishEnabled),
            Owner = Custom(SetupKeys.PublishOwner),
            Repo = Custom(SetupKeys.PublishRepo),
            Workflow = Custom(SetupKeys.PublishWorkflow),
            Environment = Custom(SetupKeys.PublishEnvironment),
            Target = Custom(SetupKeys.PublishTarget),
        };
        var constitution = new SetupConstitutionConfig
        {
            DomainPrinciples = Custom(SetupKeys.ConstitutionDomainPrinciples),
            TechStack = Custom(SetupKeys.ConstitutionTechStack),
            CodingStyle = Custom(SetupKeys.ConstitutionCodingStyle),
            SecurityCompliance = Custom(SetupKeys.ConstitutionSecurityCompliance),
            Performance = Custom(SetupKeys.ConstitutionPerformance),
        };
        IReadOnlyList<string>? agents = Custom(SetupKeys.Agents) is { } a
            ? a.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : null;

        return new SetupConfig
        {
            SchemaVersion = 1,
            Identity = AnySet(identity) ? identity : null,
            Versioning = versioning.NextVersion is not null ? versioning : null,
            Release = AnySet(release) ? release : null,
            Publish = AnySet(publish) ? publish : null,
            Agents = agents,
            Constitution = AnySet(constitution) ? constitution : null,
        };
    }

    private static bool AnySet(SetupIdentityConfig i) =>
        i.Name is not null || i.Company is not null || i.Output is not null || i.Description is not null
        || i.Authors is not null || i.RepositoryUrl is not null || i.License is not null;

    private static bool AnySet(SetupReleaseConfig r) =>
        r.EnvironmentVariable is not null || r.Directory is not null || r.Enabled is not null;

    private static bool AnySet(SetupPublishConfig p) =>
        p.Enabled is not null || p.Owner is not null || p.Repo is not null
        || p.Workflow is not null || p.Environment is not null || p.Target is not null;

    private static bool AnySet(SetupConstitutionConfig c) =>
        c.DomainPrinciples is not null || c.TechStack is not null || c.CodingStyle is not null
        || c.SecurityCompliance is not null || c.Performance is not null;

    /// <summary>D6: drop the machine-local release fields (directory + enabled) before persisting to the tracked file.</summary>
    public static SetupConfig StripMachineLocal(SetupConfig intent)
    {
        if (intent.Release is null)
        {
            return intent;
        }

        SetupReleaseConfig stripped = intent.Release with { Directory = null, Enabled = null };
        bool releaseEmpty = stripped.EnvironmentVariable is null;
        return intent with { Release = releaseEmpty ? null : stripped };
    }

    private static bool HasPortableIntent(SetupConfig config) =>
        config.Identity is not null
        || config.Versioning is not null
        || config.Release is not null
        || config.Publish is not null
        || (config.Agents is { Count: > 0 })
        || config.Constitution is not null;

    private static string FullPath(string repositoryRoot) =>
        Path.GetFullPath(Path.Combine(repositoryRoot, RelativePath.Replace('/', Path.DirectorySeparatorChar)));
}
