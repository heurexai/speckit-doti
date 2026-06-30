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
        var featureMembers = completions
            .Select(completion => FeatureForCompletion(state, completion))
            .ToArray();

        // 030 (bug-release-bridge): a test-passed /doti-bug mini-cycle is ALSO a releasable member — a bug-fix-only
        // repo releases (patch). The records live under Hx.Doti.Core; the provider was injected so Cycle.Core stays
        // acyclic. A not-yet-test-passed bug cycle is NOT a member here (fail-closed in the provider).
        IReadOnlyList<CycleReleaseTrainFeature> bugMembers = _bugReleaseMembers(_repositoryRoot);
        var members = featureMembers.Concat(bugMembers).ToArray();

        var blockers = new List<string>();
        if (members.Length == 0)
        {
            blockers.Add("no completed unreleased feature cycles are ready for release");
        }

        blockers.AddRange(members.SelectMany(member => member.Blockers));

        // FR-037/SC-019: cross-feature release-train drift (a later feature changed paths an earlier feature owns).
        // Scoped to FEATURE members only — a bug mini-cycle owns no feature stage artifacts (no docs/specs ownership),
        // so it never participates in the pairwise owned-path drift scan.
        IReadOnlyList<ReleaseTrainDriftFinding> driftFindings =
            new ReleaseTrainDriftDetector().Detect(_repositoryRoot, _stageModel, featureMembers);
        blockers.AddRange(driftFindings.Select(finding => finding.Reason));

        return new CycleReleaseTrain(
            JsonContractDefaults.SchemaVersion,
            blockers.Count == 0,
            members,
            blockers,
            driftFindings.Count > 0 ? driftFindings : null);
    }

    private IReadOnlyList<CycleCompletionRecord> CompletionRecordsForRelease(CycleState state)
    {
        var completions = new List<CycleCompletionRecord>();
        completions.AddRange(state.CompletedUnreleasedCycles ?? []);

        // 030 (bug-release-bridge), PART A: the active feature counts as a releasable member at BOTH terminal points —
        // already at release (via the drift-review→release transition) AND parked at drift-review (its
        // implement→drift-review transition). The latter is the single-feature-cycle / no-prior-anchor case that
        // previously yielded an empty, invalid train. Same dedup guard against an already-finalized completed cycle.
        CycleCompletionRecord? active =
            string.Equals(state.CurrentStage, "release", StringComparison.OrdinalIgnoreCase)
                ? CompletionForActiveReleaseFeature(state)
                : string.Equals(state.CurrentStage, "drift-review", StringComparison.OrdinalIgnoreCase)
                    ? CompletionForActiveDriftReviewFeature(state)
                    : null;
        if (active is { } member
            && completions.All(c => !string.Equals(c.Feature, member.Feature, StringComparison.OrdinalIgnoreCase)))
        {
            completions.Add(member);
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

    /// <summary>
    /// 030 (bug-release-bridge), PART A: the active feature parked at drift-review, anchored on its LAST
    /// implement→drift-review transition. The completion's stage is overridden to <c>drift-review</c> so
    /// <see cref="FeatureForCompletion"/>'s stage check passes (the feature completed drift-review even though it has
    /// not yet transitioned into release). BaseRef is the transition's pre-commit head, mirroring
    /// <see cref="CompletionForActiveReleaseFeature"/> / <see cref="CompletionFromTransition"/>.
    /// </summary>
    private CycleCompletionRecord? CompletionForActiveDriftReviewFeature(CycleState state)
    {
        CycleTransitionRecord? transition = (state.Transitions ?? [])
            .LastOrDefault(t =>
                string.Equals(t.Feature, state.Feature, StringComparison.OrdinalIgnoreCase)
                && string.Equals(t.Stage, "implement", StringComparison.OrdinalIgnoreCase)
                && string.Equals(t.NextStage, "drift-review", StringComparison.OrdinalIgnoreCase));
        if (transition is null)
        {
            return null;
        }

        return CompletionFromTransition(transition, state with { BaseRef = transition.PreCommitHead })
            with { Stage = "drift-review" };
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
        // 030 (bug-release-bridge), PART A: the LIVE gate proof is re-validated for the active feature at BOTH its
        // release terminus AND while parked at drift-review (the proof was minted at the implement→drift-review
        // transition and is current there). A stale/failed live proof must still block in either state; an earlier,
        // already-finalized feature stays digest-attested. (`isActiveReleaseFeature` keeps its name in the pure
        // classifier's signature for its existing tests; here it denotes "the feature being released NOW".)
        bool isActiveReleaseFeature =
            string.Equals(completion.Feature, state.Feature, StringComparison.OrdinalIgnoreCase)
            && (string.Equals(state.CurrentStage, "release", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state.CurrentStage, "drift-review", StringComparison.OrdinalIgnoreCase));

        PersistedGateProof? persisted = hasImplement && !string.IsNullOrWhiteSpace(digest) && isActiveReleaseFeature
            ? new GateProofStore(_repositoryRoot).Read()
            : null;

        // 023-bug fix: re-validate the active feature's proof against the feature's OWN release HEAD, not live HEAD, so
        // an unrelated commit landing on top (e.g. a separate bug fix on the same branch) cannot falsely invalidate an
        // unchanged feature's gate proof. The bound is the LATEST transition INTO release (a `release→release` re-stamp
        // wins over the original `drift-review→release`, since the proof was minted against that later commit) — NOT
        // `completion.CommitSha`, which is pinned to the drift-review→release transition and misses re-stamps. Earlier
        // features are already attested by their recorded digest above.
        string? featureReleaseHead = (state.Transitions ?? [])
            .Where(t => string.Equals(t.Feature, completion.Feature, StringComparison.OrdinalIgnoreCase)
                && string.Equals(t.NextStage, "release", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(t.CommitSha))
            .Select(t => t.CommitSha)
            .LastOrDefault();

        // 030 (bug-release-bridge): the active feature PARKED AT drift-review has no →release transition yet, so
        // `featureReleaseHead` is null and `completion.CommitSha` is the FROZEN implement→drift-review transition
        // commit. But its live gate proof was RE-BOUND to the current cycle HEAD by the drift-review re-stamp — a bug
        // fix or doc commit legitimately landing on top grows the change set and is re-gated, then re-stamped. Validate
        // that live proof against the current HEAD where it was actually minted, not the stale transition commit, or
        // the re-planned affected-test scope diverges from the proof ("planner hash does not match the change set").
        bool isActiveDriftReview =
            string.Equals(completion.Feature, state.Feature, StringComparison.OrdinalIgnoreCase)
            && string.Equals(state.CurrentStage, "drift-review", StringComparison.OrdinalIgnoreCase);
        string? validationHead = featureReleaseHead
            ?? (isActiveDriftReview ? GitRefs.TryHeadSha(_repositoryRoot) : completion.CommitSha);
        IReadOnlyList<string> reasons = persisted is null
            ? []
            : ValidatePersistedGateProof(persisted, validationHead);
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

    private IReadOnlyList<string> ValidatePersistedGateProof(PersistedGateProof persisted, string? headRef = null)
    {
        var reasons = new List<string>();
        if (persisted.Proof.Outcome != StageOutcome.Pass)
        {
            reasons.Add($"outcome is {persisted.Proof.Outcome}, not Pass");
        }

        reasons.AddRange(GateProofValidator.ValidateAffectedTestProof(_repositoryRoot, persisted, headRef));
        reasons.AddRange(GateProofValidator.ValidateLadderCoverage(_repositoryRoot, persisted));
        reasons.AddRange(GateProofValidator.ValidateScope(_repositoryRoot, persisted, headRef));
        return reasons;
    }

    private static string TaskCompletionStatus(TaskCompletionResult result) =>
        result.Outcome == StageOutcome.Pass ? "pass" : "fail";
}
