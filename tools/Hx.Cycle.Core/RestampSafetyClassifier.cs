namespace Hx.Cycle.Core;

/// <summary>The safety of re-stamping a stale stage (FR-005/006/007).</summary>
public enum RestampSafety
{
    /// <summary>Stale only because of a runner/binding-format migration — re-stamping re-binds the current,
    /// unchanged content without re-running the stage (FR-006).</summary>
    SafeReinterpret,

    /// <summary>Stale because a real input changed (own design content, an upstream artifact, or the change-set
    /// identity) — the stage's command must be re-run, not merely re-stamped (FR-007).</summary>
    RerunRequired,

    /// <summary>Cannot be re-stamped — the stage's produced artifact is absent (FR-005); refuse.</summary>
    NotBound,
}

/// <summary>
/// Maps a stale stage's <see cref="StaleReason"/> to its <see cref="RestampSafety"/> — a pure, total function so
/// <c>doti cycle refresh</c> can never disagree with <see cref="FreshnessEvaluator"/>. Only the runner/binding
/// migrations (<see cref="StaleReason.MissingArtifactBinding"/>, <see cref="StaleReason.MissingBinding"/>) are safe
/// to auto-reinterpret; any real input change requires re-running the stage; an absent artifact refuses.
/// </summary>
public static class RestampSafetyClassifier
{
    public static RestampSafety Classify(StaleReason reason) => reason switch
    {
        StaleReason.MissingArtifactBinding => RestampSafety.SafeReinterpret,
        StaleReason.MissingBinding => RestampSafety.SafeReinterpret,
        StaleReason.NotProduced => RestampSafety.NotBound,
        StaleReason.OwnArtifactChanged => RestampSafety.RerunRequired,
        StaleReason.PrereqArtifactChanged => RestampSafety.RerunRequired,
        StaleReason.ChangeSetDiffers => RestampSafety.RerunRequired,
        _ => RestampSafety.RerunRequired,
    };
}
