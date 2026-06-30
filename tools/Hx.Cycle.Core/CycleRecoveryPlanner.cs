using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

/// <summary>
/// Projects a fail-closed <see cref="CycleCheckReport"/> into an operator-facing recovery plan (FR-001): one step
/// per not-fresh prerequisite, each carrying its restamp-safety (via <see cref="RestampSafetyClassifier"/>) and the
/// single recommended next command. Pure — a projection of the check output, never a second freshness evaluator —
/// so the plan can never disagree with the chokepoint. <see cref="CycleRecoveryPlan.Recoverable"/> is true when
/// every blocking step is a safe re-interpret (so <c>doti cycle refresh --apply-safe</c> fully recovers).
/// </summary>
public static class CycleRecoveryPlanner
{
    /// <summary>027 FR-004: the status surfaced for a current-graph-required stage that is absent from cycle-state
    /// (a stage inserted/reordered into workflow.yml after the in-flight cycle was stamped). It carries the single
    /// <c>/NN</c> command that produces+stamps it, instead of an opaque "missing"/"not stamped" dead-end.</summary>
    public const string InsertedStageStatus = "inserted-stage";

    public static CycleRecoveryPlan Project(CycleCheckReport report, StageModel stageModel)
    {
        List<StageRecoveryStep> steps = report.Prerequisites
            .Where(prereq => !prereq.Ok)
            .Select(prereq => ToStep(report, prereq, stageModel))
            .ToList();

        bool recoverable = steps.All(step =>
            step.Safety is RestampSafety.SafeReinterpret or RestampSafety.ReBindContentEqual);
        return new CycleRecoveryPlan(JsonContractDefaults.SchemaVersion, report.Stage, recoverable, steps);
    }

    private static StageRecoveryStep ToStep(CycleCheckReport report, StagePrereqResult prereq, StageModel stageModel)
    {
        string? command = TryResolveCommand(stageModel, prereq.Stage);
        string rerun = command is null ? string.Empty : $"/{command}";

        // 027 FR-004: a known stage that is absent from cycle-state but required by the current graph is an
        // inserted/reordered stage — surface it as `inserted-stage` with its `/NN` command, not an opaque dead-end.
        if (IsInsertedStage(prereq, command))
        {
            return new StageRecoveryStep(prereq.Stage, InsertedStageStatus, prereq.Reason, Safety: null, rerun, rerun);
        }

        RestampSafety? safety = prereq.StaleReason is { } reason
            ? RestampSafetyClassifier.Classify(reason, IsAttestablePrereq(reason, prereq.Stage, stageModel))
            : null;

        // 027 FR-003: gate the content-equal rebind tier. A ReBindContentEqual step is only safe to auto-rebind
        // when every producing stage in its transitive closure is Fresh IN THIS SAME report AND the dependent is
        // not a review-kind stage (a review verdict is a judgment over its inputs; a moved input invalidates it).
        // Otherwise it is downgraded to RerunRequired — fail-closed, never an auto-rebind of an unproven input.
        if (safety == RestampSafety.ReBindContentEqual
            && !IsContentEqualRebindSafe(report, prereq.Stage, stageModel))
        {
            safety = RestampSafety.RerunRequired;
        }

        string next = safety switch
        {
            RestampSafety.SafeReinterpret => $"doti cycle refresh --target {report.Stage} --apply-safe",
            RestampSafety.ReBindContentEqual => $"doti cycle refresh --target {report.Stage} --apply-safe",
            // 028 FR-003/FR-004: the agent-gated reviewed-no-impact rebind — NOT a refresh; the agent reads the
            // surfaced upstream diff, then records the verdict on the stale stage ITSELF (its own freshness, not the
            // target's prerequisites). Never auto-applied by `refresh --apply-safe`.
            RestampSafety.ReviewedNoImpact => $"doti cycle review-rebind --target {prereq.Stage} --attest no-impact",
            RestampSafety.RerunRequired => rerun,
            RestampSafety.NotBound => rerun,
            // missing / invalid / synthetic (release-train, commit-recovery): re-run the stage if known.
            _ => command is null ? $"resolve '{prereq.Stage}': {prereq.Status}" : rerun,
        };

        return new StageRecoveryStep(prereq.Stage, prereq.Status, prereq.Reason, safety, rerun, next, prereq.ChangedPrereqPaths);
    }

    /// <summary>
    /// 028 FR-004: the planner-side attestability test (the pure <see cref="ReviewRebindEligibility"/> fence applied to
    /// a prerequisite step). A prerequisite stale via <see cref="StaleReason.PrereqArtifactChanged"/> is agent-eligible
    /// only when the stage is non-review and not change-set-bound. A synthetic/unknown step is never attestable
    /// (fail-closed). This is a projection over the SAME stale reason the check produced — never a second freshness
    /// evaluation — so the planner can never disagree with the chokepoint.
    /// </summary>
    private static bool IsAttestablePrereq(StaleReason reason, string stageId, StageModel stageModel)
    {
        if (reason != StaleReason.PrereqArtifactChanged)
        {
            return false;
        }

        CycleStage stage;
        try
        {
            stage = stageModel.Find(stageId);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        return !IsReviewKind(stage)
            && !ReviewRebindEligibility.RequiresChangeSetIdentity(stage, stageModel);
    }

    private static bool IsInsertedStage(StagePrereqResult prereq, string? command) =>
        command is not null
        && string.Equals(prereq.Status, "missing", StringComparison.Ordinal);

    /// <summary>
    /// 027 FR-003: true when a <see cref="RestampSafety.ReBindContentEqual"/> step is safe to auto-rebind — the
    /// dependent is not review-kind AND every producing stage in its transitive prerequisite closure is Fresh in
    /// <paramref name="report"/>. A pure projection over the SAME check report (never a second freshness evaluator),
    /// so refresh, the on-stamp cascade, and the chokepoint can never disagree. A producing upstream missing from the
    /// report (not in the target's closure) is treated as not-Fresh — fail-closed.
    /// </summary>
    private static bool IsContentEqualRebindSafe(CycleCheckReport report, string stageId, StageModel stageModel)
    {
        CycleStage stage;
        try
        {
            stage = stageModel.Find(stageId);
        }
        catch (InvalidOperationException)
        {
            return false; // a synthetic/unknown step is never an auto-rebind target
        }

        if (IsReviewKind(stage))
        {
            return false;
        }

        foreach (CycleStage producer in stageModel.TransitivePrereqStages(stageId))
        {
            if (producer.Produces is null)
            {
                continue; // only stages that PRODUCE an artifact carry content the dependent re-binds against
            }

            StagePrereqResult? upstream = report.Prerequisites
                .FirstOrDefault(p => string.Equals(p.Stage, producer.Id, StringComparison.OrdinalIgnoreCase));
            if (upstream is null || !upstream.Ok || !string.Equals(upstream.Status, "fresh", StringComparison.Ordinal))
            {
                return false; // an upstream that is not provably Fresh in this report blocks the rebind
            }
        }

        return true;
    }

    private static bool IsReviewKind(CycleStage stage) =>
        string.Equals(stage.Kind, "review", StringComparison.OrdinalIgnoreCase);

    private static string? TryResolveCommand(StageModel stageModel, string stageId)
    {
        try
        {
            return stageModel.Find(stageId).Command;
        }
        catch (InvalidOperationException)
        {
            return null; // synthetic prereq entries (release-train / commit-recovery / cycle) are not stages
        }
    }
}
