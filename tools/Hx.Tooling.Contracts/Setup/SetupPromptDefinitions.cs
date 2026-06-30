namespace Hx.Tooling.Contracts.Setup;

/// <summary>029 FR-005/D4: a conditional-enable predicate over the answers collected so far — modeled as DATA (not CLI
/// branching). E.g. the NuGet sub-questions are enabled only when <c>publish.enabled == true</c>.</summary>
public sealed record SetupEnabledWhen(string Key, string EqualsValue);

/// <summary>
/// 029 FR-005/D4: ONE wizard prompt — pure data the CLI loop iterates (it never branches on the key). Each maps 1:1
/// to a <see cref="SetupKey"/> via <see cref="Key"/>: the prose <see cref="Question"/>, the <see cref="Default"/>
/// shown, the one-line <see cref="WhatItAffects"/>, an optional <see cref="EnabledWhen"/> conditional, and whether it
/// is <see cref="FreeText"/> (multi-word prose like §2) or a single-token answer. Lives in Contracts so the wizard
/// (CLI) stays a dumb iterator and SC-004 (wizard == --config) is provable on scripted input.
/// </summary>
public sealed record SetupPromptDefinition(
    string Key,
    SetupGroup Group,
    SetupAudience Audience,
    string Question,
    string Default,
    string WhatItAffects,
    bool FreeText = false,
    SetupEnabledWhen? EnabledWhen = null);

/// <summary>029 FR-005/D4: the ordered, pure wizard prompt set — one definition per operator-configurable key, grouped
/// identity → versioning → release → publish → agents → constitution, with the publish sub-questions gated behind
/// <c>publish.enabled</c>. The CLI loop reads this; adding a prompt is a registry row.</summary>
public static class SetupPromptDefinitions
{
    public static readonly IReadOnlyList<SetupPromptDefinition> All =
    [
        // identity
        new(SetupKeys.IdentityName, SetupGroup.Identity, SetupAudience.Both,
            "Project / repo name", "", "Substituted into projects, namespaces, the package-id suffix, and the CLI command."),
        new(SetupKeys.IdentityCompany, SetupGroup.Identity, SetupAudience.New,
            "Company / owner", SetupConfigDefaults.Company, "Drives <Company>, <Authors>, the PackageId prefix, and the copyright holder."),
        new(SetupKeys.IdentityDescription, SetupGroup.Identity, SetupAudience.New,
            "Package description", SetupConfigDefaults.Description, "Sets <Description> on the CLI package.", FreeText: true),
        new(SetupKeys.IdentityAuthors, SetupGroup.Identity, SetupAudience.New,
            "Authors (blank = company)", SetupConfigDefaults.AuthorsDefaultMarker, "Sets <Authors> independent of company.", FreeText: true),
        new(SetupKeys.IdentityRepositoryUrl, SetupGroup.Identity, SetupAudience.New,
            "Repository URL (blank = none)", "", "Sets <RepositoryUrl> / <PackageProjectUrl> — the source link on the package."),
        new(SetupKeys.IdentityLicense, SetupGroup.Identity, SetupAudience.New,
            "License (SPDX id)", SetupConfigDefaults.License, "Sets <PackageLicenseExpression>."),

        // versioning
        new(SetupKeys.VersioningNextVersion, SetupGroup.Versioning, SetupAudience.Both,
            "Version series seed (SemVer)", SetupConfigDefaults.NextVersion, "Sets GitVersion.yml next-version; the first release tags v<this>."),

        // release
        new(SetupKeys.ReleaseEnvironmentVariable, SetupGroup.Release, SetupAudience.Both,
            "Local-release env-var name", SetupConfigDefaults.ReleaseEnvironmentVariable, "Sets release.json defaultReleaseRootEnvironmentVariable."),

        // publish (gated)
        new(SetupKeys.PublishEnabled, SetupGroup.Publish, SetupAudience.Both,
            "Will this repo publish to NuGet? (true/false)", SetupKeys.BoolText(SetupConfigDefaults.PublishEnabled), "Surfaces the NuGet OIDC checklist (never executed by hx)."),
        new(SetupKeys.PublishOwner, SetupGroup.Publish, SetupAudience.Both,
            "NuGet OIDC policy owner", "", "The repo owner in the nuget.org Trusted-Publishing policy.",
            EnabledWhen: new SetupEnabledWhen(SetupKeys.PublishEnabled, "true")),
        new(SetupKeys.PublishRepo, SetupGroup.Publish, SetupAudience.Both,
            "NuGet OIDC policy repo", "", "The repo name in the policy.",
            EnabledWhen: new SetupEnabledWhen(SetupKeys.PublishEnabled, "true")),
        new(SetupKeys.PublishWorkflow, SetupGroup.Publish, SetupAudience.Both,
            "NuGet OIDC trusted workflow", SetupConfigDefaults.PublishWorkflow, "The workflow file the policy trusts.",
            EnabledWhen: new SetupEnabledWhen(SetupKeys.PublishEnabled, "true")),
        new(SetupKeys.PublishEnvironment, SetupGroup.Publish, SetupAudience.Both,
            "GitHub Environment", SetupConfigDefaults.PublishEnvironment, "The Environment gating OIDC issuance.",
            EnabledWhen: new SetupEnabledWhen(SetupKeys.PublishEnabled, "true")),

        // agents
        new(SetupKeys.Agents, SetupGroup.Agents, SetupAudience.Both,
            "Agent toolchains (comma-separated: claude,codex)", SetupConfigDefaults.Agents, "Which agent skill trees + root entrypoints render."),

        // constitution §2
        new(SetupKeys.ConstitutionDomainPrinciples, SetupGroup.Constitution, SetupAudience.Both,
            "§2 Domain principles (blank = leave placeholder)", "", "Fills §2 Domain principles in the constitution.", FreeText: true),
        new(SetupKeys.ConstitutionTechStack, SetupGroup.Constitution, SetupAudience.Both,
            "§2 Tech stack (blank = leave placeholder)", "", "Fills §2 Tech stack.", FreeText: true),
        new(SetupKeys.ConstitutionCodingStyle, SetupGroup.Constitution, SetupAudience.Both,
            "§2 Coding style (blank = leave placeholder)", "", "Fills §2 Coding style.", FreeText: true),
        new(SetupKeys.ConstitutionSecurityCompliance, SetupGroup.Constitution, SetupAudience.Both,
            "§2 Security & compliance (blank = leave placeholder)", "", "Fills §2 Security & compliance.", FreeText: true),
        new(SetupKeys.ConstitutionPerformance, SetupGroup.Constitution, SetupAudience.Both,
            "§2 Performance (blank = leave placeholder)", "", "Fills §2 Performance.", FreeText: true),
    ];
}
