using Hx.Cycle.Core;
using Hx.Tooling.Contracts;
using Xunit;
using static Hx.Cycle.Tests.CycleTestFixtures;

namespace Hx.Cycle.Tests;

/// <summary>T003: each stale arm of <see cref="FreshnessEvaluator"/> returns the correct machine-readable
/// <see cref="StaleReason"/> (the category the restamp-safety classifier consumes), while the prose reason is preserved.</summary>
public sealed class FreshnessReasonTests
{
    private static CycleStageProof Proof(
        string stage, string changeSetId, IReadOnlyList<string> artifactHashes, IReadOnlyList<string>? prereqArtifactHashes) =>
        new(stage, CycleStageOutcome.Stamped, changeSetId, artifactHashes, "HEAD", null, prereqArtifactHashes);

    [Fact]
    public void ChangeSetDiffers_when_identity_moves_and_required()
    {
        string dir = NewTempDir();
        try
        {
            var evaluator = new FreshnessEvaluator(dir, TwoStageModel(dir));
            CycleStageProof proof = Proof("plan", "STAMPED-ID", [], null);

            StageFreshnessResult result = evaluator.Evaluate(proof, Feature, "CURRENT-ID", requireChangeSetIdentity: true);

            Assert.Equal(StageFreshness.Stale, result.Freshness);
            Assert.Equal(StaleReason.ChangeSetDiffers, result.StaleReason);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void OwnArtifactChanged_when_the_present_artifact_hash_differs()
    {
        string dir = NewTempDir();
        try
        {
            StageModel model = TwoStageModel(dir);
            Write(dir, "docs/plans/001-test-plan.md", "current plan content");
            var evaluator = new FreshnessEvaluator(dir, model);
            CycleStageProof proof = Proof("plan", "ID", ["a-stale-hash"], null);

            StageFreshnessResult result = evaluator.Evaluate(proof, Feature, "ID", requireChangeSetIdentity: false);

            Assert.Equal(StaleReason.OwnArtifactChanged, result.StaleReason);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void NotProduced_when_the_produced_artifact_is_absent()
    {
        string dir = NewTempDir();
        try
        {
            var evaluator = new FreshnessEvaluator(dir, TwoStageModel(dir));
            CycleStageProof proof = Proof("plan", "ID", ["any-hash"], null); // plan file is never written

            StageFreshnessResult result = evaluator.Evaluate(proof, Feature, "ID", requireChangeSetIdentity: false);

            Assert.Equal(StaleReason.NotProduced, result.StaleReason);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void MissingArtifactBinding_when_the_produced_artifact_is_present_but_unbound()
    {
        string dir = NewTempDir();
        try
        {
            StageModel model = TwoStageModel(dir);
            Write(dir, "docs/plans/001-test-plan.md", "plan content"); // present
            var evaluator = new FreshnessEvaluator(dir, model);
            CycleStageProof proof = Proof("plan", "ID", [], null); // empty ArtifactHashes — never bound

            StageFreshnessResult result = evaluator.Evaluate(proof, Feature, "ID", requireChangeSetIdentity: false);

            Assert.Equal(StaleReason.MissingArtifactBinding, result.StaleReason);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void MissingBinding_when_prereq_artifacts_present_but_binding_is_null()
    {
        string dir = NewTempDir();
        try
        {
            StageModel model = TwoStageModel(dir);
            Write(dir, "docs/specs/001-test.md", "spec content");                  // prereq present
            string planHash = Write(dir, "docs/plans/001-test-plan.md", "plan");   // own artifact matches
            var evaluator = new FreshnessEvaluator(dir, model);
            CycleStageProof proof = Proof("plan", "ID", [planHash], prereqArtifactHashes: null); // null binding

            StageFreshnessResult result = evaluator.Evaluate(proof, Feature, "ID", requireChangeSetIdentity: false);

            Assert.Equal(StaleReason.MissingBinding, result.StaleReason);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void PrereqArtifactChanged_when_an_upstream_artifact_hash_differs()
    {
        string dir = NewTempDir();
        try
        {
            StageModel model = TwoStageModel(dir);
            Write(dir, "docs/specs/001-test.md", "spec content");
            string planHash = Write(dir, "docs/plans/001-test-plan.md", "plan");
            var evaluator = new FreshnessEvaluator(dir, model);
            CycleStageProof proof = Proof("plan", "ID", [planHash], ["docs/specs/001-test.md:STALE-HASH"]);

            StageFreshnessResult result = evaluator.Evaluate(proof, Feature, "ID", requireChangeSetIdentity: false);

            Assert.Equal(StaleReason.PrereqArtifactChanged, result.StaleReason);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void PrereqRebindable_when_own_fresh_and_only_prereq_set_moved_with_identical_shared_content()
    {
        // 027 FR-001: own artifact unchanged, change-set identity NOT required, and the bound prereq set differs
        // from the current closure ONLY by a dropped edge — every SHARED path's content hash is byte-identical.
        // A pure edge/reorder move that the chokepoint may auto-rebind once upstreams are Fresh.
        string dir = NewTempDir();
        try
        {
            StageModel model = TwoStageModel(dir);
            string specHash = Write(dir, "docs/specs/001-test.md", "spec content");
            string planHash = Write(dir, "docs/plans/001-test-plan.md", "plan");
            var evaluator = new FreshnessEvaluator(dir, model);
            CycleStageProof proof = Proof("plan", "ID", [planHash],
                [$"docs/specs/001-test.md:{specHash}", "docs/specs/001-dropped.md:OLD-EDGE-HASH"]);

            StageFreshnessResult result = evaluator.Evaluate(proof, Feature, "ID", requireChangeSetIdentity: false);

            Assert.Equal(StageFreshness.Stale, result.Freshness);
            Assert.Equal(StaleReason.PrereqRebindable, result.StaleReason);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void PrereqArtifactChanged_not_rebindable_when_a_shared_prereq_content_value_differs()
    {
        // The SAFE invariant's negative: a SHARED path whose content hash differs is a real content VALUE change,
        // never a pure edge move — it stays PrereqArtifactChanged (RerunRequired), even with identity unrequired.
        string dir = NewTempDir();
        try
        {
            StageModel model = TwoStageModel(dir);
            Write(dir, "docs/specs/001-test.md", "spec content");
            string planHash = Write(dir, "docs/plans/001-test-plan.md", "plan");
            var evaluator = new FreshnessEvaluator(dir, model);
            // Same shared path, DIFFERENT bound hash for it (a content value change) + a dropped edge.
            CycleStageProof proof = Proof("plan", "ID", [planHash],
                ["docs/specs/001-test.md:CHANGED-VALUE-HASH", "docs/specs/001-dropped.md:OLD-EDGE-HASH"]);

            StageFreshnessResult result = evaluator.Evaluate(proof, Feature, "ID", requireChangeSetIdentity: false);

            Assert.Equal(StaleReason.PrereqArtifactChanged, result.StaleReason);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Change_set_bound_stage_never_rebinds_even_when_only_prereq_set_moved()
    {
        // The SAFE invariant locks the 021/026 fix: a change-set-bound stage with a moved identity hits
        // ChangeSetDiffers first and can NEVER be reclassified as the auto-rebindable PrereqRebindable.
        string dir = NewTempDir();
        try
        {
            StageModel model = TwoStageModel(dir);
            string specHash = Write(dir, "docs/specs/001-test.md", "spec content");
            string planHash = Write(dir, "docs/plans/001-test-plan.md", "plan");
            var evaluator = new FreshnessEvaluator(dir, model);
            CycleStageProof proof = Proof("plan", "STAMPED-ID", [planHash],
                [$"docs/specs/001-test.md:{specHash}", "docs/specs/001-dropped.md:OLD-EDGE-HASH"]);

            StageFreshnessResult result = evaluator.Evaluate(proof, Feature, "MOVED-ID", requireChangeSetIdentity: true);

            Assert.Equal(StaleReason.ChangeSetDiffers, result.StaleReason);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Fresh_carries_no_stale_reason()
    {
        string dir = NewTempDir();
        try
        {
            StageModel model = TwoStageModel(dir);
            string specHash = Write(dir, "docs/specs/001-test.md", "spec content");
            string planHash = Write(dir, "docs/plans/001-test-plan.md", "plan");
            var evaluator = new FreshnessEvaluator(dir, model);
            CycleStageProof proof = Proof("plan", "ID", [planHash], [$"docs/specs/001-test.md:{specHash}"]);

            StageFreshnessResult result = evaluator.Evaluate(proof, Feature, "ID", requireChangeSetIdentity: false);

            Assert.Equal(StageFreshness.Fresh, result.Freshness);
            Assert.Null(result.StaleReason);
        }
        finally { Directory.Delete(dir, true); }
    }
}
