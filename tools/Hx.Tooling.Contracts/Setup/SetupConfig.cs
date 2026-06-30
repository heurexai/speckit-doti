namespace Hx.Tooling.Contracts.Setup;

/// <summary>
/// 029 (FR-001/FR-002): the deserialized <c>.doti/setup.json</c> / <c>--config</c> DTO — the OPERATOR INTENT, all
/// fields nullable so an omitted field falls through to its documented default during resolution (never a structural
/// default that masks "unset"). Mirrors the <c>docs/configuration.md</c> operator subset: identity, versioning,
/// release local-output, publish intent, agents, and constitution §2. Pure (System.Text.Json only) — it lives in
/// Contracts so both input paths (<c>--config</c> + the wizard) and <c>config show</c> share ONE shape.
/// </summary>
public sealed record SetupConfig
{
    /// <summary>Schema guard; must be <c>1</c> (fail-closed, mirroring release.json/hx.config.json).</summary>
    public int? SchemaVersion { get; init; }

    public SetupIdentityConfig? Identity { get; init; }
    public SetupVersioningConfig? Versioning { get; init; }
    public SetupReleaseConfig? Release { get; init; }
    public SetupPublishConfig? Publish { get; init; }

    /// <summary>Which agent skill trees render — a subset of <c>claude</c>, <c>codex</c>.</summary>
    public IReadOnlyList<string>? Agents { get; init; }

    public SetupConstitutionConfig? Constitution { get; init; }
}

/// <summary>Project identity + package metadata (docs/configuration.md §1, operator subset).</summary>
public sealed record SetupIdentityConfig
{
    /// <summary>The source name substituted into projects/namespaces/package-id-suffix. Flag <c>--name</c> overrides.</summary>
    public string? Name { get; init; }

    /// <summary>The company/owner — <c>&lt;Company&gt;</c>, PackageId prefix, copyright holder. Flag <c>--company</c> overrides.</summary>
    public string? Company { get; init; }

    /// <summary>Output directory for the generated repo (location only; never substituted). Flag <c>--output</c> overrides. New-only.</summary>
    public string? Output { get; init; }

    /// <summary><c>&lt;Description&gt;</c> on the CLI package.</summary>
    public string? Description { get; init; }

    /// <summary><c>&lt;Authors&gt;</c> independent of company (defaults to company).</summary>
    public string? Authors { get; init; }

    /// <summary><c>&lt;RepositoryUrl&gt;</c> / <c>&lt;PackageProjectUrl&gt;</c>.</summary>
    public string? RepositoryUrl { get; init; }

    /// <summary><c>&lt;PackageLicenseExpression&gt;</c> — an SPDX id/expression.</summary>
    public string? License { get; init; }
}

/// <summary>Versioning (docs/configuration.md §2, operator subset).</summary>
public sealed record SetupVersioningConfig
{
    /// <summary><c>GitVersion.yml</c> <c>next-version</c> — the version-series start (3-part numeric SemVer core).</summary>
    public string? NextVersion { get; init; }
}

/// <summary>Release local output (docs/configuration.md §3, operator subset — the repo-tracked release.json fields).</summary>
public sealed record SetupReleaseConfig
{
    /// <summary><c>release.json defaultReleaseRootEnvironmentVariable</c> — the env-var NAME for the local release root.</summary>
    public string? EnvironmentVariable { get; init; }

    /// <summary><c>hx.config.json localReleaseOutput.directory</c> — MACHINE-LOCAL; never persisted to tracked setup.json (D6).</summary>
    public string? Directory { get; init; }

    /// <summary><c>hx.config.json localReleaseOutput.enabled</c> — the local-copy master switch. MACHINE-LOCAL (D6).</summary>
    public bool? Enabled { get; init; }
}

/// <summary>Publish intent (docs/configuration.md §4, operator subset — NuGet OIDC policy parameters; never executed, FR-007).</summary>
public sealed record SetupPublishConfig
{
    /// <summary>Whether the repo intends to publish (drives the NuGet sub-questions in the wizard).</summary>
    public bool? Enabled { get; init; }

    /// <summary>The repo owner in the nuget.org Trusted-Publishing policy.</summary>
    public string? Owner { get; init; }

    /// <summary>The repo name in the policy.</summary>
    public string? Repo { get; init; }

    /// <summary>The workflow file the policy trusts (default <c>release.yml</c>).</summary>
    public string? Workflow { get; init; }

    /// <summary>The GitHub Environment gating OIDC issuance (default <c>production</c>).</summary>
    public string? Environment { get; init; }

    /// <summary>The publish target registry (default <c>nuget.org</c>).</summary>
    public string? Target { get; init; }
}

/// <summary>Constitution §2 — the only operator-authored constitution content (docs/configuration.md §7).</summary>
public sealed record SetupConstitutionConfig
{
    public string? DomainPrinciples { get; init; }
    public string? TechStack { get; init; }
    public string? CodingStyle { get; init; }
    public string? SecurityCompliance { get; init; }
    public string? Performance { get; init; }
}
