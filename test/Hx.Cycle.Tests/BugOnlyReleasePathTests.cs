using Hx.Cycle.Core;
using Hx.Tooling.Contracts;
using Xunit;
using static Hx.Cycle.Tests.CycleTestFixtures;

namespace Hx.Cycle.Tests;

/// <summary>
/// 033 (bug-only-release-path): three guards sat UPSTREAM of the 030 bug-release bridge and threw on a null
/// <c>.doti/cycle-state.json</c> before the bridge's bug-half ever ran — so a confirmed, test-passed
/// <c>/doti-bug</c> mini-cycle (no numbered feature cycle, hence no cycle state) could not be released. These tests
/// exercise <see cref="CycleService.GetReleaseTrain"/>/<see cref="CycleService.MarkReleaseTrainReleased"/> against a
/// temp dir where <see cref="CycleStateStore.Read"/> genuinely returns null (a real store over a directory that has
/// no <c>.doti/cycle-state.json</c> — a <see cref="CycleState"/> is NEVER written to the store here, unlike
/// <see cref="ReleaseTrainBridgeTests"/>, which hand-builds one). Regression: the 4 existing
/// <see cref="ReleaseTrainBridgeTests"/> facts (all state-present) are unchanged by this fix.
/// </summary>
public sealed class BugOnlyReleasePathTests
{
    [Fact]
    public void NoCycleState_withOneConfirmedBugMember_isAValidTrainWithThatBugIncluded()
    {
        string dir = NewTempDir();
        try
        {
            CycleService service = ServiceWithNoCycleState(dir, bugMembers: _ => [BugMember("033-bug-x")]);

            CycleReleaseTrain train = service.GetReleaseTrain();

            Assert.True(train.Valid, string.Join("; ", train.Blockers));
            CycleReleaseTrainFeature member = Assert.Single(train.Features);
            Assert.Equal("033-bug-x", member.Feature);
            Assert.Equal("bug", member.CompletedStage);
            Assert.Equal("included", member.InclusionStatus);
            Assert.False(new CycleStateStore(dir).Exists, "the bug-only path must never fabricate a cycle-state.json");
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void NoCycleState_andNoBugMembers_stillFailsClosed()
    {
        string dir = NewTempDir();
        try
        {
            // No cycle state AND an empty bug provider (no confirmed/test-passed bug either) — the fix must not turn
            // "no cycle state" into "always valid". Fail-closed, same as the feature-only "no completed unreleased
            // feature cycles" blocker.
            CycleService service = ServiceWithNoCycleState(dir, bugMembers: _ => []);

            CycleReleaseTrain train = service.GetReleaseTrain();

            Assert.False(train.Valid);
            Assert.Empty(train.Features);
            Assert.Contains(train.Blockers, b => b.Contains("no completed unreleased", StringComparison.Ordinal));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void NoCycleState_withNoBugProviderWiredAtAll_stillFailsClosed()
    {
        // Mirrors the production default ctor (CycleService(repositoryRoot)), which wires NO bug provider — the
        // internal seam ctor's null bugReleaseMembers defaults identically (`_ => []`). Confirms the null-state path
        // does not accidentally validate a train with neither half populated when nothing is injected at all.
        string dir = NewTempDir();
        try
        {
            var service = new CycleService(dir, new CycleStateStore(dir), TwoStageModel(dir));

            CycleReleaseTrain train = service.GetReleaseTrain();

            Assert.False(train.Valid);
            Assert.Empty(train.Features);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void MarkReleaseTrainReleased_onABugOnlyTrain_writesNoCycleStateFile()
    {
        string dir = NewTempDir();
        try
        {
            CycleService service = ServiceWithNoCycleState(dir, bugMembers: _ => [BugMember("033-bug-x")]);

            CycleReleaseTrain released = service.MarkReleaseTrainReleased();

            Assert.True(released.Valid, string.Join("; ", released.Blockers));
            Assert.False(
                new CycleStateStore(dir).Exists,
                "BugReleaseGit self-maintains shipped status via tag-reachability; marking a bug-only train released must not fabricate a CycleState.");
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void MarkReleaseTrainReleased_onAnInvalidBugOnlyTrain_throwsAndWritesNothing()
    {
        string dir = NewTempDir();
        try
        {
            CycleService service = ServiceWithNoCycleState(dir, bugMembers: _ => []);

            Assert.Throws<InvalidOperationException>(() => service.MarkReleaseTrainReleased());

            Assert.False(new CycleStateStore(dir).Exists);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void RoundTrip_onceTheBugProviderReportsTheFixTagReachable_theNextTrainExcludesIt()
    {
        // Simulates BugReleaseGit.IsReleased flipping to true once a release tag makes the bug's fix commit reachable
        // (the self-maintaining round-trip 030 relies on): the SAME bug id, injected as already-released, yields an
        // empty train next time — with no cycle-state write anywhere in the sequence.
        string dir = NewTempDir();
        try
        {
            CycleService beforeTag = ServiceWithNoCycleState(dir, bugMembers: _ => [BugMember("033-bug-x")]);
            CycleReleaseTrain trainBeforeTag = beforeTag.GetReleaseTrain();
            Assert.True(trainBeforeTag.Valid, string.Join("; ", trainBeforeTag.Blockers));
            beforeTag.MarkReleaseTrainReleased();

            // After the (simulated) release tag, the provider's own git-reachability check now excludes the bug —
            // modeled here by the provider returning no members for the same bug id, exactly as BugReleaseGit.IsReleased
            // would once the fix commit becomes an ancestor of the new tag.
            CycleService afterTag = ServiceWithNoCycleState(dir, bugMembers: _ => []);
            CycleReleaseTrain trainAfterTag = afterTag.GetReleaseTrain();

            Assert.False(trainAfterTag.Valid);
            Assert.Empty(trainAfterTag.Features);
            Assert.False(new CycleStateStore(dir).Exists, "no cycle-state.json should ever appear across the round-trip");
        }
        finally { Directory.Delete(dir, true); }
    }

    // ---- fixtures ----

    private static CycleReleaseTrainFeature BugMember(string bugId) =>
        new(bugId, "bug", "", null, "pass", "not-required", "included", []);

    // A CycleService wired against a temp dir's real CycleStateStore that has NEVER had a CycleState written to it —
    // Read() genuinely returns null, exactly the bug-only-repo shape (no numbered feature cycle was ever started).
    private static CycleService ServiceWithNoCycleState(
        string dir, Func<string, IReadOnlyList<CycleReleaseTrainFeature>> bugMembers) =>
        new(dir, new CycleStateStore(dir), TwoStageModel(dir), bugMembers);
}
