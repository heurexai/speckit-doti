using Hx.Cycle.Core;
using Hx.Tooling.Contracts;
using Xunit;
using static Hx.Cycle.Tests.CycleTestFixtures;

namespace Hx.Cycle.Tests;

/// <summary>
/// T013 (FR-002, SC-003, W1 scenario 4) — the 021/026 regression lock: a change-set-bound stage (e.g.
/// <c>implement</c>) stamped, then a code-only working-tree edit moves the change-set identity. The
/// <c>requireChangeSetIdentity</c> arm of <see cref="FreshnessEvaluator"/> fires FIRST, returning
/// <see cref="StaleReason.ChangeSetDiffers"/>, so the new 027 <see cref="StaleReason.PrereqRebindable"/> tier can
/// never reclassify it — even when the prerequisite binding ALSO moved (an edge/reorder with byte-identical
/// shared content, the shape that would otherwise be auto-rebindable). The classifier keeps
/// <see cref="StaleReason.ChangeSetDiffers"/> mapped to <see cref="RestampSafety.RerunRequired"/>.
/// </summary>
public sealed class ImplementChangeSetLockTests
{
    private static CycleStageProof Proof(
        string stage, string changeSetId, IReadOnlyList<string> artifactHashes,
        IReadOnlyList<string>? prereqArtifactHashes) =>
        new(stage, CycleStageOutcome.Stamped, changeSetId, artifactHashes, "HEAD", null, prereqArtifactHashes);

    // specify (doc) -> implement (diff, change-set bound, no produced artifact). implement re-derives via the
    // change-set identity, not a file binding — a code edit moves that identity.
    private static StageModel ImplementModel(string dir)
    {
        string yml = Path.Combine(dir, "workflow-implement.yml");
        File.WriteAllText(yml,
            "schemaVersion: 2\nname: t\nstages:\n" +
            "  - id: specify\n    command: 01-doti-specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n" +
            "  - id: implement\n    command: 07-doti-implement\n    kind: diff\n    prereqs: [specify]\n");
        return StageModel.Load(yml);
    }

    [Fact]
    public void Code_only_edit_during_implement_is_ChangeSetDiffers_RerunRequired_never_reclassified()
    {
        string dir = NewTempDir();
        try
        {
            Write(dir, "docs/specs/001-test.md", "spec content");
            var evaluator = new FreshnessEvaluator(dir, ImplementModel(dir));
            // implement stamped at one identity; the working tree has since moved (a code-only edit).
            CycleStageProof proof = Proof("implement", "STAMPED-ID", [], null);

            StageFreshnessResult result = evaluator.Evaluate(
                proof, Feature, "MOVED-ID", requireChangeSetIdentity: true);

            Assert.Equal(StageFreshness.Stale, result.Freshness);
            Assert.Equal(StaleReason.ChangeSetDiffers, result.StaleReason);
            // The new tier must NOT touch a change-set-bound stale — it stays a real re-run.
            Assert.NotEqual(StaleReason.PrereqRebindable, result.StaleReason);
            Assert.Equal(RestampSafety.RerunRequired, RestampSafetyClassifier.Classify(result.StaleReason!.Value));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void ChangeSet_arm_fires_before_the_prereq_arm_so_an_edge_move_is_still_ChangeSetDiffers()
    {
        // The adversarial case: the change-set identity moved (code edit) AND the prerequisite binding moved to a
        // byte-identical shared artifact (an edge/reorder). Were the prereq arm reached, it would emit the
        // auto-rebindable PrereqRebindable. The change-set arm MUST short-circuit first → ChangeSetDiffers,
        // so a code-during-implement edit is never rubber-stamped by the 027 tier.
        string dir = NewTempDir();
        try
        {
            string specHash = Write(dir, "docs/specs/001-test.md", "spec content");
            var evaluator = new FreshnessEvaluator(dir, ImplementModel(dir));
            // Bind a prerequisite hash to the SAME current spec content (so any prereq divergence would be
            // edge-only / rebindable), yet pass a moved identity under requireChangeSetIdentity:true.
            CycleStageProof proof = Proof("implement", "STAMPED-ID", [], [$"docs/specs/001-test.md:{specHash}"]);

            StageFreshnessResult result = evaluator.Evaluate(
                proof, Feature, "MOVED-ID", requireChangeSetIdentity: true);

            Assert.Equal(StaleReason.ChangeSetDiffers, result.StaleReason);
            Assert.NotEqual(StaleReason.PrereqRebindable, result.StaleReason);
            Assert.Equal(RestampSafety.RerunRequired, RestampSafetyClassifier.Classify(result.StaleReason!.Value));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void ChangeSet_bound_stage_never_emits_PrereqRebindable_even_when_identity_holds()
    {
        // Defense in depth: PrereqRebindable is gated on !requireChangeSetIdentity in the evaluator. With the
        // identity UNCHANGED (the change-set arm passes) but the stage still evaluated as change-set-bound, an
        // edge-only prereq move must NOT become rebindable — it stays PrereqArtifactChanged (RerunRequired).
        // (implement has no prereq-artifact binding in practice; this asserts the requireChangeSetIdentity guard
        // on the rebindable branch directly, using a doc stage shape.)
        string dir = NewTempDir();
        try
        {
            StageModel model = TwoStageModel(dir); // specify -> plan
            string specHash = Write(dir, "docs/specs/001-test.md", "spec content");
            string planHash = Write(dir, "docs/plans/001-test-plan.md", "plan");
            var evaluator = new FreshnessEvaluator(dir, model);
            // Own artifact matches; the prereq binding is byte-identical content under a DIFFERENT path label
            // (an edge/reorder shape) — but evaluated with requireChangeSetIdentity:true and a matching identity.
            CycleStageProof proof = Proof("plan", "ID", [planHash], ["docs/specs/legacy-path.md:" + specHash]);

            StageFreshnessResult result = evaluator.Evaluate(
                proof, Feature, "ID", requireChangeSetIdentity: true);

            Assert.Equal(StaleReason.PrereqArtifactChanged, result.StaleReason); // NOT PrereqRebindable
            Assert.Equal(RestampSafety.RerunRequired, RestampSafetyClassifier.Classify(result.StaleReason!.Value));
        }
        finally { Directory.Delete(dir, true); }
    }
}
