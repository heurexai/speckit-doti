using Hx.Runner.Core.Io;
using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

public sealed partial class CycleService
{
    public CycleState Stamp(string stageId, string? feature, string? baseRef)
    {
        CycleStage stage = _stageModel.Find(stageId); // fail-closed on an unknown stage
        RecoveryEvaluation recovery = RecoverStateIfNeeded();
        CycleState? existing = ResolveExistingForStamp(stage, feature, recovery);
        string resolvedFeature = ResolveStampFeature(feature, existing);
        string resolvedBaseRef = baseRef ?? existing?.BaseRef ?? GitRefs.ResolveBaseRef(_repositoryRoot);

        EnsurePrerequisitesFresh(stage);
        string? prerequisiteProofHash = CycleStageProofHasher.HashPrerequisites(existing, stage.Prereqs);
        string identity = ChangeSetIdentity.Of(_repositoryRoot, resolvedBaseRef, "HEAD");
        CycleStageProof proof = CreateStageProof(stage, resolvedFeature, identity, prerequisiteProofHash);
        var state = new CycleState(
            JsonContractDefaults.SchemaVersion,
            resolvedFeature,
            resolvedBaseRef,
            stage.Id,
            ReplaceStageProof(existing, stage.Id, proof));
        _store.Write(state);
        return state;
    }

    private CycleState? ResolveExistingForStamp(CycleStage stage, string? feature, RecoveryEvaluation recovery)
    {
        if (recovery.Report.Verdict == CycleRecoveryVerdict.Ambiguous)
        {
            throw new InvalidOperationException(recovery.Report.Reason);
        }

        CycleState? existing = recovery.State;
        if (existing?.Completion is null)
        {
            return existing;
        }

        if (stage.Prereqs.Count > 0)
        {
            throw new InvalidOperationException(
                $"The previous cycle completed at {existing.Completion.CommitSha}; start the next cycle with `doti cycle stamp --stage specify --feature <slug>`.");
        }

        if (string.IsNullOrWhiteSpace(feature))
        {
            throw new InvalidOperationException(
                $"The previous cycle completed at {existing.Completion.CommitSha}; pass --feature <slug> to start a new cycle.");
        }

        return null;
    }

    private static string ResolveStampFeature(string? feature, CycleState? existing) =>
        feature
            ?? existing?.Feature
            ?? throw new InvalidOperationException(
                "No feature set for the cycle; pass --feature <slug> on the first stamp (e.g. phase-14-doti-cycle-state).");

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

    private static List<CycleStageProof> ReplaceStageProof(
        CycleState? existing,
        string stageId,
        CycleStageProof proof) =>
        (existing?.Stages ?? [])
            .Where(s => !string.Equals(s.Stage, stageId, StringComparison.OrdinalIgnoreCase))
            .Append(proof)
            .ToList();
}
