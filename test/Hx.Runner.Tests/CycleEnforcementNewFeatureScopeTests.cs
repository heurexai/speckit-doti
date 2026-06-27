using Hx.Cycle.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>
/// T012 (FR-038/SC-020): a new-feature start (drift-review → specify) must succeed with the incoming feature's
/// spec already on disk (untracked) — the bug was the prior feature's readiness pre-check reading that spec as a
/// dirty tree and blocking the one-stamp start. A stray UNRELATED untracked file MUST still block (the exclusion
/// is exactly the incoming feature's owned paths, by exact path — never a prefix). Lives beside the other cycle
/// enforcement tests to reuse their git fixture.
/// </summary>
public sealed partial class CycleEnforcementTests
{
    [Fact]
    public void NewFeatureStart_WithIncomingSpecOnDisk_SucceedsInOneStamp()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            PrepareDocsOnlyCycle(dir, service); // ends at drift-review

            // The incoming feature's spec is on disk but untracked — this is the reproduced FR-038 condition.
            File.WriteAllText(Path.Combine(dir, "docs", "specs", "002-next.md"), "next spec body");

            CycleState next = service.Stamp("specify", "002-next", null);

            Assert.Equal("002-next", next.Feature);
            Assert.Equal("specify", next.CurrentStage);
            Assert.Single(next.Stages);
        }
        finally { ForceDelete(dir); }
    }

    [Fact]
    public void NewFeatureStart_WithAStrayUnrelatedUntrackedFile_StillBlocks()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            PrepareDocsOnlyCycle(dir, service);

            File.WriteAllText(Path.Combine(dir, "docs", "specs", "002-next.md"), "next spec body");
            // A stray file the incoming feature does NOT own — the deliberate-scope guard must still fire.
            File.WriteAllText(Path.Combine(dir, "stray-unrelated.txt"), "not owned by 002-next");

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => service.Stamp("specify", "002-next", null));
            Assert.Contains("untracked", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally { ForceDelete(dir); }
    }

    [Fact]
    public void NewFeatureStart_DoesNotExcludeAStraySiblingUnderTheOwnedDirectory()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            PrepareDocsOnlyCycle(dir, service);

            File.WriteAllText(Path.Combine(dir, "docs", "specs", "002-next.md"), "next spec body");
            // Exact-path, not prefix: a different file under docs/specs/ is NOT owned and must still block.
            File.WriteAllText(Path.Combine(dir, "docs", "specs", "002-next-stray.md"), "sibling, not owned");

            Assert.Throws<InvalidOperationException>(() => service.Stamp("specify", "002-next", null));
        }
        finally { ForceDelete(dir); }
    }
}
