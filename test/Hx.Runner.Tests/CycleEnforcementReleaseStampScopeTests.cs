using System.Linq;
using Hx.Cycle.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>
/// BUG 021: the /08 drift-review report, authored after implement and staged for the drift-review → release
/// transition, must NOT stale the diff-kind <c>implement</c> prerequisite. The report is one of the feature's OWN
/// artifacts; the prerequisite-freshness change-set identity now subtracts the feature's owned doc/review paths, so
/// a later-stage artifact no longer falsely reads as "code changed since stamp" and blocks every release stamp.
/// Reuses the cycle-enforcement git fixture + the passing-gate-proof helper.
/// </summary>
public sealed partial class CycleEnforcementTests
{
    [Fact]
    public void StagingTheDriftReviewReport_DoesNotStaleImplement_AtTheReleaseCheck()
    {
        string dir = InitRepo();
        try
        {
            WriteWorkflow(dir,
                "schemaVersion: 2\nstages:\n" +
                "  - id: specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n" +
                "  - id: implement\n    kind: diff\n    prereqs: [specify]\n" +
                "  - id: drift-review\n    kind: review\n    produces: docs/reviews/{feature}-drift-review.md\n    prereqs: [implement]\n    next: [release]\n" +
                "  - id: release\n    kind: release\n    prereqs: [drift-review]\n");

            var service = new CycleService(dir);
            Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
            File.WriteAllText(Path.Combine(dir, "docs", "specs", "001-f.md"), "spec body");
            Git(dir, "add", "docs/specs/001-f.md");

            service.Stamp("specify", "001-f", null);
            service.Stamp("implement", null, null);        // commits "specify: 001-f"; implement bound to a clean diff
            WritePassingGateProofForCurrentDiff(dir);
            service.Stamp("drift-review", null, null);      // implement -> drift-review (gate-proof-bound)

            // The reproduced condition: the /08 report (a feature-owned artifact) authored + staged for release.
            Directory.CreateDirectory(Path.Combine(dir, "docs", "reviews"));
            File.WriteAllText(Path.Combine(dir, "docs", "reviews", "001-f-drift-review.md"), "no drift");
            Git(dir, "add", "docs/reviews/001-f-drift-review.md");

            // Before the fix this read `implement: stale (code changed since stamp)` and blocked the release stamp.
            CycleCheckReport check = service.Check("release");

            // The assessed bug is narrowly that the owned drift-review report moved the change-set identity and
            // staled the diff-kind `implement` proof. The fix subtracts owned artifacts → implement stays FRESH.
            // (Other prerequisites in this minimal fixture — the report's own artifact binding, the release train —
            // are separate concerns, intentionally out of scope for this regression.)
            StagePrereqResult implement = check.Prerequisites.Single(p => p.Stage == "implement");
            Assert.Equal("fresh", implement.Status);
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void StagingAnUnownedFileStillStalesImplement_AtTheReleaseCheck()
    {
        string dir = InitRepo();
        try
        {
            WriteWorkflow(dir,
                "schemaVersion: 2\nstages:\n" +
                "  - id: specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n" +
                "  - id: implement\n    kind: diff\n    prereqs: [specify]\n" +
                "  - id: drift-review\n    kind: review\n    produces: docs/reviews/{feature}-drift-review.md\n    prereqs: [implement]\n    next: [release]\n" +
                "  - id: release\n    kind: release\n    prereqs: [drift-review]\n");

            var service = new CycleService(dir);
            Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
            File.WriteAllText(Path.Combine(dir, "docs", "specs", "001-f.md"), "spec body");
            Git(dir, "add", "docs/specs/001-f.md");

            service.Stamp("specify", "001-f", null);
            service.Stamp("implement", null, null);
            WritePassingGateProofForCurrentDiff(dir);
            service.Stamp("drift-review", null, null);

            // A NON-owned change (real code) must still stale implement — the exclusion is exact, never a blanket pass.
            Directory.CreateDirectory(Path.Combine(dir, "src"));
            File.WriteAllText(Path.Combine(dir, "src", "Extra.cs"), "a real post-implement code edit");
            Git(dir, "add", "src/Extra.cs");

            CycleCheckReport check = service.Check("release");

            Assert.False(check.Passed);
            Assert.Contains(check.Prerequisites, p => p.Stage == "implement" && p.Status == "stale");
        }
        finally
        {
            ForceDelete(dir);
        }
    }
}
