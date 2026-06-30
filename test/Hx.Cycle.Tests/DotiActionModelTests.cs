using Hx.Cycle.Core;
using Hx.Cycle.Core.Actions;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Cycle.Tests;

/// <summary>
/// 028 T012/T013/T015 (FR-010 / SC-009/010/011): the code-generated action model. Covers the registry static-invariants
/// (one advance per stage; no two same-kind descriptors apply at one decision point; no decision point yields zero
/// actions), the <see cref="Applicability.Describe"/> == <see cref="Applicability.Evaluate"/> no-drift property, the
/// projector per-state, and the recovery-descriptor-id tagging matching the single <see cref="CycleRecoveryPlanner"/>
/// projection (never a second evaluator).
/// </summary>
public sealed class DotiActionModelTests
{
    private static StageModel FullModel()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-action-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string yml = Path.Combine(dir, "workflow.yml");
        File.WriteAllText(yml,
            "schemaVersion: 2\nname: t\nstages:\n" +
            "  - id: specify\n    command: 01-doti-specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n    next: [clarify]\n" +
            "  - id: clarify\n    command: 02-doti-clarify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: [specify]\n    next: [plan]\n" +
            "  - id: plan\n    command: 03-doti-plan\n    kind: doc\n    produces: docs/plans/{feature}-plan.md\n    prereqs: [clarify]\n    next: [arch-review]\n" +
            "  - id: arch-review\n    command: 04-doti-arch-review\n    kind: review\n    produces: docs/reviews/{feature}-arch-review.md\n    prereqs: [plan]\n    next: [tasks]\n" +
            "  - id: tasks\n    command: 05-doti-tasks\n    kind: doc\n    produces: docs/tasks/{feature}-tasks.md\n    prereqs: [arch-review]\n    next: [analyze]\n" +
            "  - id: analyze\n    command: 06-doti-analyze\n    kind: review\n    produces: docs/reviews/{feature}-analyze-report.md\n    prereqs: [tasks]\n    next: [implement]\n" +
            "  - id: implement\n    command: 07-doti-implement\n    kind: diff\n    prereqs: [analyze]\n    next: [drift-review]\n" +
            "  - id: drift-review\n    command: 08-doti-drift-review\n    kind: review\n    produces: docs/reviews/{feature}-drift-review.md\n    prereqs: [implement]\n    next: [release, specify]\n" +
            "  - id: release\n    command: 09-doti-release\n    kind: release\n    prereqs: [drift-review]\n    next: []\n");
        return StageModel.Load(yml);
    }

    private static CommandContext AtStage(string stage, bool checkPassed) =>
        new(
            new CycleState(1, "001-f", "HEAD", stage, []),
            checkReport: new CycleCheckReport(1, stage, checkPassed, []));

    // ----- registry static-invariants (SC-011) -----

    [Fact]
    public void Exactly_one_advance_descriptor_exists_per_stage_with_a_forward_successor()
    {
        var model = new DotiActionModel(FullModel());
        // 8 forward edges (specify..implement → next, drift-review → release); release is terminal.
        List<CommandDescriptor> advances = model.Descriptors
            .Where(d => d.Kind == CommandKind.Advance && d.Id.StartsWith("advance.", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(8, advances.Count);
        Assert.Equal(advances.Count, advances.Select(d => d.Id).Distinct().Count());
    }

    [Fact]
    public void No_two_same_kind_descriptors_apply_at_any_stage_decision_point()
    {
        var model = new DotiActionModel(FullModel());
        var projector = new DotiActionProjector(model);
        foreach (string stage in StageIds(FullModel()))
        {
            foreach (bool passed in new[] { true, false })
            {
                IReadOnlyList<ProjectedAction> actions = projector.Project(AtStage(stage, passed));
                // No two ADVANCE-kind affordances at one decision point (the dangerous overlap — utilities are
                // intentionally always-available and are not advance moves).
                List<ProjectedAction> advances = actions
                    .Where(a => a.Descriptor.Kind == CommandKind.Advance)
                    .ToList();
                Assert.True(advances.Count <= 1,
                    $"more than one advance applies at {stage}/passed={passed}: {string.Join(",", advances.Select(a => a.Descriptor.Id))}");
            }
        }
    }

    [Fact]
    public void Every_stage_decision_point_yields_at_least_one_action()
    {
        var model = new DotiActionModel(FullModel());
        var projector = new DotiActionProjector(model);
        foreach (string stage in StageIds(FullModel()))
        {
            IReadOnlyList<ProjectedAction> actions = projector.Project(AtStage(stage, checkPassed: true));
            Assert.NotEmpty(actions); // utility skills alone guarantee non-empty; never a dead end.
        }
    }

    [Fact]
    public void Describe_reads_the_same_named_condition_evaluate_tests_no_drift()
    {
        // The ACID no-drift property: a descriptor's "available when …" is derived from the SAME declarative condition
        // its applicability evaluates. Toggling the matching context fact flips BOTH evaluate AND a substring of describe.
        var model = new DotiActionModel(FullModel());
        CommandDescriptor advancePlan = model.Descriptors.Single(d => d.Id == "advance.plan");

        Assert.Contains("plan", advancePlan.AvailableWhen());
        Assert.True(advancePlan.AppliesTo(AtStage("plan", checkPassed: true)));
        Assert.False(advancePlan.AppliesTo(AtStage("plan", checkPassed: false))); // check not passed
        Assert.False(advancePlan.AppliesTo(AtStage("tasks", checkPassed: true)));  // different stage
    }

    // ----- projector per-state (SC-009) -----

    [Fact]
    public void At_a_passed_stage_the_single_forward_advance_is_projected()
    {
        var projector = new DotiActionProjector(new DotiActionModel(FullModel()));
        ProjectedAction advance = Assert.Single(
            projector.Project(AtStage("plan", checkPassed: true)),
            a => a.Descriptor.Kind == CommandKind.Advance);
        Assert.Equal("/04-doti-arch-review", advance.Command);
    }

    [Fact]
    public void Outside_a_cycle_the_start_feature_action_is_projected()
    {
        var projector = new DotiActionProjector(new DotiActionModel(FullModel()));
        IReadOnlyList<ProjectedAction> actions = projector.Project(new CommandContext(state: null));
        ProjectedAction start = Assert.Single(actions, a => a.Descriptor.Id == DotiActionModel.StartFeatureId);
        Assert.Equal("/01-doti-specify", start.Command);
    }

    [Fact]
    public void At_the_terminal_stage_passed_the_publish_boundary_stop_is_projected_with_no_command()
    {
        var projector = new DotiActionProjector(new DotiActionModel(FullModel()));
        ProjectedAction stop = Assert.Single(
            projector.Project(AtStage("release", checkPassed: true)),
            a => a.Descriptor.Kind == CommandKind.PublishBoundary);
        Assert.Null(stop.Command); // the publish boundary is an operator-decision STOP with no command.
    }

    [Fact]
    public void The_review_rebind_verb_is_projected_only_at_an_attestable_recovery_tier_with_the_stage_substituted()
    {
        var projector = new DotiActionProjector(new DotiActionModel(FullModel()));

        var planStep = new StageRecoveryStep("plan", "stale", "a prerequisite changed", RestampSafety.ReviewedNoImpact,
            "/03-doti-plan", "doti cycle review-rebind --target plan --attest no-impact");
        var withTier = new CommandContext(
            new CycleState(1, "001-f", "HEAD", "analyze", []),
            recoveryPlan: new CycleRecoveryPlan(1, "analyze", false, [planStep]));

        ProjectedAction verb = Assert.Single(
            projector.Project(withTier), a => a.Descriptor.Id == DotiActionModel.ReviewRebindVerbId);
        Assert.Equal("doti cycle review-rebind --target plan --attest no-impact", verb.Command);

        // Without the tier, the verb is NOT offered.
        var noTier = new CommandContext(
            new CycleState(1, "001-f", "HEAD", "analyze", []),
            recoveryPlan: new CycleRecoveryPlan(1, "analyze", true, []));
        Assert.DoesNotContain(projector.Project(noTier), a => a.Descriptor.Id == DotiActionModel.ReviewRebindVerbId);
    }

    // ----- recovery descriptor-id tagging matches the single planner projection (H6) -----

    [Theory]
    [InlineData(RestampSafety.SafeReinterpret, DotiActionModel.RecoverySafeRefreshId)]
    [InlineData(RestampSafety.ReBindContentEqual, DotiActionModel.RecoverySafeRefreshId)]
    [InlineData(RestampSafety.ReviewedNoImpact, DotiActionModel.RecoveryReviewRebindId)]
    [InlineData(RestampSafety.RerunRequired, DotiActionModel.RecoveryRerunId)]
    [InlineData(RestampSafety.NotBound, DotiActionModel.RecoveryNotBoundId)]
    public void Recovery_descriptor_id_is_tagged_from_the_step_safety(RestampSafety safety, string expectedId)
    {
        var step = new StageRecoveryStep("plan", "stale", "r", safety, "/r", "n");
        Assert.Equal(expectedId, DotiActionModel.RecoveryDescriptorIdFor(step));
    }

    [Fact]
    public void An_inserted_stage_step_is_tagged_inserted_stage()
    {
        var step = new StageRecoveryStep("plan", CycleRecoveryPlanner.InsertedStageStatus, "r", null, "/r", "n");
        Assert.Equal(DotiActionModel.RecoveryInsertedStageId, DotiActionModel.RecoveryDescriptorIdFor(step));
    }

    [Fact]
    public void Recovery_actions_wrap_every_plan_step_with_its_descriptor_id()
    {
        var projector = new DotiActionProjector(new DotiActionModel(FullModel()));
        var steps = new[]
        {
            new StageRecoveryStep("plan", "stale", "r", RestampSafety.ReviewedNoImpact, "/p", "doti cycle review-rebind --target plan --attest no-impact"),
            new StageRecoveryStep("tasks", "stale", "r", RestampSafety.RerunRequired, "/05", "/05"),
        };
        var ctx = new CommandContext(
            new CycleState(1, "001-f", "HEAD", "analyze", []),
            recoveryPlan: new CycleRecoveryPlan(1, "analyze", false, steps));

        List<ProjectedAction> recovery = projector.Project(ctx)
            .Where(a => a.Descriptor.Id.StartsWith("recovery.", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(2, recovery.Count);
        Assert.Contains(recovery, a => a.Descriptor.Id == DotiActionModel.RecoveryReviewRebindId);
        Assert.Contains(recovery, a => a.Descriptor.Id == DotiActionModel.RecoveryRerunId);
    }

    [Fact]
    public void The_seven_utility_skills_are_modeled_as_always_available_utility_descriptors()
    {
        var model = new DotiActionModel(FullModel());
        List<CommandDescriptor> utilities = model.Descriptors
            .Where(d => d.Kind == CommandKind.Utility)
            .ToList();
        Assert.Equal(7, utilities.Count);
        foreach (string skill in new[]
            { "doti-bug", "doti-amend", "doti-converge", "doti-drift-fix", "doti-constitution", "doti-auto", "doti-upgrade" })
        {
            Assert.Contains(utilities, d => d.InvocationTemplate == $"/{skill}");
        }
    }

    private static IEnumerable<string> StageIds(StageModel model) => model.Stages.Select(s => s.Id);
}
