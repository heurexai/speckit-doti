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
}
