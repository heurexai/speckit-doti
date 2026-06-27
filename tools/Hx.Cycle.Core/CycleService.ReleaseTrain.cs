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

        // FR-037/SC-019: cross-feature release-train drift (a later feature changed paths an earlier feature owns).
        IReadOnlyList<ReleaseTrainDriftFinding> driftFindings =
            new ReleaseTrainDriftDetector().Detect(_repositoryRoot, _stageModel, features);
        blockers.AddRange(driftFindings.Select(finding => finding.Reason));

        return new CycleReleaseTrain(
            JsonContractDefaults.SchemaVersion,
            blockers.Count == 0,
            features,
            blockers,
            driftFindings.Count > 0 ? driftFindings : null);
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

        return transition is null ? null : CompletionFromTransition(transition, state with { BaseRef = transition.PreCommitHead });
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

        (string gateProofStatus, IReadOnlyList<string> gateProofBlockers) = GateProofStatusForFeature(state, completion);
        blockers.AddRange(gateProofBlockers);

        string? stageCommitRange = string.IsNullOrWhiteSpace(completion.BaseRef)
            ? null
            : $"{completion.BaseRef}..{completion.CommitSha}";
        return new CycleReleaseTrainFeature(
            completion.Feature,
            completion.Stage,
            completion.CommitSha,
            stageCommitRange,
            TaskCompletionStatus(taskCompletion),
            gateProofStatus,
            blockers.Count == 0 ? "included" : "invalid",
            blockers);
    }

    /// <summary>
    /// FR-036: the per-feature gate-proof status — VALIDATED, not "does the digest string exist?". The feature's gate
    /// proof was minted at its implement→drift-review transition; a feature with no diff/implement stage required no
    /// proof. For the feature being released NOW (its proof + change set are current) the persisted proof is
    /// re-validated with the same validators the transition used; earlier features are attested by their recorded
    /// digest (each was validated at its own transition). A stale/invalid live proof becomes a release blocker.
    /// </summary>
    private (string Status, IReadOnlyList<string> Blockers) GateProofStatusForFeature(CycleState state, CycleCompletionRecord completion)
    {
        CycleTransitionRecord? implementTransition = (state.Transitions ?? [])
            .LastOrDefault(t =>
                string.Equals(t.Feature, completion.Feature, StringComparison.OrdinalIgnoreCase)
                && string.Equals(t.Stage, "implement", StringComparison.OrdinalIgnoreCase)
                && string.Equals(t.NextStage, "drift-review", StringComparison.OrdinalIgnoreCase));

        bool hasImplement = implementTransition is not null;
        string? digest = implementTransition?.GateProofDigest;
        bool isActiveReleaseFeature =
            string.Equals(completion.Feature, state.Feature, StringComparison.OrdinalIgnoreCase)
            && string.Equals(state.CurrentStage, "release", StringComparison.OrdinalIgnoreCase);

        PersistedGateProof? persisted = hasImplement && !string.IsNullOrWhiteSpace(digest) && isActiveReleaseFeature
            ? new GateProofStore(_repositoryRoot).Read()
            : null;

        IReadOnlyList<string> reasons = persisted is null ? [] : ValidatePersistedGateProof(persisted);
        string status = ClassifyGateProofStatus(hasImplement, digest, isActiveReleaseFeature, persisted is not null, reasons.Count);

        IReadOnlyList<string> blockers = status switch
        {
            "missing" => [$"feature '{completion.Feature}' has no gate proof on its implement transition"],
            "present-stale" => reasons.Select(r => $"feature '{completion.Feature}' gate proof: {r}").ToList(),
            _ => [],
        };
        return (status, blockers);
    }

    /// <summary>Pure FR-036 status classification (testable without git).</summary>
    public static string ClassifyGateProofStatus(bool hasImplementStage, string? digest, bool isActiveReleaseFeature, bool proofPresent, int validationIssueCount)
    {
        if (!hasImplementStage)
        {
            return "not-required";
        }

        if (string.IsNullOrWhiteSpace(digest))
        {
            return "missing";
        }

        if (!isActiveReleaseFeature || !proofPresent)
        {
            return "present"; // attested by the recorded digest; validated at its own transition
        }

        return validationIssueCount == 0 ? "present-valid" : "present-stale";
    }

    private IReadOnlyList<string> ValidatePersistedGateProof(PersistedGateProof persisted)
    {
        var reasons = new List<string>();
        if (persisted.Proof.Outcome != StageOutcome.Pass)
        {
            reasons.Add($"outcome is {persisted.Proof.Outcome}, not Pass");
        }

        reasons.AddRange(GateProofValidator.ValidateAffectedTestProof(_repositoryRoot, persisted));
        reasons.AddRange(GateProofValidator.ValidateLadderCoverage(_repositoryRoot, persisted));
        reasons.AddRange(GateProofValidator.ValidateScope(_repositoryRoot, persisted));
        return reasons;
    }

    private static string TaskCompletionStatus(TaskCompletionResult result) =>
        result.Outcome == StageOutcome.Pass ? "pass" : "fail";
}
