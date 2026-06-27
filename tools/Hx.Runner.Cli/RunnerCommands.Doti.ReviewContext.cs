using Hx.Cli.Kernel;
using Hx.Cycle.Core;
using Hx.Doti.Core;
using Hx.Impact.Core.ChangeDetection;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

/// <summary>009 FR-008: the arch-review context envelope — the lens applicability projection PLUS the fresh §2
/// constitution, composed in the RUNNER (never in <c>Hx.Cycle.Core</c>, so no Cycle→Doti core edge). Arch-review reads
/// <c>data.review.*</c> for lenses and <c>data.constitution.section2Content</c> for the §2 it evaluates against.</summary>
public sealed record ReviewContextWithConstitution(ReviewContext Review, ConstitutionReadResult Constitution);

public static partial class RunnerCommands
{
    /// <summary>FR-025 + 009 FR-008: project the current change set into the arch-review lens applicability (which
    /// lenses apply vs skip + the change categories) AND compose the fresh §2 constitution, so <c>/06</c> receives both
    /// as the OUTPUT of the one command it already runs — the constitution delivery is codified, not an agent-skippable
    /// step (surface-and-proceed when absent, FR-016). Thin: build the change set, project, read the constitution, render.</summary>
    public static CliResult DotiReviewContext(CliMeta meta, string repo, string baseRef)
    {
        string root = Path.GetFullPath(repo);
        string resolvedBase = !string.IsNullOrWhiteSpace(baseRef)
            ? baseRef
            : new CycleStateStore(root).Read()?.BaseRef ?? "HEAD";

        ChangeSetContext changeSet = new ChangeSetContextBuilder().BuildForRepo(root, resolvedBase, "HEAD");
        if (!changeSet.RefsResolved)
        {
            return CliResults.Fail(meta, "doti review-context", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed,
                    changeSet.UnresolvedReason ?? "Could not resolve the change set.", target: "--base")],
                "Could not resolve the change set for review context.");
        }

        ReviewContext review = new ReviewContextProjector(LayerMap.Load(root)).Project(changeSet);
        ConstitutionReadResult constitution = ConstitutionService.Read(root); // 009 FR-008: codified §2 delivery
        var envelope = new ReviewContextWithConstitution(review, constitution);

        string lensPart = review.IsDocsOnly
            ? $"Docs/prose-only change: {review.ApplicableLenses.Count} lens(es) apply, {review.SkippedLenses.Count} skipped."
            : $"{review.Categories.Count} change categor(ies); {review.ApplicableLenses.Count} arch-review lens(es) apply, {review.SkippedLenses.Count} skipped.";
        string constitutionPart = constitution.Exists
            ? " Constitution §2 attached for the review."
            : " No constitution (run /doti-constitution) — proceeding; §1 stays gate-enforced.";
        return CliResults.Ok(meta, "doti review-context", lensPart + constitutionPart, envelope);
    }
}
