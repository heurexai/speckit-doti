using Hx.Cycle.Core;
using Xunit;

namespace Hx.Cycle.Tests;

/// <summary>
/// 028 T002 / SC-002: the pure, git-free <see cref="ReviewRebindEligibility.IsAttestable"/> matrix. A stage is
/// agent-attestable ONLY when it is stale via <see cref="StaleReason.PrereqArtifactChanged"/> AND non-review-kind AND
/// not change-set-bound. Every other stale reason, a review-kind stage, and a change-set-bound stage are all false.
/// </summary>
public sealed class ReviewRebindEligibilityTests
{
    private static CycleStage Stage(string id, string kind, params string[] prereqs) =>
        new(id, $"/{id}", kind, kind == "diff" ? null : $"docs/{id}.md", prereqs, []);

    private static StageFreshnessResult Stale(StaleReason reason) =>
        new("plan", StageFreshness.Stale, reason.ToString(), reason);

    [Theory]
    [InlineData(StaleReason.OwnArtifactChanged)]
    [InlineData(StaleReason.ChangeSetDiffers)]
    [InlineData(StaleReason.NotProduced)]
    [InlineData(StaleReason.MissingArtifactBinding)]
    [InlineData(StaleReason.MissingBinding)]
    [InlineData(StaleReason.PrereqRebindable)]
    public void Non_prereq_content_changed_reasons_are_never_attestable(StaleReason reason) =>
        Assert.False(ReviewRebindEligibility.IsAttestable(
            Stale(reason), Stage("plan", "doc"), requiresChangeSetIdentity: false));

    [Fact]
    public void PrereqArtifactChanged_on_a_doc_non_changeset_stage_is_attestable() =>
        Assert.True(ReviewRebindEligibility.IsAttestable(
            Stale(StaleReason.PrereqArtifactChanged), Stage("plan", "doc"), requiresChangeSetIdentity: false));

    [Fact]
    public void PrereqArtifactChanged_on_a_review_kind_stage_is_not_attestable() =>
        Assert.False(ReviewRebindEligibility.IsAttestable(
            Stale(StaleReason.PrereqArtifactChanged), Stage("arch-review", "review"), requiresChangeSetIdentity: false));

    [Fact]
    public void PrereqArtifactChanged_on_a_change_set_bound_stage_is_not_attestable() =>
        Assert.False(ReviewRebindEligibility.IsAttestable(
            Stale(StaleReason.PrereqArtifactChanged), Stage("plan", "doc"), requiresChangeSetIdentity: true));

    [Fact]
    public void A_fresh_result_is_never_attestable() =>
        Assert.False(ReviewRebindEligibility.IsAttestable(
            new StageFreshnessResult("plan", StageFreshness.Fresh, null), Stage("plan", "doc"),
            requiresChangeSetIdentity: false));

    [Fact]
    public void RequiresChangeSetIdentity_is_true_for_a_diff_stage_or_a_diff_dependent()
    {
        StageModel model = ModelWithDiffStage();
        // implement is itself diff-kind; drift-review depends on implement (a transitive diff prereq); plan does not.
        Assert.True(ReviewRebindEligibility.RequiresChangeSetIdentity(model.Find("implement"), model));
        Assert.True(ReviewRebindEligibility.RequiresChangeSetIdentity(model.Find("drift-review"), model));
        Assert.False(ReviewRebindEligibility.RequiresChangeSetIdentity(model.Find("plan"), model));
    }

    private static StageModel ModelWithDiffStage()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-elig-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string yml = Path.Combine(dir, "workflow.yml");
        File.WriteAllText(yml,
            "schemaVersion: 2\nname: t\nstages:\n" +
            "  - id: plan\n    command: c\n    kind: doc\n    produces: docs/plans/{feature}-plan.md\n    prereqs: []\n" +
            "  - id: implement\n    command: c\n    kind: diff\n    prereqs: [plan]\n" +
            "  - id: drift-review\n    command: c\n    kind: review\n    produces: docs/reviews/{feature}-drift.md\n    prereqs: [implement]\n");
        return StageModel.Load(yml);
    }
}
