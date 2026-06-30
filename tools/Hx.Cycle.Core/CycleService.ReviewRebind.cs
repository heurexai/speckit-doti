using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

public sealed partial class CycleService
{
    /// <summary>The only attestation verdict the verb accepts today (FR-003).</summary>
    public const string NoImpactAttestation = "no-impact";

    /// <summary>028 FR-002: the commit a stamped stage was bound to (<see cref="CycleStageProof.StampedAtCommit"/>), or
    /// null (unborn/detached HEAD, or an unstamped stage). The CLI/recovery seam uses it to compute the line-level
    /// <c>git diff &lt;StampedAtCommit&gt;..HEAD</c> of the changed prerequisite paths, falling back to a worktree diff
    /// when null. Read-only accessor — keeps git out of the pure freshness leaf (H3).</summary>
    public string? StampedAtCommitOf(string stage)
    {
        CycleState? state = _store.Read();
        return state?.Stages
            .FirstOrDefault(s => string.Equals(s.Stage, stage, StringComparison.OrdinalIgnoreCase))
            ?.StampedAtCommit;
    }

    /// <summary>
    /// 028 FR-003/FR-005 (B1/B2/B7): the agent-gated reviewed-no-impact rebind. After the agent reads the surfaced
    /// upstream diff and judges the change does not affect <paramref name="target"/>, it records that verdict here. The
    /// verb evaluates the TARGET's OWN freshness directly via <see cref="FreshnessEvaluator.Evaluate(CycleStageProof,
    /// string, string, bool)"/> (NOT <see cref="Check(string)"/>, which only sees the target's prerequisites and never
    /// the target's own <see cref="StaleReason.PrereqArtifactChanged"/>), then runs the pure
    /// <see cref="ReviewRebindEligibility.IsAttestable"/> fence on that single result. If attestable and
    /// <paramref name="attest"/> is <see cref="NoImpactAttestation"/>, it content-rebinds the target's
    /// <see cref="CycleStageProof.PrerequisiteArtifactHashes"/> to the current upstream content, marks the proof
    /// <see cref="CycleStageOutcome.ReviewedNoImpactRebound"/>, appends an immutable
    /// <see cref="CycleReviewedRebindRecord"/> to <see cref="CycleState.ReviewedRebinds"/>, and persists proof + record
    /// in ONE <see cref="CycleStateStore.Write"/> (SC-007). The on-stamp cascade is SUPPRESSED (B7): only the target is
    /// rebound; each further-downstream stage surfaces for its own attestation. Decay comes for free — the rebound
    /// proof is an ordinary content-bound proof, so a later upstream edit re-stales it (SC-001/SC-003).
    /// </summary>
    public CycleState ReviewRebind(string target, string attest, string? reason)
    {
        CycleStage stage = _stageModel.Find(target); // fail-closed on an unknown stage
        if (!string.Equals(attest, NoImpactAttestation, StringComparison.OrdinalIgnoreCase))
        {
            throw new CycleInputException(
                $"--attest must be '{NoImpactAttestation}' (the only reviewed-rebind verdict); got '{attest}'.");
        }

        var recovery = RecoverStateIfNeeded();
        CycleState state = recovery.State
            ?? throw new CycleReviewRebindIneligibleException(
                $"No cycle state at {CycleStateStore.RelativePath}; there is no '{target}' proof to review-rebind.",
                ReviewRebindRefusal.NotStale);

        CycleStageProof targetProof = state.Stages.FirstOrDefault(
            s => string.Equals(s.Stage, stage.Id, StringComparison.OrdinalIgnoreCase))
            ?? throw new CycleReviewRebindIneligibleException(
                $"Stage '{stage.Id}' is not stamped; there is no proof to review-rebind.",
                ReviewRebindRefusal.NotStale);

        bool requiresChangeSetIdentity = RequiresChangeSetIdentity(stage.Id);
        string identity = FreshnessIdentity(state);
        var evaluator = new FreshnessEvaluator(_repositoryRoot, _stageModel);
        StageFreshnessResult freshness = evaluator.Evaluate(
            targetProof, state.Feature, identity, requiresChangeSetIdentity);

        EnsureAttestable(stage, freshness, requiresChangeSetIdentity);

        return RebindReviewed(state, stage, targetProof, freshness, reason);
    }

