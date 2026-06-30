using Hx.Tooling.Contracts.Setup;

namespace Hx.Doti.Core.Setup;

/// <summary>029 FR-005/D4: builds a <see cref="SetupConfig"/> from the wizard's collected answers (keyed by
/// <see cref="SetupKey.Id"/>), so the wizard re-enters the IDENTICAL <c>--config</c> resolve+project path (SC-004:
/// wizard == --config). Pure mapping; no IO. Lives in Doti.Core so both CLI hosts' wizards share it.</summary>
public static class SetupConfigBuilder
{
    public static SetupConfig FromAnswers(IReadOnlyDictionary<string, string> answers)
    {
        string? A(string key) => answers.TryGetValue(key, out string? v) && v.Length > 0 ? v : null;
        bool? B(string key) => A(key) is { } v ? string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) : null;

        var identity = new SetupIdentityConfig
        {
            Name = A(SetupKeys.IdentityName),
            Company = A(SetupKeys.IdentityCompany),
            Output = A(SetupKeys.IdentityOutput),
            Description = A(SetupKeys.IdentityDescription),
            Authors = A(SetupKeys.IdentityAuthors),
            RepositoryUrl = A(SetupKeys.IdentityRepositoryUrl),
            License = A(SetupKeys.IdentityLicense),
        };
        var versioning = new SetupVersioningConfig { NextVersion = A(SetupKeys.VersioningNextVersion) };
        var release = new SetupReleaseConfig
        {
            EnvironmentVariable = A(SetupKeys.ReleaseEnvironmentVariable),
            Directory = A(SetupKeys.ReleaseDirectory),
            Enabled = B(SetupKeys.ReleaseEnabled),
        };
        var publish = new SetupPublishConfig
        {
            Enabled = B(SetupKeys.PublishEnabled),
            Owner = A(SetupKeys.PublishOwner),
            Repo = A(SetupKeys.PublishRepo),
            Workflow = A(SetupKeys.PublishWorkflow),
            Environment = A(SetupKeys.PublishEnvironment),
            Target = A(SetupKeys.PublishTarget),
        };
        var constitution = new SetupConstitutionConfig
        {
            DomainPrinciples = A(SetupKeys.ConstitutionDomainPrinciples),
            TechStack = A(SetupKeys.ConstitutionTechStack),
            CodingStyle = A(SetupKeys.ConstitutionCodingStyle),
            SecurityCompliance = A(SetupKeys.ConstitutionSecurityCompliance),
            Performance = A(SetupKeys.ConstitutionPerformance),
        };
        IReadOnlyList<string>? agents = A(SetupKeys.Agents) is { } a
            ? a.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : null;

        return new SetupConfig
        {
            SchemaVersion = 1,
            Identity = Has(identity) ? identity : null,
            Versioning = versioning.NextVersion is not null ? versioning : null,
            Release = Has(release) ? release : null,
            Publish = Has(publish) ? publish : null,
            Agents = agents,
            Constitution = Has(constitution) ? constitution : null,
        };
    }

    private static bool Has(SetupIdentityConfig i) =>
        i.Name is not null || i.Company is not null || i.Output is not null || i.Description is not null
        || i.Authors is not null || i.RepositoryUrl is not null || i.License is not null;

    private static bool Has(SetupReleaseConfig r) =>
        r.EnvironmentVariable is not null || r.Directory is not null || r.Enabled is not null;

    private static bool Has(SetupPublishConfig p) =>
        p.Enabled is not null || p.Owner is not null || p.Repo is not null
        || p.Workflow is not null || p.Environment is not null || p.Target is not null;

    private static bool Has(SetupConstitutionConfig c) =>
        c.DomainPrinciples is not null || c.TechStack is not null || c.CodingStyle is not null
        || c.SecurityCompliance is not null || c.Performance is not null;
}
