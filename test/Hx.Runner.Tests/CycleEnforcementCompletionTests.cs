using Hx.Cycle.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

public sealed partial class CycleEnforcementTests
{
    [Fact]
    public void Stamp_NextStage_CreatesTransitionCommitAndAdvancesBaseline()
    {
        string dir = InitRepo();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
            File.WriteAllText(Path.Combine(dir, "docs", "specs", "001-f.md"), "spec body");
            Git(dir, "add", "docs/specs/001-f.md");

            var service = new CycleService(dir);
            service.Stamp("specify", "001-f", null);
            string before = GitHead(dir);

            CycleState state = service.Stamp("drift-review", null, null);

            string after = GitHead(dir);
            Assert.NotEqual(before, after);
            Assert.Equal(after, state.BaseRef);
            CycleTransitionRecord transition = Assert.Single(state.Transitions!);
            Assert.Equal("specify", transition.Stage);
            Assert.Equal("drift-review", transition.NextStage);
            Assert.Equal(after, transition.CommitSha);
            Assert.Equal($"specify: 001-f", GitLogSubject(dir, after));
            Assert.Contains("Doti-Transition: 001-f/specify->drift-review", GitLogBody(dir, after));
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void Stamp_ReviewStageTransition_AllowsNoFileChangeCommit()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            PrepareDocsOnlyCycle(dir, service);
            string before = GitHead(dir);

            CycleState state = service.Stamp("release", null, null, "minor");

            string after = GitHead(dir);
            Assert.NotEqual(before, after);
            Assert.Equal("release", state.CurrentStage);
            Assert.Contains(state.Transitions!, t => t.Stage == "drift-review" && t.NextStage == "release");
            Assert.Equal($"drift-review: 001-f", GitLogSubject(dir, after));
            Assert.Contains("+semver: minor", GitLogBody(dir, after));
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void Stamp_ReleaseStageRecovery_CommitsStagedFixAfterGateProof()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            PrepareDocsOnlyCycle(dir, service);
            service.Stamp("release", null, null, "minor");
            string before = GitHead(dir);

            File.WriteAllText(Path.Combine(dir, "release-fix.txt"), "late packaging fix");
            Git(dir, "add", "release-fix.txt");
            WritePassingReleaseGateProofForCurrentDiff(dir);

            CycleState state = service.Stamp("release", null, null, "minor");

            string after = GitHead(dir);
            Assert.NotEqual(before, after);
            Assert.Equal("release", state.CurrentStage);
            Assert.Contains(state.Transitions!, t => t.Stage == "release" && t.NextStage == "release" && t.CommitSha == after);
            Assert.Equal($"release: 001-f", GitLogSubject(dir, after));
            Assert.Contains("+semver: minor", GitLogBody(dir, after));

            CycleReleaseTrainFeature feature = Assert.Single(service.Status().ReleaseTrain!.Features);
            Assert.EndsWith(".." + before, feature.StageCommitRange, StringComparison.Ordinal);
            Assert.False(feature.StageCommitRange!.StartsWith(after + "..", StringComparison.Ordinal));
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void Stamp_TransitionRefusesUntrackedFilesWithPathDiagnostic()
    {
        string dir = InitRepo();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
            File.WriteAllText(Path.Combine(dir, "docs", "specs", "001-f.md"), "spec body");
            File.WriteAllText(Path.Combine(dir, "extra.txt"), "not in scope");
            Git(dir, "add", "docs/specs/001-f.md");

            var service = new CycleService(dir);
            service.Stamp("specify", "001-f", null);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => service.Stamp("drift-review", null, null));

            Assert.Contains("untracked changes present", ex.Message);
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void Stamp_TransitionRecoversWhenStateWriteWasLostAfterGitCommit()
    {
        string dir = InitRepo();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
            File.WriteAllText(Path.Combine(dir, "docs", "specs", "001-f.md"), "spec body");
            Git(dir, "add", "docs/specs/001-f.md");

            var service = new CycleService(dir);
            service.Stamp("specify", "001-f", null);
            CycleState transitioned = service.Stamp("drift-review", null, null);
            CycleTransitionRecord transition = Assert.Single(transitioned.Transitions!);

            var store = new CycleStateStore(dir);
            CycleState state = store.Read()!;
            store.Write(state with
            {
                BaseRef = transition.PreCommitHead,
                CurrentStage = transition.Stage,
                Transitions = [],
                PendingCommit = new CycleCompletionIntent(
                    JsonContractDefaults.SchemaVersion,
                    transition.Feature,
                    transition.Stage,
                    transition.PreCommitHead,
                    transition.PreCommitHead,
                    transition.ChangeSetId,
                    transition.ChangeSetId,
                    transition.MessageHash,
                    DateTimeOffset.UtcNow.ToString("O"),
                    transition.StagedTreeId,
                    transition.StageProofHashes,
                    transition.GateProofDigest,
                    transition.RunnerIdentity,
                    "cycle-transition/v1",
                    transition.NextStage),
            });

            CycleStatusReport recovered = service.Status();

            Assert.Equal(CycleRecoveryVerdict.Completed, recovered.Recovery?.Verdict);
            CycleState recoveredState = store.Read()!;
            Assert.Null(recoveredState.PendingCommit);
            Assert.Equal("drift-review", recoveredState.CurrentStage);
            Assert.Single(recoveredState.Transitions!);
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void Stamp_SpecifyAfterDriftReview_CarriesCompletedUnreleasedCycle()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            PrepareDocsOnlyCycle(dir, service);
            string before = GitHead(dir);

            CycleState next = service.Stamp("specify", "002-next", null);

            Assert.NotEqual(before, GitHead(dir));
            Assert.Equal("002-next", next.Feature);
            Assert.Equal("specify", next.CurrentStage);
            CycleCompletionRecord completed = Assert.Single(next.CompletedUnreleasedCycles!);
            Assert.Equal("001-f", completed.Feature);
            Assert.Equal("drift-review", completed.Stage);
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void ReleaseTrain_IncludesCompletedUnreleasedAndActiveReleaseFeature()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            PrepareDocsOnlyCycle(dir, service);

            CompleteSecondCycleToRelease(dir, service);

            CycleStatusReport status = service.Status();
            Assert.NotNull(status.ReleaseTrain);
            Assert.True(status.ReleaseTrain!.Valid, string.Join("; ", status.ReleaseTrain.Blockers));
            Assert.Equal(["001-f", "002-next"], status.ReleaseTrain.Features.Select(f => f.Feature).ToArray());
            Assert.All(status.ReleaseTrain.Features, feature =>
            {
                Assert.Equal("drift-review", feature.CompletedStage);
                Assert.Equal("included", feature.InclusionStatus);
                Assert.Equal("pass", feature.TaskCompletionStatus);
                Assert.True(feature.GateProofStatus is "present" or "not-required");
            });
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void ReleaseTrain_UncheckedCarriedForwardTask_FailsReleaseCheck()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            PrepareDocsOnlyCycle(dir, service);
            CompleteSecondCycleToRelease(dir, service);

            File.WriteAllText(Path.Combine(dir, "docs", "tasks", "001-f-tasks.md"),
                "- [ ] `T001` (FR-001, SC-001) - Complete the feature proof.\n");

            CycleCheckReport check = service.Check("release");

            Assert.False(check.Passed);
            Assert.NotNull(check.ReleaseTrain);
            CycleReleaseTrainFeature feature = Assert.Single(check.ReleaseTrain!.Features, f => f.Feature == "001-f");
            Assert.Equal("fail", feature.TaskCompletionStatus);
            Assert.Contains(feature.Blockers, b => b.Contains("unchecked", StringComparison.Ordinal));
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void ReleaseTrain_InvalidCompletedCycle_FailsReleaseCheck()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            PrepareDocsOnlyCycle(dir, service);
            CompleteSecondCycleToRelease(dir, service);

            var store = new CycleStateStore(dir);
            CycleState state = store.Read()!;
            CycleCompletionRecord invalid = state.CompletedUnreleasedCycles!.Single() with
            {
                Stage = "implement",
            };
            store.Write(state with { CompletedUnreleasedCycles = [invalid] });

            CycleCheckReport check = service.Check("release");

            Assert.False(check.Passed);
            Assert.NotNull(check.ReleaseTrain);
            Assert.False(check.ReleaseTrain!.Valid);
            Assert.Contains(check.Prerequisites, p =>
                p.Stage == "release-train"
                && p.Status == "invalid"
                && p.Reason!.Contains("expected drift-review", StringComparison.Ordinal));
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void ReleaseTrain_MarkReleased_MovesIncludedCyclesOutOfUnreleased()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            PrepareDocsOnlyCycle(dir, service);
            CompleteSecondCycleToRelease(dir, service);

            CycleReleaseTrain released = service.MarkReleaseTrainReleased();
            CycleState state = new CycleStateStore(dir).Read()!;

            Assert.True(released.Valid);
            Assert.Empty(state.CompletedUnreleasedCycles!);
            Assert.Equal(["001-f", "002-next"], state.ReleasedCycles!.Select(c => c.Feature).ToArray());
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void Stamp_SpecifyFromImplementStage_FailsClosed()
    {
        string dir = InitRepo();
        try
        {
            string head = GitHead(dir);
            new CycleStateStore(dir).Write(new CycleState(
                JsonContractDefaults.SchemaVersion,
                "001-f",
                "HEAD",
                "implement",
                [new CycleStageProof("specify", CycleStageOutcome.Stamped, "change-set", ["hash"], head)]));

            var service = new CycleService(dir);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => service.Stamp("specify", "002-next", null));

            Assert.Contains("Complete drift-review", ex.Message);
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

    private static string GitLogSubject(string dir, string commit)
    {
        Hx.Runner.Core.Process.ProcessRunResult r = Hx.Runner.Core.Process.ProcessRunner.Run(
            new Hx.Runner.Core.Process.ToolCommand("git", ["log", "-1", "--format=%s", commit], dir));
        Assert.Equal(0, r.ExitCode);
        return r.StandardOutput.Trim();
    }

    private static string GitLogBody(string dir, string commit)
    {
        Hx.Runner.Core.Process.ProcessRunResult r = Hx.Runner.Core.Process.ProcessRunner.Run(
            new Hx.Runner.Core.Process.ToolCommand("git", ["log", "-1", "--format=%B", commit], dir));
        Assert.Equal(0, r.ExitCode);
        return r.StandardOutput;
    }
}
