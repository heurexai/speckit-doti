using Hx.Tooling.Contracts;
using Hx.Cycle.Core.Tasks;

namespace Hx.Cycle.Core;

public sealed partial class CycleService
{
    public CycleReleaseTrain GetReleaseTrain()
    {
        CycleState state = _store.Read()
            ?? throw new InvalidOperationException(
                $"No cycle state at {CycleStateStore.RelativePath}; complete a Doti cycle before release.");

        return BuildReleaseTrain(state);
    }

    public CycleReleaseTrain MarkReleaseTrainReleased()
    {
        CycleState state = _store.Read()
            ?? throw new InvalidOperationException(
                $"No cycle state at {CycleStateStore.RelativePath}; complete a Doti cycle before release.");
        CycleReleaseTrain train = BuildReleaseTrain(state);
        if (!train.Valid)
        {
            throw new InvalidOperationException(
                "Release train is invalid: " + string.Join("; ", train.Blockers));
        }

        IReadOnlyList<CycleCompletionRecord> included = CompletionRecordsForRelease(state);
        _store.Write(state with
        {
            CompletedUnreleasedCycles = [],
            ReleasedCycles = (state.ReleasedCycles ?? []).Concat(included).ToArray(),
        });
        return train;
    }

    private CycleReleaseTrain BuildReleaseTrain(CycleState state)
    {
        IReadOnlyList<CycleCompletionRecord> completions = CompletionRecordsForRelease(state);
        var features = completions
            .Select(completion => FeatureForCompletion(state, completion))
            .ToArray();
        var blockers = new List<string>();
        if (features.Length == 0)
        {
            blockers.Add("no completed unreleased feature cycles are ready for release");
        }

        blockers.AddRange(features.SelectMany(feature => feature.Blockers));
        return new CycleReleaseTrain(
            JsonContractDefaults.SchemaVersion,
            blockers.Count == 0,
            features,
            blockers);
    }

    private IReadOnlyList<CycleCompletionRecord> CompletionRecordsForRelease(CycleState state)
    {
        var completions = new List<CycleCompletionRecord>();
        completions.AddRange(state.CompletedUnreleasedCycles ?? []);

        if (string.Equals(state.CurrentStage, "release", StringComparison.OrdinalIgnoreCase)
            && CompletionForActiveReleaseFeature(state) is { } active
            && completions.All(c => !string.Equals(c.Feature, active.Feature, StringComparison.OrdinalIgnoreCase)))
        {
            completions.Add(active);
        }

        return completions;
    }

    private CycleCompletionRecord? CompletionForActiveReleaseFeature(CycleState state)
    {
        CycleTransitionRecord? transition = (state.Transitions ?? [])
            .LastOrDefault(t =>
                string.Equals(t.Feature, state.Feature, StringComparison.OrdinalIgnoreCase)
                && string.Equals(t.Stage, "drift-review", StringComparison.OrdinalIgnoreCase)
                && string.Equals(t.NextStage, "release", StringComparison.OrdinalIgnoreCase));

        return transition is null ? null : CompletionFromTransition(transition, state);
    }

    private CycleReleaseTrainFeature FeatureForCompletion(CycleState state, CycleCompletionRecord completion)
    {
        var blockers = new List<string>();
        if (!string.Equals(completion.Stage, "drift-review", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add($"feature '{completion.Feature}' completed stage '{completion.Stage}', expected drift-review");
        }

        if (string.IsNullOrWhiteSpace(completion.CommitSha))
        {
            blockers.Add($"feature '{completion.Feature}' has no transition commit");
        }

        TaskCompletionResult taskCompletion = DotiTaskCompletion.ValidateFeature(_repositoryRoot, completion.Feature);
        if (taskCompletion.Outcome != StageOutcome.Pass)
        {
            foreach (TaskCompletionDiagnostic diagnostic in taskCompletion.Diagnostics)
            {
                blockers.Add($"feature '{completion.Feature}' task completion failed: {diagnostic.ToEvidenceMessage()}");
            }
        }

        string? stageCommitRange = string.IsNullOrWhiteSpace(completion.BaseRef)
            ? null
            : $"{completion.BaseRef}..{completion.CommitSha}";
        return new CycleReleaseTrainFeature(
            completion.Feature,
            completion.Stage,
            completion.CommitSha,
            stageCommitRange,
            TaskCompletionStatus(taskCompletion),
            string.IsNullOrWhiteSpace(completion.GateProofDigest) ? "not-required" : "present",
            blockers.Count == 0 ? "included" : "invalid",
            blockers);
    }

    private static string TaskCompletionStatus(TaskCompletionResult result) =>
        result.Outcome == StageOutcome.Pass ? "pass" : "fail";
}
