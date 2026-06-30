using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

public sealed partial class CycleService
{
    // 027 FR-006: re-entrancy guard for the on-stamp auto-cascade. A successful Stamp auto-rebinds its
    // content-equal dependents by re-running the safe refresh; those nested Stamp calls must NOT themselves
    // trigger another cascade (Stamp -> Refresh -> Stamp -> Refresh ...). [ThreadStatic] so a parallel test's
    // service instance is unaffected; the field is per-thread ambient state, reset in a finally.
    [ThreadStatic]
    private static bool _cascadeInProgress;

    public CycleState Stamp(string stageId, string? feature, string? baseRef, string? releaseIntent = null)
    {
        CycleStage stage = _stageModel.Find(stageId); // fail-closed on an unknown stage
        string? normalizedReleaseIntent = NormalizeReleaseIntent(stage, releaseIntent);
        var recovery = RecoverStateIfNeeded();
        CycleState? existing = ResolveExistingForStamp(stage, feature, recovery);
        string resolvedFeature = ResolveStampFeature(feature, existing);

        EnsureNumberedFeatureSlugOnInitialStamp(stage, feature);
        existing = TransitionBeforeStamp(stage, feature, existing, normalizedReleaseIntent);
        string resolvedBaseRef = baseRef ?? existing?.BaseRef ?? GitRefs.ResolveBaseRef(_repositoryRoot);
        // 028 FR-004/B1: the in-Stamp eligibility fence. A bare `doti cycle stamp` must not silently clear an
        // ATTESTABLE stale of the target (own artifact unchanged, only prerequisite content diverged) — that is the
        // exact agent rubber-stamp this cycle closes. Evaluate the target's OWN freshness (EnsurePrerequisitesFresh
        // below only checks its prerequisites) and refuse, routing to the recorded `review-rebind` verb.
        RefuseBareStampOnAttestableStale(stage, existing, resolvedBaseRef, resolvedFeature);
        EnsurePrerequisitesFresh(stage);
        string? prerequisiteProofHash = CycleStageProofHasher.HashPrerequisites(existing, stage.Prereqs);
        IReadOnlyList<string> prerequisiteArtifactHashes =
            CanonicalArtifactHasher.PrerequisiteArtifactHashes(_repositoryRoot, _stageModel, stage.Id, resolvedFeature);
        string identity = StageChangeSetIdentity(resolvedBaseRef, resolvedFeature);
        CycleStageProof proof = CreateStageProof(
            stage, resolvedFeature, identity, prerequisiteProofHash, prerequisiteArtifactHashes);
        var state = new CycleState(
            JsonContractDefaults.SchemaVersion,
            resolvedFeature,
            resolvedBaseRef,
            CurrentStageAfterStamp(existing, stage),
            ReplaceStageProof(existing, stage.Id, proof),
            Transitions: existing?.Transitions,
            CompletedUnreleasedCycles: existing?.CompletedUnreleasedCycles,
            ReleasedCycles: existing?.ReleasedCycles);
        _store.Write(state);
        CascadeSafeRebindAfterStamp(stage);
        return state;
    }

