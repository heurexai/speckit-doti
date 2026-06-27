using Hx.Impact.Core.ChangeDetection;
using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

/// <summary>
/// Resolves the docs-only gate scope (FR-028): the architecture + Sentrux steps are skipped ONLY when the change is
/// docs/prose-only — proven by the AND of "the affected-test planner says no-tests-required" and "the review context
/// says prose/docs-only". The AND is the safety: a generated-code-template change (<c>scaffold/templates/**</c>) has
/// no test impact (planner = no-tests-required) but the review context classifies it as CODE, so the skip does NOT
/// fire (M-1). The same resolver runs at gate time and at proof validation, so a scope skip is provable-not-bypassed.
/// </summary>
public static class GateScopeResolver
{
    /// <summary>The steps a docs-only scope skips (architecture + Sentrux measure implemented code).</summary>
    public static readonly IReadOnlyList<string> ScopeSkippableSteps = ["sentrux-verify", "architecture-test", "sentrux-check"];

    /// <summary>Pure: a change is docs-only ONLY when BOTH signals agree (M-1).</summary>
    public static bool IsDocsOnly(AffectedPlan affectedPlan, ReviewContext review) =>
        affectedPlan.Outcome == AffectedOutcome.NoTestsRequired && review.IsDocsOnly;

    public static GateScope Resolve(string repositoryRoot, string baseRef, string headRef, AffectedPlan affectedPlan)
    {
        ChangeSetContext changeSet = new ChangeSetContextBuilder().BuildForRepo(repositoryRoot, baseRef, headRef);
        ReviewContext review = new ReviewContextProjector(LayerMap.Load(repositoryRoot)).Project(changeSet);
        bool docsOnly = changeSet.RefsResolved && IsDocsOnly(affectedPlan, review);
        string reason = docsOnly
            ? "scope: docs-only — affected plan is no-tests-required AND review context is prose/docs-only; architecture + Sentrux skipped (FR-028)"
            : $"scope: not docs-only (affected={affectedPlan.Outcome}, reviewDocsOnly={review.IsDocsOnly}, categories=[{string.Join(",", review.Categories)}])";
        return new GateScope(
            JsonContractDefaults.SchemaVersion, docsOnly, reason, docsOnly ? ScopeSkippableSteps : []);
    }
}
