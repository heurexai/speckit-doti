using Hx.Gate.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>Unit tests for the gate runner's pure logic: lane resolution and proof aggregation.
/// The full ladder is exercised end-to-end by `gate run` (validated on the scaffold + generated repos).</summary>
public sealed class GateTests
{
    [Theory]
    [InlineData("auto", Lane.Normal)]
    [InlineData("normal", Lane.Normal)]
    [InlineData("advisory", Lane.Advisory)]
    [InlineData("release", Lane.Release)]
    [InlineData("RELEASE", Lane.Release)]
    public void Resolve_maps_known_profiles_to_lanes(string profile, Lane expected)
    {
        LaneDecision decision = LaneResolver.Resolve(profile);
        Assert.Equal(expected, decision.Lane);
        Assert.Equal(StageOutcome.Pass, decision.Outcome);
    }

    [Fact]
    public void Resolve_fails_closed_on_an_unknown_profile()
    {
        LaneDecision decision = LaneResolver.Resolve("bogus");
        Assert.Equal(StageOutcome.Fail, decision.Outcome);
        Assert.Contains("unknown profile", decision.Reason);
    }

    [Fact]
    public void Aggregate_passes_when_all_steps_pass_or_skip()
    {
        var steps = new[]
        {
            Step("a", StageOutcome.Pass),
            Step("b", StageOutcome.Skipped),
            Step("c", StageOutcome.Pass),
        };
        Assert.Equal(StageOutcome.Pass, GateRunner.Aggregate(steps));
    }

    [Theory]
    [InlineData(StageOutcome.Fail)]
    [InlineData(StageOutcome.Blocked)]
    public void Aggregate_fails_closed_on_a_failed_or_blocked_step(StageOutcome bad)
    {
        // A missing/stale Sentrux baseline surfaces as a Blocked sentrux-check step; the gate must fail.
        var steps = new[]
        {
            Step("a", StageOutcome.Pass),
            Step("sentrux-check", bad),
            Step("c", StageOutcome.Skipped),
        };
        Assert.Equal(StageOutcome.Fail, GateRunner.Aggregate(steps));
    }

    private static GateStep Step(string name, StageOutcome outcome) =>
        new(name, outcome, [new GateEvidence(name, name)]);
}
