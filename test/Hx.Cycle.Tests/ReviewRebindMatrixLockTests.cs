using Hx.Cycle.Core;
using Hx.Tooling.Contracts;
using Xunit;
using static Hx.Cycle.Tests.CycleTestFixtures;

namespace Hx.Cycle.Tests;

/// <summary>
/// 028 FR-009 / T020: lock the reconciliation-matrix cells the agent-gated rebind must NOT relax. A change-set-bound
/// stage stale on a (doc-only) prerequisite content change stays <see cref="RestampSafety.RerunRequired"/> — never the
/// agent-attestable <see cref="RestampSafety.ReviewedNoImpact"/> tier; a review-kind stage likewise. The
/// <see cref="RestampSafetyClassifier.Classify(StaleReason, bool)"/> agent-eligibility-aware map is the lock.
/// </summary>
public sealed class ReviewRebindMatrixLockTests
{
    [Fact]
    public void Change_set_bound_prereq_content_change_is_never_the_reviewed_no_impact_tier()
    {
        // A change-set-bound dependent's prereq content change is PrereqArtifactChanged, but attestable=false ⇒
        // it stays RerunRequired (the agent gate is unreachable). The single-arg map already returns RerunRequired.
        Assert.Equal(RestampSafety.RerunRequired,
            RestampSafetyClassifier.Classify(StaleReason.PrereqArtifactChanged, attestable: false));
    }

    [Fact]
    public void Attestable_prereq_content_change_is_the_reviewed_no_impact_tier()
    {
        Assert.Equal(RestampSafety.ReviewedNoImpact,
            RestampSafetyClassifier.Classify(StaleReason.PrereqArtifactChanged, attestable: true));
    }

    [Theory]
    [InlineData(StaleReason.OwnArtifactChanged)]
    [InlineData(StaleReason.ChangeSetDiffers)]
    [InlineData(StaleReason.PrereqRebindable)]
    [InlineData(StaleReason.NotProduced)]
    public void Non_prereq_changed_reasons_are_unchanged_even_when_attestable_is_true(StaleReason reason) =>
        // Only PrereqArtifactChanged can ever reach the agent gate; the attestable flag is inert for every other reason.
        Assert.Equal(RestampSafetyClassifier.Classify(reason),
            RestampSafetyClassifier.Classify(reason, attestable: true));

    [Fact]
    public void The_recovery_planner_surfaces_review_rebind_only_for_an_attestable_doc_stage()
    {
        // The spec+plan union: editing the spec stales the doc plan (attestable ⇒ ReviewedNoImpact) but a change-set-
        // bound dependent would stay RerunRequired. Here the planner over a doc 'plan' classifies it ReviewedNoImpact.
        string dir = NewTempDir();
        try
        {
            CycleCheckReport report = new(1, "tasks", false,
            [
                new StagePrereqResult("plan", "stale", false, "a prerequisite artifact changed since stamp",
                    StaleReason.PrereqArtifactChanged),
            ]);

            CycleRecoveryPlan plan = CycleRecoveryPlanner.Project(report, TwoStageModel(dir));

            StageRecoveryStep step = Assert.Single(plan.Steps);
            Assert.Equal(RestampSafety.ReviewedNoImpact, step.Safety);
            Assert.Contains("review-rebind", step.NextCommand);
            Assert.False(plan.Recoverable); // an agent decision is required; --apply-safe cannot recover it.
        }
        finally { Directory.Delete(dir, true); }
    }
}