    /// <summary>
    /// 027 FR-006: after a stage is stamped, auto-rebind its content-equal dependents so re-running the ONE
    /// genuinely-changed stage settles the rest with zero manual stamps. The cascade is the SAME safe refresh the
    /// chokepoint already computes (one projection, never a second evaluator), bounded to the stamped stage's
    /// dependents (the most-downstream dependent's prerequisite closure is exactly the stamped stage + everything
    /// between), prerequisite-first, and re-entrancy-guarded (its own nested Stamp calls do not re-cascade). The
    /// planner gate guarantees ONLY <see cref="RestampSafety.ReBindContentEqual"/> / <see cref="RestampSafety.SafeReinterpret"/>
    /// steps are stamped — a RerunRequired / ChangeSetDiffers / inserted-stage step is never auto-stamped. A cascade
    /// failure is isolated: it never fails or rolls back the primary stamp.
    /// </summary>
    private void CascadeSafeRebindAfterStamp(CycleStage stamped)
    {
        if (_cascadeInProgress)
        {
            return; // a nested stamp issued BY the cascade itself — do not recurse.
        }

        CycleStage? deepestDependent = MostDownstreamDependent(stamped);
        if (deepestDependent is null)
        {
            return; // no stage depends on the stamped stage — nothing to cascade.
        }

        _cascadeInProgress = true;
        try
        {
            // Refresh --apply-safe is the deterministic projection: it stamps only planner-safe steps
            // (ReBindContentEqual gated by all-upstreams-Fresh + not-review-kind; SafeReinterpret migrations),
            // prereq-first, re-deriving after each so a chain settles, and terminates.
            Refresh(deepestDependent.Id, applySafe: true);
        }
        catch (Exception)
        {
            // FR-006: a cascade failure must never fail or roll back the primary stamp. The primary stamp is
            // already persisted; any un-rebound dependent simply re-surfaces as stale on the next check.
        }
        finally
        {
            _cascadeInProgress = false;
        }
    }

    /// <summary>The most-downstream stage whose transitive prerequisite closure contains <paramref name="stamped"/>
    /// (the last-declared dependent). Refreshing it bounds the cascade to the stamped stage's dependents. Null when
    /// no stage depends on the stamped stage.</summary>
    private CycleStage? MostDownstreamDependent(CycleStage stamped)
    {
        CycleStage? deepest = null;
        int deepestOrdinal = -1;
        for (int i = 0; i < _stageModel.Stages.Count; i++)
        {
            CycleStage candidate = _stageModel.Stages[i];
            bool dependsOnStamped = _stageModel.TransitivePrereqStages(candidate.Id)
                .Any(p => string.Equals(p.Id, stamped.Id, StringComparison.OrdinalIgnoreCase));
            if (dependsOnStamped && i > deepestOrdinal)
            {
                deepest = candidate;
                deepestOrdinal = i;
            }
        }

        return deepest;
    }

