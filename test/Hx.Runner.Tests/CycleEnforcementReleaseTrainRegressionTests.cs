using Hx.Cycle.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>
/// T027 (FR-033/034/035, SC-017/SC-018): the multi-spec release train is already implemented + verified — this pins
/// it against regression. Starting a new numbered feature from drift-review FINALIZES the prior cycle as
/// completed-unreleased (it is NOT force-released), and a release AGGREGATES every completed-unreleased cycle.
/// </summary>
public sealed partial class CycleEnforcementTests
{
    [Fact]
    public void ReleaseTrain_StartingNewFeatureFinalizesPrior_AndReleaseAggregatesAll()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            PrepareDocsOnlyCycle(dir, service); // 001-f → drift-review

            // FR-033: starting 002 from 001's drift-review finalizes 001 as completed-unreleased, not released.
            CycleState afterStart = service.Stamp("specify", "002-next", null);
            Assert.Equal(["001-f"], afterStart.CompletedUnreleasedCycles!.Select(c => c.Feature).ToArray());
            Assert.Null(afterStart.Completion);
            Assert.Null(afterStart.ReleasedCycles);

            // Finish 002 to release.
            WriteCompletedTaskFile(dir, "002-next");
            Git(dir, "add", "docs/tasks/002-next-tasks.md");
            Git(dir, "commit", "-q", "-m", "seed 002 task file");
            File.WriteAllText(Path.Combine(dir, "docs", "specs", "002-next.md"), "second spec body");
            Git(dir, "add", "docs/specs/002-next.md");
            service.Stamp("specify", "002-next", null);
            service.Stamp("drift-review", null, null);
            WritePassingGateProofForCurrentDiff(dir);
            service.Stamp("release", null, null);

            // FR-034/035: the release train aggregates BOTH completed-unreleased cycles, in order.
            CycleReleaseTrain train = service.GetReleaseTrain();
            Assert.True(train.Valid, string.Join("; ", train.Blockers));
            Assert.Equal(["001-f", "002-next"], train.Features.Select(f => f.Feature).ToArray());

            // SC-018: marking released moves them out of completed-unreleased into released.
            service.MarkReleaseTrainReleased();
            CycleState released = new CycleStateStore(dir).Read()!;
            Assert.Empty(released.CompletedUnreleasedCycles!);
            Assert.Equal(["001-f", "002-next"], released.ReleasedCycles!.Select(c => c.Feature).OrderBy(f => f).ToArray());
        }
        finally { ForceDelete(dir); }
    }
}
