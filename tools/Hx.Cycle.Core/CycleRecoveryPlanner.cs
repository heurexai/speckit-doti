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
    public static CycleRecoveryPlan Project(CycleCheckReport report, StageModel stageModel)
    {
        List<StageRecoveryStep> steps = report.Prerequisites
            .Where(prereq => !prereq.Ok)
            .Select(prereq => ToStep(report.Stage, prereq, stageModel))
            .ToList();

        bool recoverable = steps.All(step => step.Safety == RestampSafety.SafeReinterpret);
        return new CycleRecoveryPlan(JsonContractDefaults.SchemaVersion, report.Stage, recoverable, steps);
    }

    private static StageRecoveryStep ToStep(string target, StagePrereqResult prereq, StageModel stageModel)
    {
        RestampSafety? safety = prereq.StaleReason is { } reason ? RestampSafetyClassifier.Classify(reason) : null;
        string? command = TryResolveCommand(stageModel, prereq.Stage);
        string rerun = command is null ? string.Empty : $"/{command}";
        string next = safety switch
        {
            RestampSafety.SafeReinterpret => $"doti cycle refresh --target {target} --apply-safe",
            RestampSafety.RerunRequired => rerun,
            RestampSafety.NotBound => rerun,
            // missing / invalid / synthetic (release-train, commit-recovery): re-run the stage if known.
            _ => command is null ? $"resolve '{prereq.Stage}': {prereq.Status}" : rerun,
        };

        return new StageRecoveryStep(prereq.Stage, prereq.Status, prereq.Reason, safety, rerun, next);
    }

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
