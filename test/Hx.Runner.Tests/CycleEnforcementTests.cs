using Hx.Cycle.Core;
using Hx.Impact.Core.Planning;
using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>
/// Enforcement: the operator-question validator (Layers B+C), the prereq-chain keystone, the
/// gate-proof store, and the fail-closed cycle transition behavior (temp git repo fixtures).
/// </summary>
public sealed partial class CycleEnforcementTests
{
    // ---------------- OperatorQuestionValidator (Layers B+C) ----------------

    [Fact]
    public void OperatorQuestion_Conformant_Passes()
    {
        OperatorQuestionValidation v = OperatorQuestionValidator.Validate(ValidQuestion());
        Assert.True(v.Valid, string.Join("; ", v.Errors));
    }

    [Fact]
    public void OperatorQuestion_MalformedVariants_FailClosed()
    {
        Assert.False(OperatorQuestionValidator.Validate(ValidQuestion() with { Question = "  " }).Valid);
        Assert.False(OperatorQuestionValidator.Validate(ValidQuestion() with { WhyItMatters = "" }).Valid);
        // an option missing its cons
        Assert.False(OperatorQuestionValidator.Validate(ValidQuestion() with
        {
            Options = [new OperatorQuestionOption("A", ["p"], [], "c"), new OperatorQuestionOption("B", ["p"], ["c"], "c")],
        }).Valid);
        // fewer than two options
        Assert.False(OperatorQuestionValidator.Validate(ValidQuestion() with
        {
            Options = [new OperatorQuestionOption("A", ["p"], ["c"], "c")],
        }).Valid);
        // recommendation names no real option
        Assert.False(OperatorQuestionValidator.Validate(ValidQuestion() with { Recommendation = new OperatorRecommendation("Z", "?") }).Valid);
        // confidence without a reason
        Assert.False(OperatorQuestionValidator.Validate(ValidQuestion() with { Confidence = new OperatorConfidence("High", " ") }).Valid);
        // an unverified assumption that does not say what would verify it
        Assert.False(OperatorQuestionValidator.Validate(ValidQuestion() with { Assumptions = [new OperatorAssumption("y", false, null)] }).Valid);
        // a premise without evidence
        Assert.False(OperatorQuestionValidator.Validate(ValidQuestion() with { Premises = [new OperatorPremise("p", "")] }).Valid);
    }

    // ---------------- Prereq-chain keystone (analyze F1) ----------------

