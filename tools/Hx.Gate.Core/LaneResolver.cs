using Hx.Tooling.Contracts;

namespace Hx.Gate.Core;

/// <summary>Resolves a <c>--profile</c> string to a <see cref="LaneDecision"/>. `auto` resolves to the
/// Normal lane (the gate has no signal to auto-classify developer-vs-commit, and Release is always an
/// explicit act / the release lane). An unknown profile fails closed.</summary>
public static class LaneResolver
{
    public static LaneDecision Resolve(string profile)
    {
        string normalized = profile.Trim().ToLowerInvariant();
        return normalized switch
        {
            "auto" => new LaneDecision(Lane.Normal, StageOutcome.Pass, "auto -> normal (no release/affected-change signal to classify on)"),
            "normal" => new LaneDecision(Lane.Normal, StageOutcome.Pass, "explicit normal (commit/readiness)"),
            "advisory" => new LaneDecision(Lane.Advisory, StageOutcome.Pass, "explicit advisory (developer/build loop)"),
            "release" => new LaneDecision(Lane.Release, StageOutcome.Pass, "explicit release"),
            _ => new LaneDecision(Lane.Normal, StageOutcome.Fail, $"unknown profile '{profile}' (use auto|advisory|normal|release)"),
        };
    }
}
