namespace Hx.Cycle.Core;

/// <summary>The safety of re-stamping a stale stage (FR-005/006/007).</summary>
public enum RestampSafety
{
    /// <summary>Stale only because of a runner/binding-format migration — re-stamping re-binds the current,
    /// unchanged content without re-running the stage (FR-006).</summary>
    SafeReinterpret,

    /// <summary>027 FR-002: stale ONLY because the prerequisite binding moved (a pure edge/reorder) while the
    /// stage's own artifact and every shared prerequisite's content are byte-identical — re-stamping re-binds
    /// the unchanged content without re-running the stage. This tier is the machine-readable precondition; the
    /// <see cref="CycleRecoveryPlanner"/> still gates it (all producing upstreams Fresh AND the dependent is not
    /// review-kind) before it is auto-rebound, so a real content change can never reach an auto-rebind.</summary>
    ReBindContentEqual,

    /// <summary>Stale because a real input changed (own design content, an upstream artifact, or the change-set
    /// identity) — the stage's command must be re-run, not merely re-stamped (FR-007).</summary>
    RerunRequired,

    /// <summary>028 FR-004: stale because an upstream artifact's content changed VALUE on a doc/non-review,
    /// non-change-set-bound dependent (the <see cref="ReviewRebindEligibility.IsAttestable"/> case). The agent — NOT
    /// the engine — may clear it by reading the surfaced diff and recording a reviewed-no-impact verdict via
    /// <c>doti cycle review-rebind --attest no-impact</c>. It is deliberately NOT a <see cref="SafeReinterpret"/> /
    /// <see cref="ReBindContentEqual"/> tier, so <c>refresh --apply-safe</c> and the on-stamp cascade NEVER auto-apply
    /// it — only the recorded agent verdict can. A bare <c>doti cycle stamp</c> on it refuses (the in-Stamp fence).</summary>
    ReviewedNoImpact,

    /// <summary>Cannot be re-stamped — the stage's produced artifact is absent (FR-005); refuse.</summary>
    NotBound,
}

/// <summary>
/// Maps a stale stage's <see cref="StaleReason"/> to its <see cref="RestampSafety"/> — a pure, total function so
/// <c>doti cycle refresh</c> can never disagree with <see cref="FreshnessEvaluator"/>. Only the runner/binding
/// migrations (<see cref="StaleReason.MissingArtifactBinding"/>, <see cref="StaleReason.MissingBinding"/>) are safe
/// to auto-reinterpret, and a pure edge/reorder (<see cref="StaleReason.PrereqRebindable"/>) maps to the
/// content-equal rebind tier (still planner-gated); any real input change requires re-running the stage; an absent
/// artifact refuses.
/// </summary>
public static class RestampSafetyClassifier
{
    public static RestampSafety Classify(StaleReason reason) => reason switch
    {
        StaleReason.MissingArtifactBinding => RestampSafety.SafeReinterpret,
        StaleReason.MissingBinding => RestampSafety.SafeReinterpret,
        StaleReason.PrereqRebindable => RestampSafety.ReBindContentEqual,
        StaleReason.NotProduced => RestampSafety.NotBound,
        StaleReason.OwnArtifactChanged => RestampSafety.RerunRequired,
        StaleReason.PrereqArtifactChanged => RestampSafety.RerunRequired,
        StaleReason.ChangeSetDiffers => RestampSafety.RerunRequired,
        _ => RestampSafety.RerunRequired,
    };

    /// <summary>
    /// 028 FR-004: the agent-eligibility-aware classification. <see cref="StaleReason.PrereqArtifactChanged"/> maps to
    /// the agent-gated <see cref="RestampSafety.ReviewedNoImpact"/> tier ONLY when <paramref name="attestable"/> (the
    /// <see cref="ReviewRebindEligibility.IsAttestable"/> predicate held for this stage). Otherwise it stays the hard
    /// <see cref="RestampSafety.RerunRequired"/> from the single-argument map. Every other reason is unchanged — the
    /// agent gate is reachable from PrereqArtifactChanged alone, never from an own-artifact/identity/edge-move stale.
    /// </summary>
    public static RestampSafety Classify(StaleReason reason, bool attestable) =>
        reason == StaleReason.PrereqArtifactChanged && attestable
            ? RestampSafety.ReviewedNoImpact
            : Classify(reason);
}
