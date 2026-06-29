using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

/// <summary>The verdict of a Sentrux rebaseline authorization check (FR-031/SC-015).</summary>
public sealed record SentruxRebaselineAuthorization(bool Authorized, string Reason);

/// <summary>
/// FR-031/SC-015 (M-2): a Sentrux baseline may be RAISED (never lowered, never auto-created) only with explicit
/// operator intent AND a change-set-fresh arch-review record AND a machine-checkable classification that the growth
/// is functionality-driven — so a wrong-architecture regression cannot be laundered into a raised baseline. This
/// guards the OPERATOR rebaseline call site (the <c>sentrux baseline</c> command), NOT the
/// <c>SentruxBaselineRunner.Save</c> method — first-scaffold baseline creation (no cycle, no arch-review yet) must
/// still work. The gate path never calls Save; the baseline is never removed.
/// </summary>
public static class SentruxRebaselinePolicy
{
    /// <summary>The marker the arch-review record must carry to classify the growth as functionality-driven (not prose).</summary>
    public const string GrowthClassificationMarker = "sentrux-rebaseline: functionality-driven-growth";

    /// <summary>Pure: authorize only when ALL three conditions hold; any missing one refuses (fail-closed).</summary>
    public static SentruxRebaselineAuthorization Evaluate(bool operatorIntent, bool archReviewFresh, bool growthClassified)
    {
        if (!operatorIntent)
        {
            return new SentruxRebaselineAuthorization(false,
                "no explicit operator intent — re-run with --authorize-rebaseline only if the baseline should rise");
        }

        if (!archReviewFresh)
        {
            return new SentruxRebaselineAuthorization(false,
                "no change-set-fresh arch-review record — run /04-doti-arch-review for the current change before rebaselining");
        }

        if (!growthClassified)
        {
            return new SentruxRebaselineAuthorization(false,
                $"the arch-review record does not classify the growth as functionality-driven (expected the marker `{GrowthClassificationMarker}`) — refactor if the architecture is wrong; do not launder a regression into a raised baseline");
        }

        return new SentruxRebaselineAuthorization(true,
            "authorized: explicit operator intent + a change-set-fresh arch-review + functionality-driven-growth classification");
    }

    public static SentruxRebaselineAuthorization Authorize(string repositoryRoot, bool operatorIntent)
    {
        string? feature = new CycleStateStore(repositoryRoot).Read()?.Feature;
        if (string.IsNullOrWhiteSpace(feature))
        {
            return new SentruxRebaselineAuthorization(false,
                "no active cycle feature to bind the rebaseline arch-review to");
        }

        return Evaluate(operatorIntent, ArchReviewFresh(repositoryRoot), GrowthClassified(repositoryRoot, feature));
    }

    private static bool ArchReviewFresh(string repositoryRoot)
    {
        try
        {
            CycleState? state = new CycleStateStore(repositoryRoot).Read();
            // M-2/T026: the arch-review evidence must be FOR THE CURRENT CHANGE — its stamped ChangeSetId must equal the
            // current diff identity. Stage freshness ALONE is insufficient: arch-review is a `review` stage with no
            // `diff` prerequisite, so FreshnessEvaluator never compares its ChangeSetId and it stays `Fresh` across pure
            // code edits — the exact regression-laundering window FR-031 closes (stamp arch-review → edit code → the
            // arch-review record is stale, yet stage-fresh). Bind it to the diff explicitly, fail-closed.
            if (!ArchReviewChangeSetFresh(state, ChangeSetIdentity.Of(repositoryRoot, state?.BaseRef ?? "HEAD", "HEAD")))
            {
                return false;
            }

            // The arch-review artifact (its .md + transitive prerequisite content) must also still be fresh.
            return new CycleService(repositoryRoot).Status().Freshness
                .Any(f => string.Equals(f.Stage, "arch-review", StringComparison.OrdinalIgnoreCase)
                    && f.Freshness == StageFreshness.Fresh);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            return false; // fail closed if the cycle state cannot be read/evaluated
        }
    }

    /// <summary>M-2/T026 (pure, testable): the arch-review proof is change-set-fresh iff it exists AND its stamped
    /// <see cref="CycleStageProof.ChangeSetId"/> equals the current diff identity — a stale record (stamped before the
    /// code under review) is refused, so it cannot authorize a rebaseline.</summary>
    public static bool ArchReviewChangeSetFresh(CycleState? state, string currentChangeSetId)
    {
        CycleStageProof? archReview = state?.Stages
            .FirstOrDefault(p => string.Equals(p.Stage, "arch-review", StringComparison.OrdinalIgnoreCase));
        return archReview is not null
            && string.Equals(archReview.ChangeSetId, currentChangeSetId, StringComparison.Ordinal);
    }

    private static bool GrowthClassified(string repositoryRoot, string feature)
    {
        string path = Path.Combine(repositoryRoot, "docs", "reviews", $"{feature}-arch-review.md");
        return File.Exists(path)
            && File.ReadAllText(path).Contains(GrowthClassificationMarker, StringComparison.OrdinalIgnoreCase);
    }
}
