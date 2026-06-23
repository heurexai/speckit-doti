using Hx.Cycle.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

public sealed partial class CycleEnforcementTests
{
    [Fact]
    public void AmbiguousHeadMovementDuringPendingCommit_FailsClosed()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            PrepareDocsOnlyCycle(dir, service);
            var store = new CycleStateStore(dir);
            CycleState state = store.Read()!;
            store.Write(state with { PendingCommit = PendingIntentFor(state, dir) });

            Git(dir, "commit", "-q", "-m", "external commit without doti trailers");

            CycleStatusReport status = service.Status();
            Assert.Equal(CycleRecoveryVerdict.Ambiguous, status.Recovery?.Verdict);

            CycleCheckReport check = service.Check("commit");
            Assert.False(check.Passed);
            Assert.Contains(check.Prerequisites, p => p.Stage == "commit-recovery" && p.Status == "ambiguous");

            CycleCommitResult commit = service.Commit("finish cycle");
            Assert.False(commit.Committed);
            Assert.False(commit.AlreadyCompleted);
            Assert.Contains(commit.Reasons, r => r.Contains("ambiguous", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void NewEditsAfterCompletion_RequireNewSpecifyStamp()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            PrepareDocsOnlyCycle(dir, service);
            CycleCommitResult commit = service.Commit("finish cycle");
            Assert.True(commit.Committed, string.Join("; ", commit.Reasons));

            File.WriteAllText(Path.Combine(dir, "docs", "specs", "next.md"), "next spec body");

            CycleStatusReport status = service.Status();
            Assert.NotNull(status.Completion);
            Assert.Contains(status.Freshness, f => f.Freshness == StageFreshness.Stale);

            CycleCheckReport check = service.Check("commit");
            Assert.False(check.Passed);
            Assert.Contains(check.Prerequisites, p => p.Status == "completed-with-new-changes");

            CycleCommitResult repeated = service.Commit("finish cycle");
            Assert.False(repeated.Committed);
            Assert.False(repeated.AlreadyCompleted);
            Assert.Contains(repeated.Reasons, r => r.Contains("new specify stamp", StringComparison.Ordinal));
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void CompletedCycle_AllowsOnlyNewSpecifyStampToStartNextCycle()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            PrepareDocsOnlyCycle(dir, service);
            CycleCommitResult commit = service.Commit("finish cycle");
            Assert.True(commit.Committed, string.Join("; ", commit.Reasons));

            InvalidOperationException oldStage = Assert.Throws<InvalidOperationException>(
                () => service.Stamp("drift-review", null, null));
            Assert.Contains("previous cycle completed", oldStage.Message);

            File.WriteAllText(Path.Combine(dir, "docs", "specs", "next.md"), "next spec body");
            CycleState next = service.Stamp("specify", "next", null);

            Assert.Equal("next", next.Feature);
            Assert.Equal("specify", next.CurrentStage);
            Assert.Null(next.Completion);
            Assert.Null(next.PendingCommit);
            Assert.Single(next.Stages);
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void Check_FlagsRealClarificationMarker_ButNotaBareMention()
    {
        string dir = InitRepo();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
            string spec = Path.Combine(dir, "docs", "specs", "f.md");
            var service = new CycleService(dir);

            File.WriteAllText(spec, "spec body with a real [NEEDS CLARIFICATION: which database?] marker");
            service.Stamp("specify", "f", null);
            CycleCheckReport withMarker = service.Check("drift-review");
            Assert.Contains(withMarker.Prerequisites, p => p.Stage == "specify" && p.Status == "invalid");

            File.WriteAllText(spec, "spec body that mentions `[NEEDS CLARIFICATION]` only as documentation");
            service.Stamp("specify", "f", null);
            CycleCheckReport withMention = service.Check("drift-review");
            Assert.Contains(withMention.Prerequisites, p => p.Stage == "specify" && p.Status == "fresh");
        }
        finally
        {
            ForceDelete(dir);
        }
    }
}
