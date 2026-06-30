using Hx.Cycle.Core;
using Hx.Tooling.Contracts;
using Xunit;
using static Hx.Cycle.Tests.CycleTestFixtures;

namespace Hx.Cycle.Tests;

/// <summary>T006: the restamp-safety classification (FR-005/006/007). The pure map, plus the L-3 end-to-end cases
/// that wire <see cref="FreshnessEvaluator"/> → <see cref="RestampSafetyClassifier"/> for the produces-binding
/// migration (SafeReinterpret) and the identity-changed variant (RerunRequired).</summary>
public sealed class RestampSafetyTests
{
    [Theory]
    [InlineData(StaleReason.MissingArtifactBinding, RestampSafety.SafeReinterpret)]
    [InlineData(StaleReason.MissingBinding, RestampSafety.SafeReinterpret)]
    [InlineData(StaleReason.NotProduced, RestampSafety.NotBound)]
    [InlineData(StaleReason.PrereqRebindable, RestampSafety.ReBindContentEqual)]
    [InlineData(StaleReason.OwnArtifactChanged, RestampSafety.RerunRequired)]
    [InlineData(StaleReason.PrereqArtifactChanged, RestampSafety.RerunRequired)]
    [InlineData(StaleReason.ChangeSetDiffers, RestampSafety.RerunRequired)]
    public void Classify_maps_each_stale_reason_to_its_safety(StaleReason reason, RestampSafety expected) =>
        Assert.Equal(expected, RestampSafetyClassifier.Classify(reason));

    private static CycleStageProof Proof(string stage, string changeSetId, IReadOnlyList<string> artifactHashes) =>
        new(stage, CycleStageOutcome.Stamped, changeSetId, artifactHashes, "HEAD", null, null);

    [Fact]
    public void Empty_artifact_binding_with_present_content_is_SafeReinterpret()
    {
        // L-3: a stage that gained a produces binding (its report present, content unchanged) is safely
        // re-interpreted — the analyze produces-binding migration must not become a RerunRequired dead end.
        string dir = NewTempDir();
        try
        {
            StageModel model = TwoStageModel(dir);
            Write(dir, "docs/plans/001-test-plan.md", "report content");
            var evaluator = new FreshnessEvaluator(dir, model);

            StageFreshnessResult fresh = evaluator.Evaluate(
                Proof("plan", "ID", []), Feature, "ID", requireChangeSetIdentity: false);

            Assert.Equal(StaleReason.MissingArtifactBinding, fresh.StaleReason);
            Assert.Equal(RestampSafety.SafeReinterpret, RestampSafetyClassifier.Classify(fresh.StaleReason!.Value));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Identity_changed_variant_is_RerunRequired()
    {
        string dir = NewTempDir();
        try
        {
            StageModel model = TwoStageModel(dir);
            Write(dir, "docs/plans/001-test-plan.md", "report content");
            var evaluator = new FreshnessEvaluator(dir, model);

            // requireChangeSetIdentity:true with a moved identity hits the ChangeSetDiffers arm first.
            StageFreshnessResult stale = evaluator.Evaluate(
                Proof("plan", "STAMPED", []), Feature, "MOVED", requireChangeSetIdentity: true);

            Assert.Equal(StaleReason.ChangeSetDiffers, stale.StaleReason);
            Assert.Equal(RestampSafety.RerunRequired, RestampSafetyClassifier.Classify(stale.StaleReason!.Value));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Own_fresh_with_only_prereq_set_moved_and_identical_content_is_ReBindContentEqual()
    {
        // 027 L-3: the stage's own artifact is unchanged and the ONLY divergence is the prerequisite binding
        // SET (a dropped/inserted edge) while every SHARED path is byte-identical — a pure edge/reorder move
        // that maps to the content-equal rebind tier (still planner-gated before any auto-rebind).
        string dir = NewTempDir();
        try
        {
            StageModel model = TwoStageModel(dir);
            string specHash = Write(dir, "docs/specs/001-test.md", "spec content"); // current shared prereq
            string planHash = Write(dir, "docs/plans/001-test-plan.md", "plan");    // own artifact unchanged
            var evaluator = new FreshnessEvaluator(dir, model);

            // Bound prereqs carry the SAME spec hash (shared path byte-identical) PLUS a stale extra edge that
            // is no longer in the current closure — a pure SET move, no content value change.
            var proof = new CycleStageProof("plan", CycleStageOutcome.Stamped, "ID", [planHash], "HEAD", null,
                [$"docs/specs/001-test.md:{specHash}", "docs/specs/001-old-prereq.md:DROPPED-EDGE-HASH"]);

            StageFreshnessResult stale = evaluator.Evaluate(
                proof, Feature, "ID", requireChangeSetIdentity: false);

            Assert.Equal(StaleReason.PrereqRebindable, stale.StaleReason);
            Assert.Equal(RestampSafety.ReBindContentEqual, RestampSafetyClassifier.Classify(stale.StaleReason!.Value));
        }
        finally { Directory.Delete(dir, true); }
    }
}
