using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

public sealed partial class CycleService
{
    private CycleState RebaseStateAfterTransition(CycleState state, CycleTransitionRecord transition)
    {
        IReadOnlyList<CycleStageProof> proofs = RebaseProofsToHead(state.Stages, transition.CommitSha, state.Feature);
        return state with
        {
            BaseRef = transition.CommitSha,
            CurrentStage = transition.NextStage,
            PendingCommit = null,
            Completion = null,
            Stages = proofs,
            Transitions = Append(state.Transitions, transition),
        };
    }

    private IReadOnlyList<CycleStageProof> RebaseProofsToHead(
        IReadOnlyList<CycleStageProof> proofs,
        string newBaseRef,
        string feature)
    {
        string rebasedIdentity = StageChangeSetIdentity(newBaseRef, feature);
        var rebased = new List<CycleStageProof>();
        foreach (CycleStageProof proof in proofs)
        {
            CycleStage stage = _stageModel.Find(proof.Stage);
            string? prereqHash = CycleStageProofHasher.HashPrerequisites(
                new CycleState(JsonContractDefaults.SchemaVersion, feature, newBaseRef, stage.Id, rebased),
                stage.Prereqs);
            rebased.Add(proof with
            {
                ChangeSetId = rebasedIdentity,
                ArtifactHashes = ArtifactHashes(stage, feature),
                StampedAtCommit = newBaseRef,
                PrerequisiteProofHash = prereqHash,
            });
        }

        return rebased;
    }

    private static CycleTransitionRecord TransitionFromIntent(CycleCompletionIntent intent, string commitSha) =>
        new(
            JsonContractDefaults.SchemaVersion,
            intent.Feature,
            intent.Stage,
            intent.NextStage ?? intent.Stage,
            intent.PreCommitHead,
            commitSha,
            intent.ChangeSetId,
            intent.MessageHash,
            DateTimeOffset.UtcNow.ToString("O"),
            intent.StagedTreeId,
            intent.StageProofHashes,
            intent.GateProofDigest,
            intent.RunnerIdentity);

    private static CycleCompletionRecord CompletionFromTransition(CycleTransitionRecord transition, CycleState state) =>
        new(
            JsonContractDefaults.SchemaVersion,
            transition.Feature,
            transition.Stage,
            state.BaseRef,
            transition.PreCommitHead,
            transition.CommitSha,
            transition.ChangeSetId,
            transition.ChangeSetId,
            transition.MessageHash,
            transition.CompletedAtUtc,
            transition.StagedTreeId,
            transition.StageProofHashes,
            transition.GateProofDigest,
            transition.RunnerIdentity,
            TransitionShape,
            transition.NextStage);

    private static IReadOnlyList<T> Append<T>(IReadOnlyList<T>? source, T value) =>
        (source ?? []).Concat([value]).ToArray();
}
