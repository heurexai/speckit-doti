namespace Hx.Tooling.Contracts.Setup;

/// <summary>029 D3: which projection target a key writes into. The projector maps a target to an injected
/// <see cref="ISetupTargetWriter"/> the caller supplies, so the orchestration stays pure (Contracts) while the IO
/// writers live at their asset (Doti.Core/Scaffold.Core). <see cref="None"/> = informational-only (e.g. publish intent
/// is surfaced in a checklist, never projected — FR-007).</summary>
public enum SetupTarget
{
    /// <summary>Not projected to a file (informational / checklist-only, e.g. publish intent).</summary>
    None,

    /// <summary>The dotnet-new template tokens (<c>--name</c>/<c>--company</c>) — applied during generation, not post-projected.</summary>
    TemplateToken,

    /// <summary><c>&lt;Description&gt;</c>/<c>&lt;RepositoryUrl&gt;</c>/<c>&lt;PackageLicenseExpression&gt;</c>/<c>&lt;Authors&gt;</c> in the CLI .csproj.</summary>
    CsprojMetadata,

    /// <summary><c>GitVersion.yml</c> <c>next-version</c>.</summary>
    GitVersionSeed,

    /// <summary><c>.doti/release.json</c> <c>defaultReleaseRootEnvironmentVariable</c>.</summary>
    ReleaseManifest,

    /// <summary>The constitution §2 sections in <c>.doti/memory/constitution.md</c>.</summary>
    ConstitutionSection2,

    /// <summary>The repo-specific Doti integration metadata (the <c>agents</c> set) — handled by the existing install metadata path.</summary>
    AgentSet,
}

/// <summary>
/// 029 FR-001/D1: the descriptor for ONE operator-configurable key — its stable id, the documented
/// <see cref="Default"/>, the <see cref="Group"/> it drives, the <see cref="Audience"/> (New|Install|Both), and the
/// projection <see cref="Target"/>. The <see cref="SetupKeys"/> registry is the single source consumed by the
/// resolver, the projector table, the formatter, and <c>config show</c> — adding a key is one registry row.
/// </summary>
public sealed record SetupKey(
    string Id,
    SetupGroup Group,
    SetupAudience Audience,
    SetupTarget Target,
    string Default,
    string What);
