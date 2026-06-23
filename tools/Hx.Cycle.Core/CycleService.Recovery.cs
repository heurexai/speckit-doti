using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

public sealed partial class CycleService
{
    private RecoveryEvaluation RecoverStateIfNeeded()
    {
        CycleState? state = _store.Read();
        if (state?.PendingCommit is not { } intent || state.Completion is not null)
        {
            return new RecoveryEvaluation(state, new CycleRecoveryReport(CycleRecoveryVerdict.None, null, state?.Completion));
        }

        string? head = GitRefs.TryHeadSha(_repositoryRoot);
        if (string.IsNullOrWhiteSpace(head))
        {
            return new RecoveryEvaluation(state, new CycleRecoveryReport(
                CycleRecoveryVerdict.Ambiguous,
                "could not resolve HEAD while recovering pending cycle commit"));
        }

        if (string.Equals(head, intent.PreCommitHead, StringComparison.Ordinal))
        {
            CycleState retryable = state with { PendingCommit = null };
            _store.Write(retryable);
            return new RecoveryEvaluation(retryable, new CycleRecoveryReport(
                CycleRecoveryVerdict.RetryableActive,
                "pending commit intent was cleared because HEAD did not move"));
        }

        if (HeadMatchesIntent(intent, head))
        {
            CycleCompletionRecord completion = CreateCompletion(intent, head);
            CycleState completed = state with
            {
                CurrentStage = "commit",
                PendingCommit = null,
                Completion = completion,
            };
            _store.Write(completed);
            return new RecoveryEvaluation(completed, new CycleRecoveryReport(
                CycleRecoveryVerdict.Completed,
                $"recovered completed cycle at commit {head}",
                completion));
        }

        return new RecoveryEvaluation(state, new CycleRecoveryReport(
            CycleRecoveryVerdict.Ambiguous,
            "ambiguous commit recovery: HEAD moved while a doti commit intent was pending, but the current commit does not match the recorded doti trailers"));
    }

    private bool TryCompletedClean(CycleState state, out CycleCompletionRecord completion)
    {
        if (state.Completion is null)
        {
            completion = null!;
            return false;
        }

        completion = state.Completion;
        string? head = GitRefs.TryHeadSha(_repositoryRoot);
        return string.Equals(head, state.Completion.CommitSha, StringComparison.Ordinal)
            && IsWorkingTreeClean();
    }

    private static CycleCompletionRecord CreateCompletion(CycleCompletionIntent intent, string commitSha) =>
        new(
            JsonContractDefaults.SchemaVersion,
            intent.Feature,
            intent.Stage,
            intent.BaseRef,
            intent.PreCommitHead,
            commitSha,
            intent.ChangeSetId,
            intent.GateChangeSetId,
            intent.MessageHash,
            DateTimeOffset.UtcNow.ToString("O"),
            intent.StagedTreeId,
            intent.StageProofHashes,
            intent.GateProofDigest,
            intent.RunnerIdentity,
            intent.ExpectedCompletionShape);

    private bool HeadMatchesIntent(CycleCompletionIntent intent, string head)
    {
        ProcessRunResult parent = ProcessRunner.Run(new ToolCommand("git", ["rev-parse", $"{head}^"], _repositoryRoot));
        if (parent.ExitCode != 0 || !string.Equals(parent.StandardOutput.Trim(), intent.PreCommitHead, StringComparison.Ordinal))
        {
            return false;
        }

        ProcessRunResult message = ProcessRunner.Run(new ToolCommand("git", ["log", "-1", "--format=%B", head], _repositoryRoot));
        if (message.ExitCode != 0)
        {
            return false;
        }

        string body = message.StandardOutput;
        return body.Contains($"Doti-Cycle: {intent.Feature}/{intent.Stage}", StringComparison.Ordinal)
            && body.Contains($"Doti-ChangeSet: {intent.ChangeSetId}", StringComparison.Ordinal)
            && body.Contains($"Doti-Message: {intent.MessageHash}", StringComparison.Ordinal)
            && (string.IsNullOrWhiteSpace(intent.StagedTreeId)
                || body.Contains($"Doti-StagedTree: {intent.StagedTreeId}", StringComparison.Ordinal))
            && (string.IsNullOrWhiteSpace(intent.GateProofDigest)
                || body.Contains($"Doti-GateProof: {intent.GateProofDigest}", StringComparison.Ordinal))
            && (string.IsNullOrWhiteSpace(intent.RunnerIdentity)
                || body.Contains($"Doti-Runner: {intent.RunnerIdentity}", StringComparison.Ordinal));
    }

    private bool IsWorkingTreeClean()
    {
        ProcessRunResult status = ProcessRunner.Run(
            new ToolCommand("git", ["status", "--porcelain=v1", "-z", "--untracked-files=all"], _repositoryRoot));
        if (status.ExitCode != 0)
        {
            return false;
        }

        return string.IsNullOrEmpty(status.StandardOutput);
    }

    private sealed record RecoveryEvaluation(CycleState? State, CycleRecoveryReport Report);
}