    private static void EnsureAttestable(
        CycleStage stage, StageFreshnessResult freshness, bool requiresChangeSetIdentity)
    {
        if (freshness.Freshness != StageFreshness.Stale)
        {
            throw new CycleReviewRebindIneligibleException(
                $"Stage '{stage.Id}' is {freshness.Freshness.ToString().ToLowerInvariant()}, not stale; "
                + "there is nothing to attest.",
                ReviewRebindRefusal.NotStale);
        }

        if (!ReviewRebindEligibility.IsAttestable(freshness, stage, requiresChangeSetIdentity))
        {
            throw new CycleReviewRebindIneligibleException(
                $"Stage '{stage.Id}' is stale ({freshness.StaleReason}) but not eligible for a reviewed-no-impact "
                + "rebind: only a doc/non-review, non-change-set-bound stage stale solely on a prerequisite content "
                + "change can be attested. Re-run the stage instead.",
                ReviewRebindRefusal.Ineligible);
        }
    }

    private CycleState RebindReviewed(
        CycleState state,
        CycleStage stage,
        CycleStageProof targetProof,
        StageFreshnessResult freshness,
        string? reason)
    {
        IReadOnlyList<string> before = targetProof.PrerequisiteArtifactHashes ?? [];
        IReadOnlyList<string> after =
            CanonicalArtifactHasher.PrerequisiteArtifactHashes(_repositoryRoot, _stageModel, stage.Id, state.Feature);

        CycleStageProof rebound = targetProof with
        {
            Outcome = CycleStageOutcome.ReviewedNoImpactRebound,
            PrerequisiteArtifactHashes = after,
        };

        var record = new CycleReviewedRebindRecord(
            JsonContractDefaults.SchemaVersion,
            stage.Id,
            ChangedUpstreamStages(stage, freshness, state.Feature),
            before,
            after,
            NoImpactAttestation,
            string.IsNullOrWhiteSpace(reason) ? null : reason!.Trim(),
            DateTimeOffset.UtcNow.ToString("O"));

        // B7: one atomic whole-state write — the rebound proof AND its audit record together; the cascade is
        // SUPPRESSED (this is not Stamp; only the target is rebound, each downstream surfaces for its own attestation).
        var next = state with
        {
            Stages = ReplaceStageProof(state, stage.Id, rebound),
            ReviewedRebinds = Append(state.ReviewedRebinds, record),
        };
        _store.Write(next);
        return next;
    }

    /// <summary>028 FR-005: the changed upstream STAGE ids — the prerequisite stages whose produced artifact path is in
    /// the changed-path set the freshness evaluator surfaced. Maps the changed file paths back to producing stages so
    /// the record names the upstream stage(s), not just files.</summary>
    private IReadOnlyList<string> ChangedUpstreamStages(
        CycleStage stage, StageFreshnessResult freshness, string feature)
    {
        IReadOnlyList<string> changedPaths = freshness.ChangedPrereqPaths ?? [];
        if (changedPaths.Count == 0)
        {
            return [];
        }

        var changed = new HashSet<string>(changedPaths, StringComparer.Ordinal);
        var stages = new List<string>();
        foreach (CycleStage prereq in _stageModel.TransitivePrereqStages(stage.Id))
        {
            if (prereq.Produces is not { } pattern)
            {
                continue;
            }

            string path = StageModel.ResolveProduces(pattern, feature);
            if (changed.Contains(path) && !stages.Contains(prereq.Id))
            {
                stages.Add(prereq.Id);
            }
        }

        return stages;
    }
}
