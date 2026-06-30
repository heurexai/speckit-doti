using Hx.Cycle.Core;
using Hx.Impact.Core.Planning;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>
/// T027 (FR-033/034/035, SC-017/SC-018): the multi-spec release train is already implemented + verified — this pins
/// it against regression. Starting a new numbered feature from drift-review FINALIZES the prior cycle as
/// completed-unreleased (it is NOT force-released), and a release AGGREGATES every completed-unreleased cycle.
/// </summary>
public sealed partial class CycleEnforcementTests
{
    [Fact]
    public void ReleaseTrain_StartingNewFeatureFinalizesPrior_AndReleaseAggregatesAll()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            PrepareDocsOnlyCycle(dir, service); // 001-f → drift-review

            // FR-033: starting 002 from 001's drift-review finalizes 001 as completed-unreleased, not released.
            CycleState afterStart = service.Stamp("specify", "002-next", null);
            Assert.Equal(["001-f"], afterStart.CompletedUnreleasedCycles!.Select(c => c.Feature).ToArray());
            Assert.Null(afterStart.Completion);
            Assert.Null(afterStart.ReleasedCycles);

            // Finish 002 to release.
            WriteCompletedTaskFile(dir, "002-next");
            Git(dir, "add", "docs/tasks/002-next-tasks.md");
            Git(dir, "commit", "-q", "-m", "seed 002 task file");
            File.WriteAllText(Path.Combine(dir, "docs", "specs", "002-next.md"), "second spec body");
            Git(dir, "add", "docs/specs/002-next.md");
            service.Stamp("specify", "002-next", null);
            service.Stamp("drift-review", null, null);
            WritePassingGateProofForCurrentDiff(dir);
            service.Stamp("release", null, null);

            // FR-034/035: the release train aggregates BOTH completed-unreleased cycles, in order.
            CycleReleaseTrain train = service.GetReleaseTrain();
            Assert.True(train.Valid, string.Join("; ", train.Blockers));
            Assert.Equal(["001-f", "002-next"], train.Features.Select(f => f.Feature).ToArray());

