namespace Hx.Tooling.Contracts.Setup;

/// <summary>
/// 029 D1/D3: the SINGLE registry of operator-configurable keys — the one table the resolver, the projection
/// orchestration, the human formatter, and <c>config show</c> all iterate. Adding a key is a single row here (plus a
/// nullable field on <see cref="SetupConfig"/> and the writer that consumes its <see cref="SetupKey.Target"/>). The
/// stable <see cref="SetupKey.Id"/> is the canonical key in the <c>config show</c> JSON. Pure; no IO.
/// </summary>
public static class SetupKeys
{
    // identity
    public const string IdentityName = "identity.name";
    public const string IdentityCompany = "identity.company";
    public const string IdentityOutput = "identity.output";
    public const string IdentityDescription = "identity.description";
    public const string IdentityAuthors = "identity.authors";
    public const string IdentityRepositoryUrl = "identity.repositoryUrl";
    public const string IdentityLicense = "identity.license";

    // versioning
    public const string VersioningNextVersion = "versioning.nextVersion";

    // release
    public const string ReleaseEnvironmentVariable = "release.environmentVariable";
    public const string ReleaseDirectory = "release.directory";
    public const string ReleaseEnabled = "release.enabled";

    // publish
    public const string PublishEnabled = "publish.enabled";
    public const string PublishOwner = "publish.owner";
    public const string PublishRepo = "publish.repo";
    public const string PublishWorkflow = "publish.workflow";
    public const string PublishEnvironment = "publish.environment";
    public const string PublishTarget = "publish.target";

    // agents
    public const string Agents = "agents";

    // constitution §2
    public const string ConstitutionDomainPrinciples = "constitution.domainPrinciples";
    public const string ConstitutionTechStack = "constitution.techStack";
    public const string ConstitutionCodingStyle = "constitution.codingStyle";
    public const string ConstitutionSecurityCompliance = "constitution.securityCompliance";
    public const string ConstitutionPerformance = "constitution.performance";

