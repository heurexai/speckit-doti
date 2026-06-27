namespace Hx.Cycle.Core;

/// <summary>The arch-review lens ids — the panel's <em>Applies-when</em> table promoted to data (FR-025), so lens
/// selection is deterministic + assertable instead of skill prose. The ids match the lenses in
/// <c>.doti/core/templates/commands/doti-arch-review.md</c> (T018 asserts the skill's lens ids are a subset of these).</summary>
public static class ReviewLens
{
    public const string DesignSoundness = "design-soundness";
    public const string EdgeCasesFailureModes = "edge-cases-failure-modes";
    public const string DataContractIntegrity = "data-contract-integrity";
    public const string SecurityTrustBoundaries = "security-trust-boundaries";
    public const string BlastRadiusDependencies = "blast-radius-dependencies";
    public const string SimplerAlternative = "simpler-alternative";
    public const string ModularityDesignSmells = "modularity-design-smells";
    public const string TestabilityProof = "testability-proof";
    public const string FitWithCurrentArchitecture = "fit-with-current-architecture";

    public static readonly IReadOnlyList<string> All =
    [
        DesignSoundness, EdgeCasesFailureModes, DataContractIntegrity, SecurityTrustBoundaries,
        BlastRadiusDependencies, SimplerAlternative, ModularityDesignSmells, TestabilityProof,
        FitWithCurrentArchitecture,
    ];

    // The code lenses — applicable to any runtime/generated-template change; DataContractIntegrity is added only
    // for a contract change; DesignSoundness is almost always on.
    public static readonly IReadOnlyList<string> Code =
    [
        DesignSoundness, EdgeCasesFailureModes, SecurityTrustBoundaries, BlastRadiusDependencies,
        SimplerAlternative, ModularityDesignSmells, TestabilityProof, FitWithCurrentArchitecture,
    ];
}

/// <summary>The kind of change a path represents (FR-023) — a richer taxonomy than the affected-test planner's
/// internal generated/docs/broad/owned split, because arch-review triage distinguishes Doti prose, generated-code
/// templates, contracts, and a dependency-edge (project) change.</summary>
public enum ReviewCategory
{
    Generated,
    DocsOnly,
    DotiProse,
    Contract,
    GeneratedTemplate,
    RuntimeCode,
    DependencyEdge,
    Broad,
    Unknown,
}

/// <summary>
/// The deterministic review context for a change set (FR-025): the changed files + affected projects, the distinct
/// change categories, and the arch-review lenses that APPLY vs are SKIPPED — so <c>/06</c> injects one verbatim lens
/// list instead of re-deriving triage, and the docs-only gate skip (FR-028) reads a machine-checkable category set.
/// </summary>
public sealed record ReviewContext(
    int SchemaVersion,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<string> AffectedProjects,
    IReadOnlyList<ReviewCategory> Categories,
    IReadOnlyList<string> ApplicableLenses,
    IReadOnlyList<string> SkippedLenses,
    IReadOnlyList<string> EscalationReasons)
{
    /// <summary>True when the change is prose/docs only (no runtime, generated-code-template, contract, or
    /// dependency-edge change) — the precondition the docs-only gate skip (FR-028) ANDs with the affected plan.</summary>
    public bool IsDocsOnly =>
        Categories.Count > 0
        && Categories.All(c => c is ReviewCategory.DocsOnly or ReviewCategory.DotiProse or ReviewCategory.Generated);
}