            // SC-018: marking released moves them out of completed-unreleased into released.
            service.MarkReleaseTrainReleased();
            CycleState released = new CycleStateStore(dir).Read()!;
            Assert.Empty(released.CompletedUnreleasedCycles!);
            Assert.Equal(["001-f", "002-next"], released.ReleasedCycles!.Select(c => c.Feature).OrderBy(f => f).ToArray());
        }
        finally { ForceDelete(dir); }
    }

    /// <summary>
    /// 030 (bug-release-bridge), PART A end-to-end: a SINGLE feature cycle driven through the real
    /// specify → implement → drift-review machinery (a passing gate proof minted before the drift-review transition) —
    /// with NO prior completed-unreleased anchor — yields a VALID release train with that feature included. Before the
    /// fix the active feature counted only at <c>currentStage=release</c> (via a drift-review→release transition that
    /// does not exist while parked at drift-review), so the train was empty + invalid and `hx release` / the release
    /// gate refused the cycle. Drives the actual <c>CycleService.Stamp</c> path, not a hand-built state.
    /// </summary>
    [Fact]
    public void ReleaseTrain_SingleFeatureAtDriftReview_WithNoPriorAnchor_IsValid()
    {
        string dir = InitRepo();
        try
        {
            WriteWorkflow(dir,
                "schemaVersion: 2\nstages:\n" +
                "  - id: specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n" +
                "  - id: implement\n    kind: diff\n    prereqs: [specify]\n" +
                "  - id: drift-review\n    kind: review\n    produces: docs/reviews/{feature}-drift-review.md\n    prereqs: [implement]\n    next: [release]\n" +
                "  - id: release\n    kind: release\n    prereqs: [drift-review]\n");

            var service = new CycleService(dir);
            // The completed task file is part of the seed (its own commit), so the specify doc-stage transition stages
            // only its own spec artifact and the release-train task-completion check passes.
            WriteCompletedTaskFile(dir, "001-only");
            Git(dir, "add", "docs/tasks/001-only-tasks.md");
            Git(dir, "commit", "-q", "-m", "seed 001-only task file");

            Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
            File.WriteAllText(Path.Combine(dir, "docs", "specs", "001-only.md"), "spec body");
            Git(dir, "add", "docs/specs/001-only.md");

            service.Stamp("specify", "001-only", null);
            service.Stamp("implement", null, null);     // commits "specify: 001-only"; implement bound to its diff
            WritePassingGateProofForCurrentDiff(dir);
            service.Stamp("drift-review", null, null);   // implement -> drift-review (gate-proof-bound); now PARKED here

            // The fix: the active feature parked at drift-review is a releasable member even with no prior anchor.
            CycleReleaseTrain train = service.GetReleaseTrain();
            Assert.True(train.Valid, string.Join("; ", train.Blockers));
            CycleReleaseTrainFeature member = Assert.Single(train.Features);
            Assert.Equal("001-only", member.Feature);
            Assert.Equal("drift-review", member.CompletedStage);
            Assert.Equal("included", member.InclusionStatus);
        }
        finally { ForceDelete(dir); }
    }

    /// <summary>
    /// 023-bug regression (direct): the release-train re-validates the ACTIVE feature's gate proof by re-planning its
    /// recorded diff. Before the fix it re-planned <c>BaseRef..HEAD</c> against the LIVE symbolic HEAD, so an unrelated
    /// later commit (e.g. a separate bug fix) moved the endpoint and the UNCHANGED feature read "stale or forged:
    /// planner hash does not match the current change set". The fix bounds the re-plan to the feature's OWN release
    /// commit. This drives <see cref="GateProofValidator.ValidateAffectedTestProof"/> directly (the cycle fixtures use a
    /// docs-only workflow with no implement stage, so they never reach the re-validation path).
    /// </summary>
    [Fact]
    public void ActiveFeatureProof_ReValidatesAgainstItsOwnReleaseCommit_NotLiveHead()
    {
        string dir = InitRepo();
        try
        {
            string baseRef = GitHead(dir); // the seed commit

            // The feature's release commit: a code change the planner attributes/escalates.
            Directory.CreateDirectory(Path.Combine(dir, "src"));
            File.WriteAllText(Path.Combine(dir, "src", "Feature.cs"), "namespace F; public sealed class Feature { }");
            Git(dir, "add", "src/Feature.cs");
            Git(dir, "commit", "-q", "-m", "feature code");
            string featureReleaseCommit = GitHead(dir);

            // Mint a release gate proof bound to baseRef..featureReleaseCommit (what the feature's gate covered).
            AffectedPlan plan = new AffectedTestPlanner().Plan(dir, baseRef, featureReleaseCommit, "Release");
            var affectedProof = new AffectedTestProof(
                JsonContractDefaults.SchemaVersion, baseRef, "HEAD", "Release",
                AffectedTestProofHasher.HashPlan(plan), AffectedTestProofHasher.HashTestScope([]),
                AffectedTestProofHasher.HashExecutedTests([]), FullSuite: true, FullSuiteReason: "release lane", plan, []);
            GateLadder ladder = GateLadderResolver.Resolve(dir).Ladder!;
            var persisted = new PersistedGateProof(
                JsonContractDefaults.SchemaVersion,
                ChangeSetIdentity.Of(dir, baseRef, featureReleaseCommit), baseRef, Lane.Release,
                new GateProof(JsonContractDefaults.SchemaVersion, StageOutcome.Pass, [], [], affectedProof,
                    Tier: ladder.Tier, LadderCoverage: ladder.Coverage()),
                featureReleaseCommit);

            // An unrelated later commit lands on top, moving HEAD (a separate bug fix on the same branch).
            File.WriteAllText(Path.Combine(dir, "src", "Unrelated.cs"), "namespace U; public sealed class Unrelated { }");
            Git(dir, "add", "src/Unrelated.cs");
            Git(dir, "commit", "-q", "-m", "fix: unrelated later code change");

            // Bounded to the feature's own release commit -> the recorded plan still reproduces -> NO planner-hash drift.
            IReadOnlyList<string> bounded = GateProofValidator.ValidateAffectedTestProof(dir, persisted, featureReleaseCommit);
            Assert.DoesNotContain(bounded, r => r.Contains("planner hash", StringComparison.OrdinalIgnoreCase));

            // Re-planned against live HEAD (the pre-fix behaviour) -> the unrelated commit moves the endpoint -> drift.
            IReadOnlyList<string> liveHead = GateProofValidator.ValidateAffectedTestProof(dir, persisted, null);
            Assert.Contains(liveHead, r => r.Contains("planner hash", StringComparison.OrdinalIgnoreCase));
        }
        finally { ForceDelete(dir); }
    }

    /// <summary>
    /// 030 (bug-release-bridge) regression: the active feature PARKED AT drift-review stays a VALID train member after a
    /// separate fix lands ON TOP of the same branch and the now-stale stages are re-gated + re-stamped. The drift-review
    /// re-stamp RE-BINDS the live gate proof to the new cycle HEAD; before this fix the train re-validated that live
    /// proof against the FROZEN implement→drift-review transition commit (no →release transition exists yet, so the 023
    /// fix's <c>featureReleaseHead</c> was null and it fell back to the stale <c>completion.CommitSha</c>), so the
    /// re-planned affected-test scope diverged ("planner hash does not match the current change set") and the unchanged
    /// feature read invalid — exactly the failure hit when bundling the 030 fix onto the 029 cycle. The fix validates
    /// the active drift-review feature's live proof against the current HEAD where it was actually minted.
    /// </summary>
    [Fact]
    public void ReleaseTrain_ActiveDriftReviewFeature_StaysValid_AfterOnTopCommitReGateAndRestamp()
    {
        string dir = InitRepo();
        try
        {
            WriteWorkflow(dir,
                "schemaVersion: 2\nstages:\n" +
                "  - id: specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n" +
                "  - id: implement\n    kind: diff\n    prereqs: [specify]\n" +
                "  - id: drift-review\n    kind: review\n    produces: docs/reviews/{feature}-drift-review.md\n    prereqs: [implement]\n    next: [release]\n" +
                "  - id: release\n    kind: release\n    prereqs: [drift-review]\n");

            var service = new CycleService(dir);
            WriteCompletedTaskFile(dir, "001-only");
            Git(dir, "add", "docs/tasks/001-only-tasks.md");
            Git(dir, "commit", "-q", "-m", "seed 001-only task file");

            Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
            File.WriteAllText(Path.Combine(dir, "docs", "specs", "001-only.md"), "spec body");
            Git(dir, "add", "docs/specs/001-only.md");

            service.Stamp("specify", "001-only", null);
            service.Stamp("implement", null, null);
            WritePassingGateProofForCurrentDiff(dir);
            service.Stamp("drift-review", null, null);
            Assert.True(service.GetReleaseTrain().Valid); // valid at the original drift-review HEAD

            // A SEPARATE fix lands ON TOP of the parked drift-review cycle, growing the change set.
            Directory.CreateDirectory(Path.Combine(dir, "src"));
            File.WriteAllText(Path.Combine(dir, "src", "OnTopFix.cs"), "namespace B; public sealed class OnTopFix { }");
            Git(dir, "add", "src/OnTopFix.cs");
            Git(dir, "commit", "-q", "-m", "fix: a separate bug landing on the same branch");

            // The legitimate recovery (NOT a rubber stamp): re-gate the grown diff and re-stamp the now-stale stages.
            // A source change escalates the affected-test plan to a full suite (fail-closed), exactly as the real gate
            // did when the 030 fix code landed on the 029 cycle — so the re-gate proof is full-suite.
            service.Stamp("implement", null, null);
            WritePassingGateProofForCurrentDiff(dir, Lane.Normal, fullSuite: true);
            service.Stamp("drift-review", null, null);

            // The active drift-review feature's live proof is re-bound to the NEW HEAD -> still a valid member.
            CycleReleaseTrain train = service.GetReleaseTrain();
            Assert.True(train.Valid, string.Join("; ", train.Blockers));
            CycleReleaseTrainFeature member = Assert.Single(train.Features);
            Assert.Equal("001-only", member.Feature);
            Assert.Equal("included", member.InclusionStatus);
        }
        finally { ForceDelete(dir); }
    }
}