    /// <summary>The ordered key registry — grouped for the human table, with each key's audience + projection target.</summary>
    public static readonly IReadOnlyList<SetupKey> All =
    [
        // --- identity ---
        new(IdentityName, SetupGroup.Identity, SetupAudience.Both, SetupTarget.TemplateToken, "",
            "Source name substituted into projects/namespaces/package-id suffix; integration name on install."),
        new(IdentityCompany, SetupGroup.Identity, SetupAudience.New, SetupTarget.TemplateToken, SetupConfigDefaults.Company,
            "<Company>, <Authors>, the PackageId prefix, and the copyright holder."),
        new(IdentityOutput, SetupGroup.Identity, SetupAudience.New, SetupTarget.None, "",
            "Output directory for the generated repo (location only; never substituted)."),
        new(IdentityDescription, SetupGroup.Identity, SetupAudience.New, SetupTarget.CsprojMetadata, SetupConfigDefaults.Description,
            "<Description> on the CLI package."),
        new(IdentityAuthors, SetupGroup.Identity, SetupAudience.New, SetupTarget.CsprojMetadata, SetupConfigDefaults.AuthorsDefaultMarker,
            "<Authors> independent of company (defaults to company)."),
        new(IdentityRepositoryUrl, SetupGroup.Identity, SetupAudience.New, SetupTarget.CsprojMetadata, SetupConfigDefaults.RepositoryUrl,
            "<RepositoryUrl> / <PackageProjectUrl> — the source link on the NuGet package."),
        new(IdentityLicense, SetupGroup.Identity, SetupAudience.New, SetupTarget.CsprojMetadata, SetupConfigDefaults.License,
            "<PackageLicenseExpression> — an SPDX id/expression."),

        // --- versioning ---
        new(VersioningNextVersion, SetupGroup.Versioning, SetupAudience.Both, SetupTarget.GitVersionSeed, SetupConfigDefaults.NextVersion,
            "GitVersion.yml next-version — the version-series start (the first release tags v0.1.0)."),

        // --- release & local output ---
        new(ReleaseEnvironmentVariable, SetupGroup.Release, SetupAudience.Both, SetupTarget.ReleaseManifest, SetupConfigDefaults.ReleaseEnvironmentVariable,
            "release.json defaultReleaseRootEnvironmentVariable — the env-var name read for the local release root."),
        new(ReleaseDirectory, SetupGroup.Release, SetupAudience.Both, SetupTarget.None, SetupConfigDefaults.ReleaseDirectory,
            "Machine-local hx.config.json localReleaseOutput.directory (never persisted to tracked setup.json)."),
        new(ReleaseEnabled, SetupGroup.Release, SetupAudience.Both, SetupTarget.None, BoolText(SetupConfigDefaults.ReleaseEnabled),
            "Machine-local hx.config.json localReleaseOutput.enabled — the local-copy master switch."),

        // --- publish intent (never executed; FR-007 checklist only) ---
        new(PublishEnabled, SetupGroup.Publish, SetupAudience.Both, SetupTarget.None, BoolText(SetupConfigDefaults.PublishEnabled),
            "Whether the repo intends to publish (drives the operator checklist)."),
        new(PublishOwner, SetupGroup.Publish, SetupAudience.Both, SetupTarget.None, SetupConfigDefaults.PublishOwner,
            "NuGet OIDC policy owner (operator-side; never executed by hx)."),
        new(PublishRepo, SetupGroup.Publish, SetupAudience.Both, SetupTarget.None, SetupConfigDefaults.PublishRepo,
            "NuGet OIDC policy repo (operator-side; never executed)."),
        new(PublishWorkflow, SetupGroup.Publish, SetupAudience.Both, SetupTarget.None, SetupConfigDefaults.PublishWorkflow,
            "The workflow file the OIDC policy trusts."),
        new(PublishEnvironment, SetupGroup.Publish, SetupAudience.Both, SetupTarget.None, SetupConfigDefaults.PublishEnvironment,
            "The GitHub Environment gating OIDC issuance."),
        new(PublishTarget, SetupGroup.Publish, SetupAudience.Both, SetupTarget.None, SetupConfigDefaults.PublishTarget,
            "The publish target registry."),

        // --- agents ---
        new(Agents, SetupGroup.Agents, SetupAudience.Both, SetupTarget.AgentSet, SetupConfigDefaults.Agents,
            "Which agent skill trees + root entrypoints render (subset of claude, codex). Flag --agents overrides."),

        // --- constitution §2 ---
        new(ConstitutionDomainPrinciples, SetupGroup.Constitution, SetupAudience.Both, SetupTarget.ConstitutionSection2, SetupConfigDefaults.DomainPrinciples,
            "§2 Domain principles — what the project is + its domain invariants."),
        new(ConstitutionTechStack, SetupGroup.Constitution, SetupAudience.Both, SetupTarget.ConstitutionSection2, SetupConfigDefaults.TechStack,
            "§2 Tech stack — chosen libs/tools above the .NET 10 baseline."),
        new(ConstitutionCodingStyle, SetupGroup.Constitution, SetupAudience.Both, SetupTarget.ConstitutionSection2, SetupConfigDefaults.CodingStyle,
            "§2 Coding style — conventions beyond §1."),
        new(ConstitutionSecurityCompliance, SetupGroup.Constitution, SetupAudience.Both, SetupTarget.ConstitutionSection2, SetupConfigDefaults.SecurityCompliance,
            "§2 Security & compliance — posture beyond §1 hygiene/SAST."),
        new(ConstitutionPerformance, SetupGroup.Constitution, SetupAudience.Both, SetupTarget.ConstitutionSection2, SetupConfigDefaults.Performance,
            "§2 Performance — performance conventions."),
    ];

    private static readonly Dictionary<string, SetupKey> ById =
        All.ToDictionary(k => k.Id, StringComparer.Ordinal);

    /// <summary>The key descriptor for <paramref name="id"/>; throws if it is not a registered key.</summary>
    public static SetupKey ById_(string id) =>
        ById.TryGetValue(id, out SetupKey? key)
            ? key
            : throw new ArgumentException($"Unknown setup key '{id}'.", nameof(id));

    /// <summary>Lower-cased boolean text (the canonical JSON/string form for a bool default).</summary>
    public static string BoolText(bool value) => value ? "true" : "false";
}
