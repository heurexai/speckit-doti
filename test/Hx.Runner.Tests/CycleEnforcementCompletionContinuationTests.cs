using Hx.Cycle.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

public sealed partial class CycleEnforcementTests
{
    [Fact]
    public void ChangeSetBoundStage_StaysFresh_AgainstItsOwnInRangeOwnedDoc()
    {
        // 026 regression: the stamp, the transition-rebase, and the freshness check must all compute the
        // SAME change-set identity (the feature's OWN doc/review artifacts subtracted). Before the fix the
        // stamp/rebase stored the RAW identity while the check excluded owned paths (the 021 fix only reached
        // the check), so a change-set-bound stage (drift-review) read a FALSE "stale" against its own
        // committed doc — exactly what blocked the 026 release stamp until I realised it was non-blocking.
        string dir = InitRepo();
        try
        {
            WriteWorkflow(dir,
                "schemaVersion: 2\nstages:\n" +
                "  - id: specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n" +
                "  - id: implement\n    kind: diff\n    prereqs: [specify]\n" +
                "  - id: drift-review\n    kind: review\n    produces: docs/reviews/{feature}-drift-review.md\n    prereqs: [implement]\n" +
                "  - id: release\n    kind: release\n    prereqs: [drift-review]\n");

            var service = new CycleService(dir);
            Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
            File.WriteAllText(Path.Combine(dir, "docs", "specs", "001-f.md"), "spec body");
            Git(dir, "add", "docs/specs/001-f.md");
            service.Stamp("specify", "001-f", null);
            service.Stamp("implement", null, null); // specify->implement commits the spec

            WritePassingGateProofForCurrentDiff(dir);
            service.Stamp("drift-review", null, null); // implement->drift-review (needs gate proof)

            // drift-review's OWN owned artifact, edited + committed, lands in base..HEAD after the stage base.
            Directory.CreateDirectory(Path.Combine(dir, "docs", "reviews"));
            File.WriteAllText(Path.Combine(dir, "docs", "reviews", "001-f-drift-review.md"), "drift review body");
            Git(dir, "add", "docs/reviews/001-f-drift-review.md");
            Git(dir, "commit", "-q", "-m", "drift review doc");
            service.Stamp("drift-review", null, null); // re-bind in place (no advance, no rebase)

            // The change-set-bound stage must NOT read stale against its own in-range owned doc.
            CycleCheckReport check = service.Check("release");
            Assert.Contains(check.Prerequisites, p => p.Stage == "drift-review" && p.Status == "fresh");
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void AmbiguousHeadMovementDuringPendingTransition_FailsClosed()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            PrepareDocsOnlyCycle(dir, service);
            var store = new CycleStateStore(dir);
            CycleState state = store.Read()!;
            store.Write(state with { PendingCommit = PendingIntentFor(state, dir) });

            File.WriteAllText(Path.Combine(dir, "external.txt"), "external");
            Git(dir, "add", "external.txt");
            Git(dir, "commit", "-q", "-m", "external commit without doti trailers");

            CycleStatusReport status = service.Status();
            Assert.Equal(CycleRecoveryVerdict.Ambiguous, status.Recovery?.Verdict);

            CycleCheckReport check = service.Check("release");
            Assert.False(check.Passed);
            Assert.Contains(check.Prerequisites, p => p.Stage == "commit-recovery" && p.Status == "ambiguous");
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void NewEditsAfterTransition_MakePrerequisitesStale()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            PrepareDocsOnlyCycle(dir, service);

            File.WriteAllText(Path.Combine(dir, "docs", "specs", "001-f.md"), "edited after transition");

            CycleStatusReport status = service.Status();
            Assert.Null(status.Completion);
            Assert.Contains(status.Freshness, f => f.Freshness == StageFreshness.Stale);

            CycleCheckReport check = service.Check("release");
            Assert.False(check.Passed);
            Assert.Contains(check.Prerequisites, p => p.Stage == "drift-review" && p.Status == "stale");
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void ImplementationEdits_DoNotStalePreImplementationDocAndReviewPrerequisites()
    {
        string dir = InitRepo();
        try
        {
            WriteWorkflow(dir,
                "schemaVersion: 2\nstages:\n" +
                "  - id: specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n" +
                "  - id: analyze\n    kind: review\n    prereqs: [specify]\n" +
                "  - id: implement\n    kind: diff\n    prereqs: [analyze]\n");

            var service = new CycleService(dir);
            Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
            File.WriteAllText(Path.Combine(dir, "docs", "specs", "001-f.md"), "spec body");
            Git(dir, "add", "docs/specs/001-f.md");
            service.Stamp("specify", "001-f", null);
            service.Stamp("analyze", null, null);

            Directory.CreateDirectory(Path.Combine(dir, "src"));
            File.WriteAllText(Path.Combine(dir, "src", "Feature.cs"), "implementation edit");

            CycleCheckReport check = service.Check("implement");

            Assert.True(check.Passed, string.Join("; ", check.Prerequisites.Select(p => $"{p.Stage}:{p.Status}:{p.Reason}")));
            Assert.Contains(check.Prerequisites, p => p.Stage == "specify" && p.Status == "fresh");
            Assert.Contains(check.Prerequisites, p => p.Stage == "analyze" && p.Status == "fresh");
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void RestampingEarlierStage_DoesNotMoveCurrentStageBackward()
    {
        string dir = InitRepo();
        try
        {
            WriteWorkflow(dir,
                "schemaVersion: 2\nstages:\n" +
                "  - id: specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n" +
                "  - id: plan\n    kind: review\n    prereqs: [specify]\n" +
                "  - id: implement\n    kind: diff\n    prereqs: [plan]\n");

            Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
            File.WriteAllText(Path.Combine(dir, "docs", "specs", "001-f.md"), "spec body");
            Git(dir, "add", "docs/specs/001-f.md");

            var service = new CycleService(dir);
            service.Stamp("specify", "001-f", null);
            service.Stamp("plan", null, null);

            File.WriteAllText(Path.Combine(dir, "docs", "specs", "001-f.md"), "spec body refreshed");
            CycleState refreshed = service.Stamp("specify", "001-f", null);

            Assert.Equal("plan", refreshed.CurrentStage);
            Assert.Single(refreshed.Stages, s => s.Stage == "specify");
            Assert.Contains(refreshed.Stages, s => s.Stage == "plan");
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void DriftReviewCycle_AllowsOnlyNewSpecifyStampToStartNextCycle()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            PrepareDocsOnlyCycle(dir, service);

            CycleState sameStage = service.Stamp("drift-review", null, null);
            Assert.Equal("drift-review", sameStage.CurrentStage);
            Assert.Single(sameStage.Transitions!);

            CycleState next = service.Stamp("specify", "002-next", null);

            Assert.Equal("002-next", next.Feature);
            Assert.Equal("specify", next.CurrentStage);
            Assert.Null(next.Completion);
            Assert.Null(next.PendingCommit);
            Assert.Single(next.Stages);
            Assert.Single(next.CompletedUnreleasedCycles!);
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void LegacyUnimplementedUnnumberedCycle_CanBeMigratedToNumberedSlug()
    {
        string dir = InitRepo();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
            string legacySpec = Path.Combine(dir, "docs", "specs", "legacy-open.md");
            string numberedSpec = Path.Combine(dir, "docs", "specs", "001-legacy-open.md");
            File.WriteAllText(legacySpec, "legacy open spec body");

            new CycleStateStore(dir).Write(new CycleState(
                JsonContractDefaults.SchemaVersion,
                "legacy-open",
                "HEAD",
                "specify",
                [new CycleStageProof("specify", CycleStageOutcome.Stamped, "legacy-change-set", ["legacy-hash"], GitHead(dir))]));

            File.Move(legacySpec, numberedSpec);
            var service = new CycleService(dir);
            CycleState migrated = service.Stamp("specify", "001-legacy-open", null);

            Assert.Equal("001-legacy-open", migrated.Feature);
            Assert.Equal("specify", migrated.CurrentStage);
            Assert.False(File.Exists(legacySpec));
            Assert.True(File.Exists(numberedSpec));

            CycleCheckReport check = service.Check("drift-review");
            Assert.True(check.Passed);
            Assert.Contains(check.Prerequisites, p => p.Stage == "specify" && p.Status == "fresh");
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void CompletedLegacyUnnumberedCycle_LeavesHistoryAndAllowsNumberedNextSpec()
    {
        string dir = InitRepo();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
            string legacySpec = Path.Combine(dir, "docs", "specs", "legacy-done.md");
            File.WriteAllText(legacySpec, "legacy completed spec body");
            Git(dir, "add", "docs/specs/legacy-done.md");
            Git(dir, "commit", "-q", "-m", "legacy completed spec");
            string head = GitHead(dir);

            var completion = new CycleCompletionRecord(
                JsonContractDefaults.SchemaVersion,
                "legacy-done",
                "drift-review",
                "HEAD",
                head,
                head,
                "legacy-change-set",
                "legacy-gate-change-set",
                "legacy-message",
                DateTimeOffset.UtcNow.ToString("O"),
                ExpectedCompletionShape: "cycle-transition/v1",
                NextStage: "specify");
            new CycleStateStore(dir).Write(new CycleState(
                JsonContractDefaults.SchemaVersion,
                "legacy-done",
                "HEAD",
                "drift-review",
                [new CycleStageProof("specify", CycleStageOutcome.Stamped, "legacy-change-set", ["legacy-hash"], head)],
                Completion: completion));

            string nextSpec = Path.Combine(dir, "docs", "specs", "002-next.md");
            File.WriteAllText(nextSpec, "next spec body");
            var service = new CycleService(dir);
            CycleState next = service.Stamp("specify", "002-next", null);

            Assert.True(File.Exists(legacySpec));
            Assert.Equal("002-next", next.Feature);
            Assert.Equal("specify", next.CurrentStage);
            Assert.Null(next.Completion);
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
            string spec = Path.Combine(dir, "docs", "specs", "001-f.md");
            var service = new CycleService(dir);

            File.WriteAllText(spec, "spec body with a real [NEEDS CLARIFICATION: which database?] marker");
            service.Stamp("specify", "001-f", null);
            CycleCheckReport withMarker = service.Check("drift-review");
            Assert.Contains(withMarker.Prerequisites, p => p.Stage == "specify" && p.Status == "invalid");

            File.WriteAllText(spec, "spec body that mentions `[NEEDS CLARIFICATION]` only as documentation");
            service.Stamp("specify", "001-f", null);
            CycleCheckReport withMention = service.Check("drift-review");
            Assert.Contains(withMention.Prerequisites, p => p.Stage == "specify" && p.Status == "fresh");
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    private static void WriteWorkflow(string dir, string content)
    {
        string workflow = Path.Combine(dir, ".doti", "workflows", "doti", "workflow.yml");
        File.WriteAllText(workflow, content);
        Git(dir, "add", ".doti/workflows/doti/workflow.yml");
        Git(dir, "commit", "-q", "-m", "replace workflow");
    }
}
