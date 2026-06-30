using Hx.Cycle.Core;
using Hx.Cycle.Core.Tasks;
using Hx.Tooling.Contracts;
using Xunit;
using static Hx.Cycle.Tests.CycleTestFixtures;

namespace Hx.Cycle.Tests;

/// <summary>
/// 030 (bug-release-bridge): the release train must let a SINGLE completed cycle anchor its own release — PART A
/// counts the active feature parked at drift-review (no prior completed-unreleased anchor required), and PART B bridges
/// a test-passed /doti-bug mini-cycle so a bug-fix-only repo releases. These are git-free seam tests: a hand-built
/// <see cref="CycleState"/> on a real <see cref="CycleStateStore"/>, the bug members injected as the same delegate the
/// release-aware callers wire in. Regression: a feature still at implement is NOT a member; a no-bug, no-feature train
/// stays invalid; an empty bug provider (a not-test-passed bug cycle) does not validate a feature-less train.
/// </summary>
public sealed class ReleaseTrainBridgeTests
{
    // ---- PART A: the active feature at drift-review counts (single-feature, no prior anchor) ----

    [Fact]
    public void SingleFeatureAtDriftReview_withNoPriorAnchor_isAValidTrainWithThatFeatureIncluded()
    {
        string dir = NewTempDir();
        try
        {
            CycleService service = ServiceAtDriftReview(dir, "001-only", bugMembers: null);

            CycleReleaseTrain train = service.GetReleaseTrain();

            Assert.True(train.Valid, string.Join("; ", train.Blockers));
            CycleReleaseTrainFeature member = Assert.Single(train.Features);
            Assert.Equal("001-only", member.Feature);
            Assert.Equal("drift-review", member.CompletedStage);
            Assert.Equal("included", member.InclusionStatus);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void FeatureStillAtImplement_isNotYetAReleaseMember_soTheTrainIsInvalid()
    {
        string dir = NewTempDir();
        try
        {
            // Same recorded implement→drift-review transition, but the cycle is parked at IMPLEMENT (the next stage has
            // not been reached). It must NOT be counted as a releasable member — fail-closed (was already true; pinned).
            CycleService service = ServiceWithImplementTransition(dir, "001-wip", "implement", bugMembers: null);

            CycleReleaseTrain train = service.GetReleaseTrain();

            Assert.False(train.Valid);
            Assert.Empty(train.Features);
            Assert.Contains(train.Blockers, b => b.Contains("no completed unreleased feature cycles", StringComparison.Ordinal));
        }
        finally { Directory.Delete(dir, true); }
    }

    // ---- PART B: a test-passed bug mini-cycle bridges a bug-fix-only repo (no feature cycle) ----

    [Fact]
    public void TestPassedBugCycle_withNoFeatureCycle_isAValidTrainWithTheBugMemberIncluded()
    {
        string dir = NewTempDir();
        try
        {
            // No feature transitions at all (a bug-fix-only repo); one release-ready bug member is injected.
            CycleService service = ServiceAtSpecify(dir, "030-bug-x", bugMembers: _ => [BugMember("030-bug-x")]);

            CycleReleaseTrain train = service.GetReleaseTrain();

            Assert.True(train.Valid, string.Join("; ", train.Blockers));
            CycleReleaseTrainFeature member = Assert.Single(train.Features);
            Assert.Equal("030-bug-x", member.Feature);
            Assert.Equal("bug", member.CompletedStage);
            Assert.Equal("included", member.InclusionStatus);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void NoFeatureCycle_andABugProviderThatYieldsNothing_isStillInvalid()
    {
        string dir = NewTempDir();
        try
        {
            // A not-yet-test-passed bug cycle is NOT a member (the provider returns []); with no feature cycle either,
            // the train stays invalid — fail-closed (a release needs at least one real, proven member).
            CycleService service = ServiceAtSpecify(dir, "030-bug-x", bugMembers: _ => []);

            CycleReleaseTrain train = service.GetReleaseTrain();

            Assert.False(train.Valid);
            Assert.Empty(train.Features);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void FeatureAtDriftReview_plusABugMember_aggregatesBoth()
    {
        string dir = NewTempDir();
        try
        {
            // The combined train: a drift-review-complete feature AND a test-passed bug cycle release together.
            CycleService service = ServiceAtDriftReview(dir, "001-only", bugMembers: _ => [BugMember("030-bug-x")]);

            CycleReleaseTrain train = service.GetReleaseTrain();

            Assert.True(train.Valid, string.Join("; ", train.Blockers));
            Assert.Equal(["001-only", "030-bug-x"], train.Features.Select(f => f.Feature).ToArray());
        }
        finally { Directory.Delete(dir, true); }
    }

    // ---- fixtures ----

    private static CycleReleaseTrainFeature BugMember(string bugId) =>
        new(bugId, "bug", "", null, "pass", "not-required", "included", []);

    // A cycle whose active feature has completed drift-review: a recorded implement→drift-review transition (carrying a
    // gate-proof digest, so the per-feature proof is digest-attested with no live proof) and a hash-valid task file.
    private static CycleService ServiceAtDriftReview(
        string dir, string feature, Func<string, IReadOnlyList<CycleReleaseTrainFeature>>? bugMembers) =>
        ServiceWithImplementTransition(dir, feature, "drift-review", bugMembers);

    private static CycleService ServiceWithImplementTransition(
        string dir, string feature, string currentStage, Func<string, IReadOnlyList<CycleReleaseTrainFeature>>? bugMembers)
    {
        WriteHashValidTaskFile(dir, feature);
        var transition = new CycleTransitionRecord(
            JsonContractDefaults.SchemaVersion, feature, "implement", "drift-review",
            PreCommitHead: "base-sha", CommitSha: "impl-sha", ChangeSetId: "cs", MessageHash: "mh",
            CompletedAtUtc: "2026-06-30T00:00:00Z", GateProofDigest: "digest-present");
        WriteState(dir, feature, currentStage, [transition], completedUnreleased: null);
        return Service(dir, bugMembers);
    }

    // A cycle parked at specify with no implement/drift-review transitions — a bug-fix-only repo has no feature member.
    private static CycleService ServiceAtSpecify(
        string dir, string feature, Func<string, IReadOnlyList<CycleReleaseTrainFeature>>? bugMembers)
    {
        WriteState(dir, feature, "specify", transitions: null, completedUnreleased: null);
        return Service(dir, bugMembers);
    }

    private static CycleService Service(string dir, Func<string, IReadOnlyList<CycleReleaseTrainFeature>>? bugMembers) =>
        new(dir, new CycleStateStore(dir), TwoStageModel(dir), bugMembers);

    private static void WriteState(
        string dir, string feature, string currentStage,
        IReadOnlyList<CycleTransitionRecord>? transitions,
        IReadOnlyList<CycleCompletionRecord>? completedUnreleased) =>
        new CycleStateStore(dir).Write(new CycleState(
            JsonContractDefaults.SchemaVersion,
            feature,
            BaseRef: "base-sha",
            CurrentStage: currentStage,
            Stages: [],
            Transitions: transitions,
            CompletedUnreleasedCycles: completedUnreleased));

    private static void WriteHashValidTaskFile(string dir, string feature)
    {
        string taskDir = Path.Combine(dir, "docs", "tasks");
        Directory.CreateDirectory(taskDir);
        File.WriteAllText(Path.Combine(taskDir, feature + "-tasks.md"),
            "- [x] `T001` (FR-001, SC-001) - Complete the feature proof.\n");
        TaskHashStampResult stamp = DotiTaskCompletion.StampFeature(dir, feature);
        Assert.Equal(StageOutcome.Pass, stamp.Outcome);
    }
}
