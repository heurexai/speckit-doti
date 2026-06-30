namespace Hx.Cycle.Core;

/// <summary>
/// 028 FR-004 / B1 / H4: the ONE pure compound fence predicate — git-free and unit-testable — that decides whether a
/// stale stage is eligible for an agent-gated reviewed-no-impact rebind. A stage is attestable when ALL hold:
/// <list type="number">
/// <item>its stale reason is <see cref="StaleReason.PrereqArtifactChanged"/> — an upstream artifact's content changed
/// VALUE (the own artifact is unchanged; only prerequisite hashes diverge);</item>
/// <item>it is not a <c>review</c>-kind stage — a review verdict is a judgment over its inputs, so a moved input
/// invalidates it and it must be genuinely re-run, never attested;</item>
/// <item>it is not change-set-bound — a diff-kind stage (or one with a transitive diff prerequisite) is bound to the
/// code-state identity, never merely the doc content, and must re-run.</item>
/// </list>
/// Every other stale reason (<see cref="StaleReason.OwnArtifactChanged"/>, <see cref="StaleReason.ChangeSetDiffers"/>,
/// <see cref="StaleReason.NotProduced"/>, the binding migrations, the edge/reorder <see cref="StaleReason.PrereqRebindable"/>)
/// is NOT attestable — it is either a real re-author, an engine-only safe rebind, or a hard refuse. Fail-closed.
/// </summary>
public static class ReviewRebindEligibility
{
    /// <summary>The pure compound fence (the core overload). <paramref name="requiresChangeSetIdentity"/> is the
    /// change-set-bound test, determined exactly as <c>CycleService.CheckHelpers</c> does (kind=="diff" OR a transitive
    /// diff prerequisite) — see <see cref="RequiresChangeSetIdentity"/>.</summary>
    public static bool IsAttestable(StageFreshnessResult result, CycleStage stage, bool requiresChangeSetIdentity) =>
        result.StaleReason == StaleReason.PrereqArtifactChanged
        && !IsReviewKind(stage)
        && !requiresChangeSetIdentity;

    /// <summary>The two-argument convenience that resolves the change-set-bound test from <paramref name="stageModel"/>
    /// (still git-free + pure — the model is read from <c>workflow.yml</c>, not git).</summary>
    public static bool IsAttestable(StageFreshnessResult result, CycleStage stage, StageModel stageModel) =>
        IsAttestable(result, stage, RequiresChangeSetIdentity(stage, stageModel));

    /// <summary>
    /// The change-set-bound test, mirroring <c>CycleService.RequiresChangeSetIdentity</c>: a stage is change-set-bound
    /// when it is itself a <c>diff</c>-kind stage OR any stage in its transitive prerequisite closure is. Pure +
    /// git-free so the fence is testable without a repo.
    /// </summary>
    public static bool RequiresChangeSetIdentity(CycleStage stage, StageModel stageModel)
    {
        if (IsDiffKind(stage))
        {
            return true;
        }

        return stageModel.TransitivePrereqStages(stage.Id).Any(IsDiffKind);
    }

    private static bool IsReviewKind(CycleStage stage) =>
        string.Equals(stage.Kind, "review", StringComparison.OrdinalIgnoreCase);

    private static bool IsDiffKind(CycleStage stage) =>
        string.Equals(stage.Kind, "diff", StringComparison.OrdinalIgnoreCase);
}
