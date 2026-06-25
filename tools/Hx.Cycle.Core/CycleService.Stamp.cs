using Hx.Runner.Core.Io;
using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

public sealed partial class CycleService
{
    public CycleState Stamp(string stageId, string? feature, string? baseRef, string? releaseIntent = null)
    {
        CycleStage stage = _stageModel.Find(stageId); // fail-closed on an unknown stage
        string? normalizedReleaseIntent = NormalizeReleaseIntent(stage, releaseIntent);
        RecoveryEvaluation recovery = RecoverStateIfNeeded();
        CycleState? existing = ResolveExistingForStamp(stage, feature, recovery);
        string resolvedFeature = ResolveStampFeature(feature, existing);

        EnsureNumberedFeatureSlugOnInitialStamp(stage, feature);
        existing = TransitionBeforeStamp(stage, feature, existing, normalizedReleaseIntent);
        string resolvedBaseRef = baseRef ?? existing?.BaseRef ?? GitRefs.ResolveBaseRef(_repositoryRoot);
        EnsurePrerequisitesFresh(stage);
        string? prerequisiteProofHash = CycleStageProofHasher.HashPrerequisites(existing, stage.Prereqs);
        string identity = ChangeSetIdentity.Of(_repositoryRoot, resolvedBaseRef, "HEAD");
        CycleStageProof proof = CreateStageProof(stage, resolvedFeature, identity, prerequisiteProofHash);
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
        return state;
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
        string? prerequisiteProofHash) =>
        new(
            stage.Id,
            CycleStageOutcome.Stamped,
            identity,
            ArtifactHashes(stage, resolvedFeature),
            GitRefs.TryHeadSha(_repositoryRoot),
            prerequisiteProofHash);

    private IReadOnlyList<string> ArtifactHashes(CycleStage stage, string resolvedFeature)
    {
        if (stage.Produces is not { } pattern)
        {
            return [];
        }

        string artifactPath = FreshnessEvaluator.ResolveProduces(pattern, resolvedFeature);
        string full = Path.GetFullPath(Path.Combine(_repositoryRoot, artifactPath.Replace('/', Path.DirectorySeparatorChar)));
        return File.Exists(full) ? [FileHashing.Sha256OfFile(full)] : [];
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
