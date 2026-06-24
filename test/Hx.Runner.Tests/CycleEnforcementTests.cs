using Hx.Cycle.Core;
using Hx.Impact.Core.Planning;
using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>
/// Enforcement: the operator-question validator (Layers B+C), the prereq-chain keystone, the
/// gate-proof store, and the fail-closed `cycle check`/`cycle commit` behavior (temp git repo fixtures).
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
                "schemaVersion: 2\nstages:\n  - id: specify\n    kind: doc\n    prereqs: []\n  - id: clarify\n    kind: doc\n    prereqs: [specify]\n  - id: commit\n    kind: commit\n    prereqs: [clarify]\n");
            StageModel model = StageModel.Load(wf);
            Assert.Empty(model.Find("specify").Prereqs);
            Assert.Equal("specify", Assert.Single(model.Find("clarify").Prereqs));
            Assert.Equal("clarify", Assert.Single(model.Find("commit").Prereqs));
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

    // ---------------- cycle check / commit (temp git repo) ----------------

    [Fact]
    public void Check_FailsClosed_WhenPrerequisitesAreMissing()
    {
        string dir = InitRepo();
        try
        {
            CycleCheckReport report = new CycleService(dir).Check("commit");
            Assert.False(report.Passed);
            Assert.Contains(report.Prerequisites, p => p.Stage == "specify" && p.Status == "missing");
            Assert.Contains(report.Prerequisites, p => p.Stage == "drift-review" && p.Status == "missing");
        }
        finally
        {
            ForceDelete(dir);
        }
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
    public void Commit_Refuses_WhenPrerequisitesOrGateProofMissing()
    {
        string dir = InitRepo();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
            File.WriteAllText(Path.Combine(dir, "docs", "specs", "001-f.md"), "spec body");

            var service = new CycleService(dir);
            service.Stamp("specify", "001-f", null); // cycle-state now exists; only specify is stamped

            CycleCommitResult result = service.Commit("a message");
            Assert.False(result.Committed);
            Assert.Null(result.CommitSha);
            // refuses for at least the missing drift-review prerequisite and the absent gate proof
            Assert.Contains(result.Reasons, r => r.Contains("drift-review", StringComparison.Ordinal));
            Assert.Contains(result.Reasons, r => r.Contains("gate proof", StringComparison.Ordinal));
        }
        finally
        {
            ForceDelete(dir);
        }
    }

}
