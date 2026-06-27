using Hx.Cycle.Core;
using Xunit;
using static Hx.Cycle.Tests.CycleTestFixtures;

namespace Hx.Cycle.Tests;

/// <summary>T010: <see cref="CycleRecoveryPlanner.Project"/> is a pure projection of a <see cref="CycleCheckReport"/>
/// — one step per blocking prerequisite, each carrying its restamp-safety and the single recommended next command.</summary>
public sealed class RecoveryPlanTests
{
    private static CycleCheckReport Report(string target, params StagePrereqResult[] prereqs) =>
        new(1, target, prereqs.All(p => p.Ok), prereqs);

    [Fact]
    public void Project_classifies_a_safe_step_and_recommends_a_refresh()
    {
        string dir = NewTempDir();
        try
        {
            CycleCheckReport report = Report("tasks",
                new StagePrereqResult("plan", "stale", false, "unbound", StaleReason.MissingArtifactBinding));

            CycleRecoveryPlan plan = CycleRecoveryPlanner.Project(report, TwoStageModel(dir));

            StageRecoveryStep step = Assert.Single(plan.Steps);
            Assert.Equal(RestampSafety.SafeReinterpret, step.Safety);
            Assert.Equal("doti cycle refresh --target tasks --apply-safe", step.NextCommand);
            Assert.True(plan.Recoverable);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Project_marks_rerun_required_when_a_real_input_changed()
    {
        string dir = NewTempDir();
        try
        {
            CycleCheckReport report = Report("tasks",
                new StagePrereqResult("plan", "stale", false, "changed", StaleReason.OwnArtifactChanged));

            CycleRecoveryPlan plan = CycleRecoveryPlanner.Project(report, TwoStageModel(dir));

            StageRecoveryStep step = Assert.Single(plan.Steps);
            Assert.Equal(RestampSafety.RerunRequired, step.Safety);
            Assert.Equal("/c", step.NextCommand); // the stage's own command (TwoStageModel uses "c")
            Assert.False(plan.Recoverable); // a re-run is needed; --apply-safe cannot fully recover
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Project_includes_only_blocking_prereqs_and_is_not_recoverable_when_mixed()
    {
        string dir = NewTempDir();
        try
        {
            CycleCheckReport report = Report("tasks",
                new StagePrereqResult("specify", "fresh", true, null),
                new StagePrereqResult("plan", "stale", false, "unbound", StaleReason.MissingArtifactBinding),
                new StagePrereqResult("clarify", "stale", false, "changed", StaleReason.PrereqArtifactChanged));

            CycleRecoveryPlan plan = CycleRecoveryPlanner.Project(report, TwoStageModel(dir));

            Assert.Equal(2, plan.Steps.Count); // the fresh prereq is excluded
            Assert.DoesNotContain(plan.Steps, s => s.Stage == "specify");
            Assert.False(plan.Recoverable); // one safe + one rerun-required ⇒ not fully recoverable
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Project_handles_a_synthetic_non_stage_prereq()
    {
        string dir = NewTempDir();
        try
        {
            CycleCheckReport report = Report("release",
                new StagePrereqResult("release-train", "invalid", false, "a feature is incomplete"));

            CycleRecoveryPlan plan = CycleRecoveryPlanner.Project(report, TwoStageModel(dir));

            StageRecoveryStep step = Assert.Single(plan.Steps);
            Assert.Null(step.Safety); // not a stale stage with a known restamp-safety
            Assert.Contains("release-train", step.NextCommand);
        }
        finally { Directory.Delete(dir, true); }
    }
}
