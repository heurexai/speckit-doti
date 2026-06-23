using Hx.Cycle.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

public sealed partial class CycleEnforcementTests
{
    [Fact]
    public void Commit_RecordsCompletedCycle_AndRepeatedStatusCheckCommitConverge()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            PrepareDocsOnlyCycle(dir, service);

            CycleCommitResult commit = service.Commit("finish cycle");

            Assert.True(commit.Committed, string.Join("; ", commit.Reasons));
            Assert.False(commit.AlreadyCompleted);
            Assert.NotNull(commit.CommitSha);
            Assert.NotNull(commit.Completion);
            Assert.False(string.IsNullOrWhiteSpace(commit.Completion!.StagedTreeId));

            CycleStatusReport status = service.Status();
            Assert.NotNull(status.Completion);
            Assert.All(status.Freshness, f => Assert.Equal(StageFreshness.Completed, f.Freshness));

            CycleCheckReport check = service.Check("commit");
            Assert.True(check.Passed);
            Assert.NotNull(check.Completion);
            Assert.Contains(check.Prerequisites, p => p.Stage == "commit" && p.Status == "completed");

            string headAfterCommit = GitHead(dir);
            CycleCommitResult repeated = service.Commit("finish cycle");
            Assert.False(repeated.Committed);
            Assert.True(repeated.AlreadyCompleted);
            Assert.Equal(headAfterCommit, repeated.CommitSha);
            Assert.Equal(headAfterCommit, GitHead(dir));
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void Status_RecoversCompletedCycle_WhenCommitSucceededButCompletionWriteWasLost()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            PrepareDocsOnlyCycle(dir, service);
            CycleCommitResult commit = service.Commit("finish cycle");
            Assert.True(commit.Committed, string.Join("; ", commit.Reasons));

            CycleCompletionRecord completion = commit.Completion!;
            var store = new CycleStateStore(dir);
            CycleState committedState = store.Read()!;
            store.Write(committedState with
            {
                CurrentStage = completion.Stage,
                Completion = null,
                PendingCommit = new CycleCompletionIntent(
                    JsonContractDefaults.SchemaVersion,
                    completion.Feature,
                    completion.Stage,
                    completion.BaseRef,
                    completion.PreCommitHead,
                    completion.ChangeSetId,
                    completion.GateChangeSetId,
                    completion.MessageHash,
                    DateTimeOffset.UtcNow.ToString("O")),
            });

            CycleStatusReport recovered = service.Status();

            Assert.Equal(CycleRecoveryVerdict.Completed, recovered.Recovery?.Verdict);
            Assert.NotNull(recovered.Completion);
            Assert.Equal(commit.CommitSha, recovered.Completion!.CommitSha);
            CycleState recoveredState = store.Read()!;
            Assert.Null(recoveredState.PendingCommit);
            Assert.Equal(commit.CommitSha, recoveredState.Completion?.CommitSha);
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void Commit_Refuses_WhenUntrackedChangesArePresent()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            PrepareDocsOnlyCycle(dir, service);
            File.WriteAllText(Path.Combine(dir, "untracked.txt"), "not in the staged scope");

            CycleCommitResult result = service.Commit("finish cycle");

            Assert.False(result.Committed);
            Assert.Contains(result.Reasons, r => r.Contains("untracked changes present", StringComparison.Ordinal));
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void Status_ClearsPendingIntent_WhenGitCommitDidNotCreateObject()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            PrepareDocsOnlyCycle(dir, service);
            var store = new CycleStateStore(dir);
            CycleState state = store.Read()!;
            store.Write(state with { PendingCommit = PendingIntentFor(state, dir) });

            CycleStatusReport report = service.Status();

            Assert.Equal(CycleRecoveryVerdict.RetryableActive, report.Recovery?.Verdict);
            Assert.Null(store.Read()!.PendingCommit);
            Assert.Null(store.Read()!.Completion);
        }
        finally
        {
            ForceDelete(dir);
        }
    }

}
