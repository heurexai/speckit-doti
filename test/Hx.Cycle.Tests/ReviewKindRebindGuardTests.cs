using Hx.Cycle.Core;
using Xunit;
using static Hx.Cycle.Tests.CycleTestFixtures;

namespace Hx.Cycle.Tests;

/// <summary>
/// T013 (FR-003, SC-003) — the review-kind carve-out: a review verdict (arch-review/analyze/drift-review) is a
/// judgment over its inputs, so a moved/changed input invalidates it. Even when a review-kind dependent's stale
/// reason is the otherwise-auto-rebindable <see cref="StaleReason.PrereqRebindable"/> AND every producing upstream
/// is Fresh in the same report (the exact conditions that promote a non-review dependent to an auto-rebind), the
/// <see cref="CycleRecoveryPlanner"/> MUST downgrade it to <see cref="RestampSafety.RerunRequired"/> — never an
/// auto-rebind. Asserted as a pure projection over a hand-built <see cref="CycleCheckReport"/>, so it can never
/// disagree with the chokepoint.
/// </summary>
public sealed class ReviewKindRebindGuardTests
{
    private static CycleCheckReport Report(string target, params StagePrereqResult[] prereqs) =>
        new(1, target, prereqs.All(p => p.Ok), prereqs);

    // specify (doc) -> plan (doc) -> <review-kind dependent>. The producing upstreams (specify, plan) produce
    // artifacts; the review-kind stage produces nothing. The kind under test is parameterized.
    private static StageModel ReviewModel(string dir, string reviewStageId, string reviewKind)
    {
        string yml = Path.Combine(dir, $"workflow-{reviewStageId}.yml");
        File.WriteAllText(yml,
            "schemaVersion: 2\nname: t\nstages:\n" +
            "  - id: specify\n    command: 01-doti-specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n" +
            "  - id: plan\n    command: 03-doti-plan\n    kind: doc\n    produces: docs/plans/{feature}-plan.md\n    prereqs: [specify]\n" +
            $"  - id: {reviewStageId}\n    command: cmd-{reviewStageId}\n    kind: {reviewKind}\n    prereqs: [plan]\n");
        return StageModel.Load(yml);
    }

    [Theory]
    [InlineData("arch-review", "review")]
    [InlineData("analyze", "review")]
    [InlineData("drift-review", "review")]
    public void Review_kind_dependent_is_never_auto_rebound_even_with_all_upstreams_fresh(
        string reviewStageId, string reviewKind)
    {
        string dir = NewTempDir();
        try
        {
            // Every producing upstream is Fresh, and the review-kind dependent is PrereqRebindable — the precise
            // shape that would otherwise classify as ReBindContentEqual (auto-rebindable). The review carve-out
            // must override it to RerunRequired.
            CycleCheckReport report = Report(reviewStageId,
                new StagePrereqResult("specify", "fresh", true, null),
                new StagePrereqResult("plan", "fresh", true, null),
                new StagePrereqResult(reviewStageId, "stale", false, "prereq edge moved",
                    StaleReason.PrereqRebindable));

            CycleRecoveryPlan plan = CycleRecoveryPlanner.Project(
                report, ReviewModel(dir, reviewStageId, reviewKind));

            StageRecoveryStep step = Assert.Single(plan.Steps, s => s.Stage == reviewStageId);
            Assert.Equal(RestampSafety.RerunRequired, step.Safety); // never ReBindContentEqual
            Assert.NotEqual(RestampSafety.ReBindContentEqual, step.Safety);
            Assert.False(plan.Recoverable); // a review re-run is required; --apply-safe cannot recover it
            Assert.Equal($"/cmd-{reviewStageId}", step.NextCommand); // routes to re-running the review stage
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Review_kind_dependent_stays_RerunRequired_when_an_upstream_artifact_actually_changed()
    {
        // A genuine upstream content change (PrereqArtifactChanged) already maps to RerunRequired in the pure
        // classifier; the review carve-out is belt-and-suspenders. Asserts a review dependent never rebinds on
        // ANY upstream change, content-value or edge-only.
        string dir = NewTempDir();
        try
        {
            CycleCheckReport report = Report("arch-review",
                new StagePrereqResult("specify", "fresh", true, null),
                new StagePrereqResult("plan", "fresh", true, null),
                new StagePrereqResult("arch-review", "stale", false, "an upstream artifact changed",
                    StaleReason.PrereqArtifactChanged));

            CycleRecoveryPlan plan = CycleRecoveryPlanner.Project(report, ReviewModel(dir, "arch-review", "review"));

            StageRecoveryStep step = Assert.Single(plan.Steps, s => s.Stage == "arch-review");
            Assert.Equal(RestampSafety.RerunRequired, step.Safety);
            Assert.False(plan.Recoverable);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Control_a_non_review_dependent_with_the_same_conditions_does_auto_rebind()
    {
        // Control: identical report shape but a doc-kind dependent ("tasks") — proves the downgrade above is
        // specifically the review-kind gate, not an accidental RerunRequired for every PrereqRebindable step.
        string dir = NewTempDir();
        try
        {
            string yml = Path.Combine(dir, "workflow-control.yml");
            File.WriteAllText(yml,
                "schemaVersion: 2\nname: t\nstages:\n" +
                "  - id: specify\n    command: 01-doti-specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n" +
                "  - id: plan\n    command: 03-doti-plan\n    kind: doc\n    produces: docs/plans/{feature}-plan.md\n    prereqs: [specify]\n" +
                "  - id: tasks\n    command: 05-doti-tasks\n    kind: doc\n    produces: docs/tasks/{feature}-tasks.md\n    prereqs: [plan]\n");
            StageModel model = StageModel.Load(yml);

            CycleCheckReport report = Report("tasks",
                new StagePrereqResult("specify", "fresh", true, null),
                new StagePrereqResult("plan", "fresh", true, null),
                new StagePrereqResult("tasks", "stale", false, "prereq edge moved",
                    StaleReason.PrereqRebindable));

            CycleRecoveryPlan plan = CycleRecoveryPlanner.Project(report, model);

            StageRecoveryStep step = Assert.Single(plan.Steps, s => s.Stage == "tasks");
            Assert.Equal(RestampSafety.ReBindContentEqual, step.Safety); // doc dependent IS auto-rebindable
            Assert.Equal("doti cycle refresh --target tasks --apply-safe", step.NextCommand);
            Assert.True(plan.Recoverable);
        }
        finally { Directory.Delete(dir, true); }
    }
}