    private static string? NormalizeReleaseIntent(CycleStage stage, string? releaseIntent)
    {
        if (string.IsNullOrWhiteSpace(releaseIntent))
        {
            return null;
        }

        if (!string.Equals(stage.Id, "release", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("--release-intent is only valid when stamping the release stage.");
        }

        string normalized = releaseIntent.Trim().ToLowerInvariant();
        return normalized is "major" or "minor" or "patch"
            ? normalized
            : throw new InvalidOperationException("--release-intent must be major, minor, or patch.");
    }

    private CycleState? ResolveExistingForStamp(CycleStage stage, string? feature, RecoveryEvaluation recovery)
    {
        if (recovery.Report.Verdict == CycleRecoveryVerdict.Ambiguous)
        {
            throw new InvalidOperationException(recovery.Report.Reason);
        }

        CycleState? existing = recovery.State;

        // A previous cycle is concluded either by a recorded Completion (the clean drift-review handoff) or
        // by having been released. MarkReleaseTrainReleased moves the active feature into ReleasedCycles
        // WITHOUT setting Completion and leaves CurrentStage = "release"; recognizing the released feature
        // here is what lets the next feature start. Without it the cycle wedges after every release — there
        // is no path from (CurrentStage = "release", Completion = null) to a new feature.
        if (existing?.Completion is null && !IsCurrentFeatureReleased(existing))
        {
            if (existing is not null
                && stage.Prereqs.Count == 0
                && !string.IsNullOrWhiteSpace(feature)
                && !string.Equals(existing.Feature, feature, StringComparison.OrdinalIgnoreCase)
                && !(string.Equals(existing.CurrentStage, stage.Id, StringComparison.OrdinalIgnoreCase)
                    && !CycleFeatureSlug.IsNumbered(existing.Feature)
                    && CycleFeatureSlug.IsNumbered(feature))
                && !string.Equals(existing.CurrentStage, "drift-review", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Cannot start feature '{feature}' while feature '{existing.Feature}' is at stage '{existing.CurrentStage}'. Complete drift-review before beginning another specification.");
            }

            return existing;
        }

        string concludedAt = existing!.Completion?.CommitSha
            ?? ReleasedCommitShaForCurrentFeature(existing)
            ?? existing.CurrentStage;

        if (stage.Prereqs.Count > 0)
        {
            throw new InvalidOperationException(
                $"The previous cycle completed at {concludedAt}; start the next cycle with `doti cycle stamp --stage specify --feature <NNN-slug>`.");
        }

        if (string.IsNullOrWhiteSpace(feature))
        {
            throw new InvalidOperationException(
                $"The previous cycle completed at {concludedAt}; pass --feature <NNN-slug> to start a new cycle.");
        }

        return null;
    }

    private static bool IsCurrentFeatureReleased(CycleState? existing) =>
        existing is not null
        && (existing.ReleasedCycles ?? []).Any(c =>
            string.Equals(c.Feature, existing.Feature, StringComparison.OrdinalIgnoreCase));

    private static string? ReleasedCommitShaForCurrentFeature(CycleState existing) =>
        (existing.ReleasedCycles ?? [])
            .LastOrDefault(c => string.Equals(c.Feature, existing.Feature, StringComparison.OrdinalIgnoreCase))
            ?.CommitSha;

    private static string ResolveStampFeature(string? feature, CycleState? existing) =>
        feature
            ?? existing?.Feature
            ?? throw new InvalidOperationException(
                "No feature set for the cycle; pass --feature <NNN-slug> on the first stamp (e.g. 001-doti-cycle-state).");

    private static void EnsureNumberedFeatureSlugOnInitialStamp(CycleStage stage, string? suppliedFeature)
    {
        if (stage.Prereqs.Count > 0 || string.IsNullOrWhiteSpace(suppliedFeature))
        {
            return;
        }

        if (!CycleFeatureSlug.IsNumbered(suppliedFeature))
        {
            throw new CycleInputException(CycleFeatureSlug.NumberedSlugRequiredMessage(suppliedFeature));
        }
    }

    /// <summary>
    /// 028 FR-004 / B1: the in-Stamp eligibility fence. If an EXISTING proof for the target stage is stale in the
    /// ATTESTABLE way (own artifact content unchanged, only a prerequisite artifact's content diverged, and the stage
    /// is non-review + non-change-set-bound — <see cref="ReviewRebindEligibility.IsAttestable"/>), a bare
    /// <c>doti cycle stamp</c> would clear the flag with no impact assessment and no record — the agent rubber-stamp.
    /// Refuse it, throwing a <see cref="CycleInputException"/> routed to <c>Validation_CycleReviewRebindRequiresAttest</c>
    /// and directing the agent to <c>doti cycle review-rebind --attest no-impact</c>. A real re-author (own-artifact
    /// hash changed) is NOT attestable, so it stamps normally; a first stamp (no existing proof) is never fenced.
    /// </summary>
    private void RefuseBareStampOnAttestableStale(
        CycleStage stage, CycleState? existing, string resolvedBaseRef, string resolvedFeature)
    {
        CycleStageProof? targetProof = existing?.Stages.FirstOrDefault(
            s => string.Equals(s.Stage, stage.Id, StringComparison.OrdinalIgnoreCase));
        if (targetProof is null)
        {
            return; // first stamp of this stage — nothing to clear, never fenced.
        }

        bool requiresChangeSetIdentity = RequiresChangeSetIdentity(stage.Id);
        string identity = StageChangeSetIdentity(resolvedBaseRef, resolvedFeature);
        var evaluator = new FreshnessEvaluator(_repositoryRoot, _stageModel);
        StageFreshnessResult freshness = evaluator.Evaluate(
            targetProof, resolvedFeature, identity, requiresChangeSetIdentity);

        if (ReviewRebindEligibility.IsAttestable(freshness, stage, requiresChangeSetIdentity))
        {
            throw new CycleReviewRebindRequiredException(
                $"Stage '{stage.Id}' is stale only because a prerequisite artifact's content changed; a bare stamp cannot "
                + "clear it. Read the surfaced upstream diff, then either re-author the stage or record a reviewed-no-impact "
                + $"verdict: `doti cycle review-rebind --target {stage.Id} --attest no-impact`.",
                stage.Id);
        }
    }

    private void EnsurePrerequisitesFresh(CycleStage stage)
    {
        if (stage.Prereqs.Count == 0)
        {
            return;
        }

        CycleCheckReport prereqCheck = Check(stage.Id);
        if (prereqCheck.Passed)
        {
            return;
        }

        string summary = string.Join("; ", prereqCheck.Prerequisites
            .Where(p => !p.Ok)
            .Select(p => $"{p.Stage}: {p.Status}" + (p.Reason is { } r ? $" ({r})" : "")));
        throw new InvalidOperationException(
            $"Cannot stamp stage '{stage.Id}' because its prerequisites are not all fresh: {summary}");
    }

    private CycleStageProof CreateStageProof(
        CycleStage stage,
        string resolvedFeature,
        string identity,
        string? prerequisiteProofHash,
        IReadOnlyList<string> prerequisiteArtifactHashes) =>
        new(
            stage.Id,
            CycleStageOutcome.Stamped,
            identity,
            ArtifactHashes(stage, resolvedFeature),
            GitRefs.TryHeadSha(_repositoryRoot),
            prerequisiteProofHash,
            prerequisiteArtifactHashes,
            StageGraphFingerprint(stage));

    /// <summary>027 FR-010: the ordered transitive prerequisite STAGE-ID set the proof is stamped against
    /// (declaration order — deterministic). Records the stage GRAPH the proof was bound to, so an edge/reorder is
    /// distinguishable from a content change and detectable across a stage-model migration.</summary>
    private IReadOnlyList<string> StageGraphFingerprint(CycleStage stage) =>
        _stageModel.TransitivePrereqStages(stage.Id).Select(s => s.Id).ToList();

    private IReadOnlyList<string> ArtifactHashes(CycleStage stage, string resolvedFeature)
    {
        if (stage.Produces is not { } pattern)
        {
            return [];
        }

        string artifactPath = StageModel.ResolveProduces(pattern, resolvedFeature);
        string full = Path.GetFullPath(Path.Combine(_repositoryRoot, artifactPath.Replace('/', Path.DirectorySeparatorChar)));
        // Canonical (FR-027): EOL/checkbox/hash-marker-insensitive, so checking task boxes during
        // /07-implement does not stale the doc stage that produced the tasks file.
        return File.Exists(full) ? [CanonicalArtifactHasher.CanonicalHashOfFile(full)] : [];
    }

    private string CurrentStageAfterStamp(CycleState? existing, CycleStage stampedStage)
    {
        if (existing is null)
        {
            return stampedStage.Id;
        }

        int existingOrdinal = StageOrdinal(existing.CurrentStage);
        int stampedOrdinal = StageOrdinal(stampedStage.Id);
        return stampedOrdinal < existingOrdinal ? existing.CurrentStage : stampedStage.Id;
    }

    private int StageOrdinal(string stageId)
    {
        for (int i = 0; i < _stageModel.Stages.Count; i++)
        {
            if (string.Equals(_stageModel.Stages[i].Id, stageId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    private static List<CycleStageProof> ReplaceStageProof(
        CycleState? existing,
        string stageId,
        CycleStageProof proof) =>
        (existing?.Stages ?? [])
            .Where(s => !string.Equals(s.Stage, stageId, StringComparison.OrdinalIgnoreCase))
            .Append(proof)
            .ToList();
}