    [Fact]
    public void StageModel_ParsesInlinePrereqs()
    {
        string dir = NewTempDir();
        try
        {
            string wf = Path.Combine(dir, "workflow.yml");
            File.WriteAllText(wf,
                "schemaVersion: 2\nstages:\n  - id: specify\n    kind: doc\n    prereqs: []\n  - id: clarify\n    kind: doc\n    prereqs: [specify]\n  - id: release\n    kind: release\n    prereqs: [clarify]\n");
            StageModel model = StageModel.Load(wf);
            Assert.Empty(model.Find("specify").Prereqs);
            Assert.Equal("specify", Assert.Single(model.Find("clarify").Prereqs));
            Assert.Equal("clarify", Assert.Single(model.Find("release").Prereqs));
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    // ---------------- GateProofStore ----------------

    [Fact]
    public void GateProofStore_RoundTrips()
    {
        string dir = NewTempDir();
        try
        {
            var store = new GateProofStore(dir);
            Assert.Null(store.Read());

            var persisted = new PersistedGateProof(
                1, "id-1", "dev", Lane.Normal, new GateProof(1, StageOutcome.Pass, [], []), "abc123");
            store.Write(persisted);

            PersistedGateProof? read = store.Read();
            Assert.NotNull(read);
            Assert.Equal("id-1", read!.ChangeSetId);
            Assert.Equal(Lane.Normal, read.Lane);
            Assert.Equal(StageOutcome.Pass, read.Proof.Outcome);
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void GateProofValidator_RejectsLegacyProofWithoutAffectedTestProof()
    {
        string dir = NewTempDir();
        try
        {
            var persisted = new PersistedGateProof(
                1, "id-1", "dev", Lane.Normal, new GateProof(1, StageOutcome.Pass, [], []), "abc123");

            IReadOnlyList<string> reasons = GateProofValidator.ValidateAffectedTestProof(dir, persisted);

            Assert.Contains(reasons, r => r.Contains("no affected-test proof", StringComparison.Ordinal));
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    // ---------------- cycle check / transition (temp git repo) ----------------

    [Fact]
    public void Check_FailsClosed_WhenPrerequisitesAreMissing()
    {
        // A FEATURE-stage check on a repo with no cycle-state fails closed with its prerequisites reported "missing".
        // Uses drift-review (a feature stage) deliberately: `release` is now bug-only-aware (038) and no longer reports
        // phantom feature-stamp "missing" results on a null-state repo — it delegates to the bug release train instead
        // (see BugOnlyReleasePathTests for the release-stage null-state path, which still fails closed on the train's
        // own blocker). The generic "missing prerequisite → fail closed" property is unchanged for feature stages.
        string dir = InitRepo();
        try
        {
            CycleCheckReport report = new CycleService(dir).Check("drift-review");
            Assert.False(report.Passed);
            Assert.Contains(report.Prerequisites, p => p.Stage == "specify" && p.Status == "missing");
            // The InitRepo model is specify -> drift-review -> release; drift-review's only prerequisite is specify.
            // No bug-only release-train stand-in appears for a feature-stage check (038 is release-scoped).
            Assert.DoesNotContain(report.Prerequisites, p => p.Stage == "release-train");
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void Transition_stagesAndCommitsTheLeavingStagesProducedDoc_evenWhenAuthoredUntracked()
    {
        // 039 WI1/FR-001/SC-001: THE live bug. A produced doc authored but left untracked must be committed BY the
        // coded transition, not orphaned. Pre-fix, the specify->drift-review transition empty-committed and left the
        // spec untracked (what the 006 agent hit). The engine now stages the leaving stage's produces itself.
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            string spec = Path.Combine(dir, "docs", "specs", "001-f.md");
            Directory.CreateDirectory(Path.GetDirectoryName(spec)!);
            File.WriteAllText(spec, "# spec\n"); // authored, UNTRACKED — never git add
            service.Stamp("specify", "001-f", null);      // initial stamp, no transition
            service.Stamp("drift-review", "001-f", null); // specify->drift-review transition commits specify's produces

            Assert.True(IsTracked(dir, "docs/specs/001-f.md"), "the produced spec must be committed by the transition, not orphaned");
            Assert.Contains("docs/specs/001-f.md", GitOut(dir, "show", "--name-only", "--format=", "HEAD"));
        }
        finally { ForceDelete(dir); }
    }

    [Fact]
    public void Transition_doesNotFailClose_whenTheProducesIsPresentAndAlreadyCommittedUnchanged()
    {
        // 039 WI1/FR-002/SC-001: two consecutive stages declaring the SAME produces (specify+clarify -> the spec). The
        // spec is committed by specify->clarify; the clarify->plan transition (clarify re-declaring the unchanged,
        // already-committed spec) must transition as a no-content-change commit, NOT fail-close. Guards the 031/#42
        // doc-dance the arch-review flagged.
        string dir = InitRepoWithStages(
            "  - id: specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n"
            + "  - id: clarify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: [specify]\n"
            + "  - id: plan\n    kind: doc\n    produces: docs/plans/{feature}.md\n    prereqs: [clarify]\n");
        try
        {
            var service = new CycleService(dir);
            string spec = Path.Combine(dir, "docs", "specs", "001-f.md");
            Directory.CreateDirectory(Path.GetDirectoryName(spec)!);
            File.WriteAllText(spec, "# spec\n");
            service.Stamp("specify", "001-f", null);
            service.Stamp("clarify", "001-f", null); // specify->clarify commits the spec
            Assert.True(IsTracked(dir, "docs/specs/001-f.md"));

            service.Stamp("plan", "001-f", null); // clarify->plan: clarify's produces is the present+committed spec — must NOT throw
        }
        finally { ForceDelete(dir); }
    }

    [Fact]
    public void FinalizeReleasedCycle_failsClosed_whenTheCycleIsNotAtReleaseStage()
    {
        // 039 WI4/FR-032: finalize-release only finalizes a cycle that reached release — a mid-cycle stage fails closed.
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            string spec = Path.Combine(dir, "docs", "specs", "001-f.md");
            Directory.CreateDirectory(Path.GetDirectoryName(spec)!);
            File.WriteAllText(spec, "# spec\n");
            service.Stamp("specify", "001-f", null); // at specify, not release

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => service.FinalizeReleasedCycle());
            Assert.Contains("not 'release'", ex.Message);
        }
        finally { ForceDelete(dir); }
    }

    [Fact]
    public void FinalizeReleasedCycle_isANoOp_whenThereIsNoCycleState()
    {
        // 039 WI4: idempotent / safe — a repo with no cycle-state (nothing to finalize) must not throw.
        string dir = InitRepo();
        try
        {
            new CycleService(dir).FinalizeReleasedCycle(); // must not throw
        }
        finally { ForceDelete(dir); }
    }

    [Fact]
    public void Transition_failsClosed_whenTheLeavingStageDeclaresAProducesNeverAuthored()
    {
        // 039 WI1/FR-002/SC-001: a declared produces that is absent from disk AND uncommitted is a genuine orphan —
        // fail closed with the named path, instead of a silent empty commit.
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);
            service.Stamp("specify", "001-f", null); // specify declares produces docs/specs/001-f.md; never authored

            CycleInputException ex = Assert.Throws<CycleInputException>(
                () => service.Stamp("drift-review", "001-f", null));
            Assert.Contains("docs/specs/001-f.md", ex.Message);
        }
        finally { ForceDelete(dir); }
    }

    [Fact]
    public void Stamp_FailsClosed_WhenPrerequisitesAreMissing()
    {
        string dir = InitRepo();
        try
        {
            var service = new CycleService(dir);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => service.Stamp("drift-review", "001-f", null));

            Assert.Contains("prerequisites are not all fresh", ex.Message);
            Assert.Contains("specify: missing", ex.Message);
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void Stamp_FailsClosed_WhenInitialFeatureSlugIsNotNumbered()
    {
        string dir = InitRepo();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
            File.WriteAllText(Path.Combine(dir, "docs", "specs", "not-numbered.md"), "spec body");

            var service = new CycleService(dir);

            CycleInputException ex = Assert.Throws<CycleInputException>(
                () => service.Stamp("specify", "not-numbered", null));

            Assert.Contains("Feature slug 'not-numbered' is not numbered", ex.Message);
            Assert.Contains("NNN-short-name", ex.Message);
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void Stamp_CanAdvanceWhenCycleStateIsIgnored()
    {
        string dir = NewTempDir();
        try
        {
            Git(dir, "init", "-q");
            Git(dir, "config", "user.email", "t@example.com");
            Git(dir, "config", "user.name", "Test");
            Git(dir, "config", "commit.gpgsign", "false");

            string wfDir = Path.Combine(dir, ".doti", "workflows", "doti");
            Directory.CreateDirectory(wfDir);
            Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
            File.WriteAllText(Path.Combine(wfDir, "workflow.yml"),
                "schemaVersion: 2\nstages:\n  - id: specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n  - id: clarify\n    kind: doc\n    prereqs: [specify]\n");
            File.WriteAllText(Path.Combine(dir, ".gitignore"),
                ".nomos/cycle-state.json\n.doti/cycle-state.json\n.doti/gate-proof.json\n");
            File.WriteAllText(Path.Combine(dir, "docs", "specs", "001-example.md"), "spec body\n");
            Git(dir, "add", "-A");
            Git(dir, "commit", "-q", "-m", "seed");

            var service = new CycleService(dir);
            service.Stamp("specify", "001-example", null);
            CycleState state = service.Stamp("clarify", "001-example", null);

            Assert.Equal("clarify", state.CurrentStage);
            Assert.Contains(state.Stages, s => s.Stage == "specify");
            Assert.Contains(state.Stages, s => s.Stage == "clarify");
        }
        finally
        {
            ForceDelete(dir);
        }
    }

    [Fact]
    public void Stamp_RefusesStartingAnotherFeatureBeforeDriftReview()
    {
        string dir = InitRepo();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
            File.WriteAllText(Path.Combine(dir, "docs", "specs", "001-f.md"), "spec body");

            var service = new CycleService(dir);
            service.Stamp("specify", "001-f", null); // cycle-state now exists; only specify is stamped

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => service.Stamp("specify", "002-next", null));

            Assert.Contains("Complete drift-review", ex.Message);
        }
        finally
        {
            ForceDelete(dir);
        }
    }

}
