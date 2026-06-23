using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

public sealed partial class CycleService
{
    /// <summary>The sanctioned commit path. Refuses (no commit) unless <see cref="Check"/> for <c>commit</c>
    /// passes, the persisted gate proof is present + passing + fresh, and the tree is a clean staged scope.
    /// On success, commits the staged tree with <paramref name="message"/> + a <c>Doti-Cycle</c> trailer,
    /// setting the sanctioned-commit sentinel so the insurance hook allows it.</summary>
    public CycleCommitResult Commit(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return Refuse("a commit --message is required");
        }

        RecoveryEvaluation recovery = RecoverStateIfNeeded();
        if (recovery.Report.Verdict == CycleRecoveryVerdict.Ambiguous)
        {
            return Refuse(recovery.Report.Reason ?? "commit recovery is ambiguous", recovery.Report);
        }

        CycleState? state = recovery.State;
        if (state is null)
        {
            return Refuse($"no cycle state at {CycleStateStore.RelativePath}; stamp the cycle first");
        }

        if (TryCompletedClean(state, out CycleCompletionRecord? alreadyCompleted))
        {
            return new CycleCommitResult(
                JsonContractDefaults.SchemaVersion,
                Committed: false,
                CommitSha: alreadyCompleted.CommitSha,
                Reasons: [],
                AlreadyCompleted: true,
                Completion: alreadyCompleted,
                Recovery: recovery.Report);
        }

        if (state.Completion is not null)
        {
            return Refuse(
                $"previous cycle completed at {state.Completion.CommitSha}; new edits require a new specify stamp before another sanctioned commit",
                recovery.Report);
        }

        CommitReadiness readiness = ValidateCommitReadiness(state);
        if (readiness.Reasons.Count > 0)
        {
            return new CycleCommitResult(
                JsonContractDefaults.SchemaVersion,
                false,
                null,
                readiness.Reasons,
                Recovery: recovery.Report);
        }

        PendingCycleCommit pending = PreparePendingCommit(message, state, readiness);
        ProcessRunResult commit = RunGitCommit(pending.FullMessage);
        if (commit.ExitCode != 0)
        {
            RecoveryEvaluation afterFailure = RecoverStateIfNeeded();
            string detail = string.IsNullOrWhiteSpace(commit.StandardError)
                ? commit.StandardOutput.Trim()
                : commit.StandardError.Trim();
            return Refuse($"git commit failed: {detail}", afterFailure.Report);
        }

        string commitSha = GitRefs.TryHeadSha(_repositoryRoot)
            ?? throw new InvalidOperationException("Could not resolve HEAD after commit.");
        CycleCompletionRecord completion;
        try
        {
            completion = CompletePendingCommit(pending, commitSha);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            completion = CreateCompletion(pending.Intent, commitSha);
            return new CycleCommitResult(
                JsonContractDefaults.SchemaVersion,
                true,
                commitSha,
                [$"git commit succeeded at {commitSha}, but completion state could not be persisted: {ex.Message}; run `doti cycle status` to recover the completed cycle before continuing"],
                CompletionPersistenceFailed: true,
                Completion: completion,
                Recovery: new CycleRecoveryReport(
                    CycleRecoveryVerdict.Completed,
                    $"commit created at {commitSha}; completion state repair is required",
                    completion));
        }

        return new CycleCommitResult(
            JsonContractDefaults.SchemaVersion,
            true,
            commitSha,
            [],
            Completion: completion,
            Recovery: recovery.Report);
    }

    private static CycleCommitResult Refuse(string reason, CycleRecoveryReport? recovery = null) =>
        new(JsonContractDefaults.SchemaVersion, false, null, [reason], Recovery: recovery);
}
