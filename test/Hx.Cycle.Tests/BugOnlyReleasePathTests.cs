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
    public void MarkReleaseTrainReleased_onABugOnlyRelease_doesNotReValidateAfterTag_soItNeverSelfExcludes()
    {
        // 037 (release-non-hx-product / bug-only self-reference): MarkReleaseTrainReleased runs in the release flow
        // AFTER the release tag is created — at which point a bug-only release's OWN bug is reachable-from-tag
        // (correctly "released"), so a re-built bug-only train is empty. It must NOT re-validate + throw here (that
        // would fail the very release that is succeeding). The fail-closed for a genuinely-empty release is the
        // release-START GetReleaseTrain check (see NoCycleState_andNoBugMembers_stillFailsClosed), which runs BEFORE
        // the tag. Marking is a no-op for a bug-only repo (BugReleaseGit self-maintains shipped status via the tag), so
        // it writes no cycle-state and does not throw even when the post-tag provider reports no members.
        string dir = NewTempDir();
        try
        {
            CycleService service = ServiceWithNoCycleState(dir, bugMembers: _ => []); // models the post-tag state

            service.MarkReleaseTrainReleased(); // must NOT throw (pre-037 this threw, failing a succeeding release)

            Assert.False(
                new CycleStateStore(dir).Exists,
                "a bug-only release marks nothing — BugReleaseGit self-maintains shipped status via the release tag.");
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

    [Fact]
    public void Check_release_onABugOnlyRepo_withAValidBugTrain_passesWithoutRequiringFeatureStamps()
    {
        // 038 (bug-only-release-cycle-check): the false blocker that misled the ergon release agent. Pre-fix,
        // Check("release") on a null-state repo marked specify+plan "missing" and FAILED, tempting a fabricated feature
        // cycle. The fix delegates to the bug-aware train: a test-passed unreleased bug makes release READY with no
        // feature stamps, and — critically — NO "missing" feature-stage result is emitted.
        string dir = NewTempDir();
        try
        {
            var service = new CycleService(dir, new CycleStateStore(dir), ModelWithRelease(dir), _ => [BugMember("038-bug-x")]);

            CycleCheckReport report = service.Check("release");

            Assert.True(report.Passed, string.Join("; ", report.Prerequisites.Select(p => $"{p.Stage}:{p.Reason}")));
            Assert.DoesNotContain(report.Prerequisites, p =>
                string.Equals(p.Status, "missing", StringComparison.Ordinal));
            Assert.Contains(report.Prerequisites, p => p.Stage == "release-train" && p.Ok);
            Assert.NotNull(report.ReleaseTrain);
            Assert.True(report.ReleaseTrain!.Valid);
            Assert.False(new CycleStateStore(dir).Exists, "checking a bug-only release must never fabricate a cycle-state.json");
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Check_release_onABugOnlyRepo_withNoReleasableBug_stillFailsClosed()
    {
        // The fix must NOT turn "no cycle state" into "always pass": a repo with neither a feature cycle nor a
        // releasable bug fails closed on the train's own blocker — never a fabricated pass.
        string dir = NewTempDir();
        try
        {
            var service = new CycleService(dir, new CycleStateStore(dir), ModelWithRelease(dir), _ => []);

            CycleCheckReport report = service.Check("release");

            Assert.False(report.Passed);
            Assert.Contains(report.Prerequisites, p =>
                p.Stage == "release-train" && !p.Ok && p.Reason!.Contains("no completed unreleased", StringComparison.Ordinal));
            Assert.False(new CycleStateStore(dir).Exists);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Stamp_release_onABugOnlyRepo_failsClosed_andFabricatesNoCycleState()
    {
        // 038 hardening (adversarial-review finding): Check("release") is bug-only-aware, but Stamp consumes the SAME
        // chokepoint via EnsurePrerequisitesFresh. Without the RefuseBugOnlyReleaseStamp guard, `doti cycle stamp
        // --stage release` on a bug-only repo — even one with a valid bug train (so the check passes) — would write a
        // FABRICATED feature cycle-state.json for a made-up feature, the exact anti-pattern 038 closes. The /09 skill
        // instructs precisely `doti cycle stamp --stage release`, so an agent mis-running it on a bug-only repo must be
        // stopped. Stamping release with no feature cycle must fail closed and write nothing.
        string dir = NewTempDir();
        try
        {
            var service = new CycleService(dir, new CycleStateStore(dir), ModelWithRelease(dir), _ => [BugMember("038-bug-x")]);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => service.Stamp("release", "099-fake", null));

            Assert.Contains("a bug-only release is not stamped", ex.Message, StringComparison.Ordinal);
            Assert.False(
                new CycleStateStore(dir).Exists,
                "a refused bug-only release stamp must fabricate no cycle-state.json");
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Check_aFeatureStage_onABugOnlyRepo_isNotShortCircuited_soThe038BranchIsReleaseScopedOnly()
    {
        // The 038 branch fires ONLY for target.Id == "release": a FEATURE-stage check on a null-state repo must NOT be
        // intercepted by the bug-only path (which would wrongly report a feature stage "ready"). Proof, git-free like
        // the rest of this file: Check("plan") is NOT short-circuited, so it proceeds to the normal feature-freshness
        // path, which resolves a change-set identity over git — absent in this temp dir, so it throws. Had the branch
        // been mis-scoped to `state is null` alone, Check("plan") would have returned via BugOnlyReleaseCheck and NOT
        // thrown. The throw is therefore the release-only scoping guarantee (the release path itself never reaches git).
        string dir = NewTempDir();
        try
        {
            var service = new CycleService(dir, new CycleStateStore(dir), ModelWithRelease(dir), _ => [BugMember("038-bug-x")]);

            Assert.ThrowsAny<InvalidOperationException>(() => service.Check("plan"));
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
