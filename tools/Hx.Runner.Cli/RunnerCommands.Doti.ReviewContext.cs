using Hx.Cli.Kernel;
using Hx.Cycle.Core;
using Hx.Impact.Core.ChangeDetection;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    /// <summary>FR-025: project the current change set into the arch-review lens applicability (which lenses apply vs
    /// skip + the change categories), so <c>/06</c> reads triage as data and the docs-only gate skip has a machine-
    /// checkable category set. Thin: build the change set, project, render.</summary>
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
        string summary = review.IsDocsOnly
            ? $"Docs/prose-only change: {review.ApplicableLenses.Count} lens(es) apply, {review.SkippedLenses.Count} skipped."
            : $"{review.Categories.Count} change categor(ies); {review.ApplicableLenses.Count} arch-review lens(es) apply, {review.SkippedLenses.Count} skipped.";
        return CliResults.Ok(meta, "doti review-context", summary, review);
    }
}
