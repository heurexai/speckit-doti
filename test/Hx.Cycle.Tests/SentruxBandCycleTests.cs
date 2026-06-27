using Hx.Cycle.Core;
using Hx.Tooling.Contracts;
using Xunit;
using static Hx.Cycle.Tests.CycleTestFixtures;

namespace Hx.Cycle.Tests;

/// <summary>T023 (FR-030/SC-014/SC-015): the two-optimization-try diagnostic and the rebaseline authorization gate.</summary>
public sealed class SentruxBandCycleTests
{
    private const string Band = SentruxOptimizationTracker.EscalationBandVerdict;

    [Fact]
    public void Two_escalation_band_attempts_require_a_structural_review()
    {
        string dir = NewTempDir();
        try
        {
            SentruxOptimizationResult first = SentruxOptimizationTracker.Record(dir, "008-f", Band);
            Assert.Equal(SentruxOptimizationVerdict.AttemptRecorded, first.Verdict);
            Assert.Equal(1, first.Attempts);

            SentruxOptimizationResult second = SentruxOptimizationTracker.Record(dir, "008-f", Band);
            Assert.Equal(SentruxOptimizationVerdict.StructuralReviewRequired, second.Verdict);
            Assert.Equal(2, second.Attempts);
            Assert.Contains("structural architecture review", second.NextAction!);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void A_non_band_verdict_clears_the_optimization_tally()
    {
        string dir = NewTempDir();
        try
        {
            SentruxOptimizationTracker.Record(dir, "008-f", Band); // attempt 1
            SentruxOptimizationResult cleared = SentruxOptimizationTracker.Record(dir, "008-f", "pass");
            Assert.Equal(SentruxOptimizationVerdict.Cleared, cleared.Verdict);

            // After clearing, the next band attempt starts at 1 again (not escalated).
            SentruxOptimizationResult next = SentruxOptimizationTracker.Record(dir, "008-f", Band);
            Assert.Equal(1, next.Attempts);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Different_features_count_independently()
    {
        string dir = NewTempDir();
        try
        {
            SentruxOptimizationTracker.Record(dir, "008-a", Band);
            SentruxOptimizationResult b = SentruxOptimizationTracker.Record(dir, "008-b", Band);
            Assert.Equal(1, b.Attempts); // feature b is independent of a
        }
        finally { Directory.Delete(dir, true); }
    }

    [Theory]
    [InlineData(false, true, true, false)]  // no operator intent → refused
    [InlineData(true, false, true, false)]  // no fresh arch-review → refused
    [InlineData(true, true, false, false)]  // not classified as functionality-driven growth → refused
    [InlineData(true, true, true, true)]    // all three → authorized
    public void Rebaseline_is_authorized_only_with_intent_fresh_review_and_growth_classification(
        bool intent, bool fresh, bool classified, bool expected)
    {
        SentruxRebaselineAuthorization auth = SentruxRebaselinePolicy.Evaluate(intent, fresh, classified);

        Assert.Equal(expected, auth.Authorized);
        Assert.False(string.IsNullOrWhiteSpace(auth.Reason));
    }

    // M-2/T026/FR-031: the "change-set-fresh arch-review" gate must compare the arch-review proof's stamped
    // ChangeSetId to the CURRENT diff identity — stage freshness alone leaves a regression-laundering window because
    // arch-review is a `review` stage whose freshness does not bind the change set. These pin the binding directly.
    [Fact]
    public void Arch_review_is_change_set_fresh_only_when_its_stamped_identity_matches_the_current_diff()
    {
        var archReview = new CycleStageProof("arch-review", CycleStageOutcome.Stamped, "IDENTITY-AT-STAMP", [], null);
        var state = new CycleState(1, "008-f", "base", "implement", [archReview]);

        Assert.True(SentruxRebaselinePolicy.ArchReviewChangeSetFresh(state, "IDENTITY-AT-STAMP"));
        // The code changed after arch-review was stamped → the current diff identity moved → STALE → refused.
        Assert.False(SentruxRebaselinePolicy.ArchReviewChangeSetFresh(state, "IDENTITY-AFTER-CODE-EDIT"));
    }

    [Fact]
    public void Arch_review_change_set_freshness_fails_closed_without_a_record()
    {
        Assert.False(SentruxRebaselinePolicy.ArchReviewChangeSetFresh(null, "ID"));

        var noArchReview = new CycleState(1, "008-f", "base", "implement",
            [new CycleStageProof("plan", CycleStageOutcome.Stamped, "ID", [], null)]);
        Assert.False(SentruxRebaselinePolicy.ArchReviewChangeSetFresh(noArchReview, "ID"));
    }
}
