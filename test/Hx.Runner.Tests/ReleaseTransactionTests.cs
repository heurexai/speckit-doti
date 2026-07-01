using Hx.Runner.Core.Git;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>039 WI2/T011/SC-006: the compensation ledger's core mechanics, proven deterministically without dotnet.</summary>
public sealed class ReleaseTransactionTests
{
    [Fact]
    public void Rollback_runsCompensationsInReverseOrder_andReportsAllSucceeded()
    {
        var tx = new ReleaseTransaction();
        var order = new List<string>();
        tx.Record("undo tag", () => order.Add("tag"));
        tx.Record("undo dir", () => order.Add("dir"));
        tx.Record("undo cycle-state", () => order.Add("cycle-state"));

        RollbackReport report = tx.Rollback(ReleaseStage.Record, "boom");

        Assert.Equal(["cycle-state", "dir", "tag"], order); // reverse of record order
        Assert.Equal(ReleaseStage.Record, report.FailedStage);
        Assert.Equal("boom", report.Reason);
        Assert.False(report.AnyResidual);
        Assert.All(report.Compensations, c => Assert.True(c.Succeeded));
    }

    [Fact]
    public void Rollback_isBestEffortAll_andFlagsAResidual_whenACompensationThrows()
    {
        var tx = new ReleaseTransaction();
        bool earlierRan = false;
        tx.Record("undo earlier (must still run)", () => earlierRan = true);
        tx.Record("undo failing", () => throw new IOException("locked"));

        RollbackReport report = tx.Rollback(ReleaseStage.Tag, "boom");

        Assert.True(earlierRan, "a failing compensation must NOT skip the remaining ones (best-effort-all)");
        Assert.True(report.AnyResidual, "a compensation failure is a fail-closed residual, never reported as a clean revert");
        Assert.Contains(report.Compensations, c => !c.Succeeded && c.Action.Contains("failing", StringComparison.Ordinal));
    }

    [Fact]
    public void Commit_runsCleanups_butNeverTheUndos()
    {
        var tx = new ReleaseTransaction();
        bool undone = false;
        bool cleaned = false;
        tx.Record("undo (must NOT run on commit)", () => undone = true, () => cleaned = true);

        tx.Commit();

        Assert.False(undone, "Commit keeps the release's durable results — it must never run the undos");
        Assert.True(cleaned, "Commit runs each cleanup (e.g. delete a baseline backup no longer needed)");
    }
}
