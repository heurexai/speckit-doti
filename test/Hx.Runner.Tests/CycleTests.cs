using Hx.Cycle.Core;
using Hx.Runner.Core.Io;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>
/// Cycle-state substrate. Deterministic, git-free unit coverage of the engine pieces; the
/// end-to-end <c>doti cycle stamp</c>/<c>status</c> CLI behavior (which drives git) is verified by the
/// CLI verification steps.
/// </summary>
public sealed class CycleTests
{
    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-cycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void ChangeSetIdentity_IsDeterministic_AndOrderIndependent_AndContentSensitive()
    {
        string dir = NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.txt"), "alpha");
            File.WriteAllText(Path.Combine(dir, "b.txt"), "beta");

            string id1 = ChangeSetIdentity.Compute(dir, ["a.txt", "b.txt"]);
            string id2 = ChangeSetIdentity.Compute(dir, ["b.txt", "a.txt"]); // sorted internally ⇒ identical
            Assert.Equal(id1, id2);

            File.WriteAllText(Path.Combine(dir, "a.txt"), "alpha-CHANGED");
            string id3 = ChangeSetIdentity.Compute(dir, ["a.txt", "b.txt"]);
            Assert.NotEqual(id1, id3); // a content edit moves the identity (the freshness property)
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void StageModel_ParsesSchema2_AndFailsClosedOnV1_AndUnknownStage()
    {
        string dir = NewTempDir();
        try
        {
            string v2 = Path.Combine(dir, "workflow.yml");
            File.WriteAllText(v2,
                "schemaVersion: 2\nname: t\nstages:\n  - id: specify\n    command: doti-specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n");
            StageModel model = StageModel.Load(v2);
            CycleStage specify = model.Find("specify");
            Assert.Equal("doc", specify.Kind);
            Assert.Equal("docs/specs/{feature}.md", specify.Produces);
            Assert.Throws<InvalidOperationException>(() => model.Find("does-not-exist"));

            string v1 = Path.Combine(dir, "old.yml");
            File.WriteAllText(v1, "schemaVersion: 1\ncommands:\n  - doti-specify\n");
            Assert.Throws<InvalidOperationException>(() => StageModel.Load(v1)); // fail closed on the old schema
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CycleStateStore_RoundTrips()
    {
        string dir = NewTempDir();
        try
        {
            var store = new CycleStateStore(dir);
            Assert.Null(store.Read());

            var state = new CycleState(1, "001-doti-cycle-state", "dev", "specify",
                [new CycleStageProof("specify", CycleStageOutcome.Stamped, "identity-1", ["hash-a"], "abc123")]);
            store.Write(state);

            CycleState? read = store.Read();
            Assert.NotNull(read);
            Assert.Equal("001-doti-cycle-state", read!.Feature);
            Assert.Equal("specify", read.CurrentStage);
            Assert.Single(read.Stages);
            Assert.Equal("identity-1", read.Stages[0].ChangeSetId);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void CanonicalHash_IgnoresTaskCheckboxAndHashMarkerAndEol_ButNotTaskText()
    {
        const string baseLine = "- [ ] `T001` do the thing";
        string h0 = CanonicalArtifactHasher.CanonicalHashOfText(baseLine);

        // Checking the box + appending a doti-task-hash marker is implementation progress, NOT a design
        // change — the canonical (design) hash must be unchanged so /07-implement box-checking never stales
        // the tasks/analyze/arch-review stages.
        string checkedWithMarker = "- [x] `T001` do the thing <!-- doti-task-hash: " + new string('a', 64) + " -->";
        Assert.Equal(h0, CanonicalArtifactHasher.CanonicalHashOfText(checkedWithMarker));

        // EOL style + trailing whitespace + a final newline are not design changes either.
        Assert.Equal(h0, CanonicalArtifactHasher.CanonicalHashOfText("- [ ] `T001` do the thing   \r\n"));

        // Editing the task TEXT is a real design change → a different hash.
        Assert.NotEqual(h0, CanonicalArtifactHasher.CanonicalHashOfText("- [ ] `T001` do a DIFFERENT thing"));
    }

    [Fact]
    public void LivingSpec_PrereqArtifactBinding_StalesDependentOnSpecEdit_NotOnBoxCheck()
    {
        string dir = NewTempDir();
        try
        {
            string wf = Path.Combine(dir, "workflow.yml");
            File.WriteAllText(wf,
                "schemaVersion: 2\nstages:\n"
                + "  - id: specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n"
                + "  - id: clarify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: [specify]\n"
                + "  - id: plan\n    kind: doc\n    produces: docs/plans/{feature}-plan.md\n    prereqs: [clarify]\n"
                + "  - id: tasks\n    kind: doc\n    produces: docs/tasks/{feature}-tasks.md\n    prereqs: [plan]\n"
                + "  - id: analyze\n    kind: review\n    prereqs: [tasks]\n");
            StageModel model = StageModel.Load(wf);
            var evaluator = new FreshnessEvaluator(dir, model);
            const string feature = "001-living-spec";

            Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
            Directory.CreateDirectory(Path.Combine(dir, "docs", "plans"));
            Directory.CreateDirectory(Path.Combine(dir, "docs", "tasks"));
            string specPath = Path.Combine(dir, "docs", "specs", feature + ".md");
            string tasksPath = Path.Combine(dir, "docs", "tasks", feature + "-tasks.md");
            File.WriteAllText(specPath, "# spec\nFR-001 do X\n");
            File.WriteAllText(Path.Combine(dir, "docs", "plans", feature + "-plan.md"), "# plan\napproach\n");
            File.WriteAllText(tasksPath, "# tasks\n- [ ] `T001` build X\n");

            CycleStageProof Proof(string stage)
            {
                string? produces = model.Find(stage).Produces;
                IReadOnlyList<string> own = produces is null
                    ? []
                    : [CanonicalArtifactHasher.CanonicalHashOfFile(Path.Combine(
                        dir, FreshnessEvaluator.ResolveProduces(produces, feature).Replace('/', Path.DirectorySeparatorChar)))];
                return new CycleStageProof(stage, CycleStageOutcome.Stamped, "ID", own, "commit",
                    null, CanonicalArtifactHasher.PrerequisiteArtifactHashes(dir, model, stage, feature));
            }

            CycleStageProof planProof = Proof("plan");
            CycleStageProof tasksProof = Proof("tasks");
            CycleStageProof analyzeProof = Proof("analyze");

            StageFreshness Eval(CycleStageProof p) =>
                evaluator.Evaluate(p, feature, "ID", requireChangeSetIdentity: false).Freshness;

            // Fresh at stamp.
            Assert.Equal(StageFreshness.Fresh, Eval(planProof));
            Assert.Equal(StageFreshness.Fresh, Eval(tasksProof));
            Assert.Equal(StageFreshness.Fresh, Eval(analyzeProof));

            // SC-015 enforcement: editing the spec stales plan/tasks/analyze even though their OWN files are
            // untouched (they bind the upstream spec CONTENT).
            File.WriteAllText(specPath, "# spec\nFR-001 do X DIFFERENTLY\n");
            Assert.Equal(StageFreshness.Stale, Eval(planProof));
            Assert.Equal(StageFreshness.Stale, Eval(tasksProof));
            Assert.Equal(StageFreshness.Stale, Eval(analyzeProof));

            // Relaxation: restoring the spec content (e.g. an idempotent re-save / no-content re-stamp upstream)
            // makes them fresh again — the binding is to content, not to the upstream proof object.
            File.WriteAllText(specPath, "# spec\nFR-001 do X\n");
            Assert.Equal(StageFreshness.Fresh, Eval(planProof));
            Assert.Equal(StageFreshness.Fresh, Eval(analyzeProof));

            // Checking a task box during /07-implement is implementation progress: it must NOT stale the tasks
            // stage (own canonical hash) or analyze (which binds tasks.md content).
            File.WriteAllText(tasksPath, "# tasks\n- [x] `T001` build X <!-- doti-task-hash: " + new string('b', 64) + " -->\n");
            Assert.Equal(StageFreshness.Fresh, Eval(tasksProof));
            Assert.Equal(StageFreshness.Fresh, Eval(analyzeProof));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Freshness_Fresh_WhenMatching_Stale_OnIdentityOrArtifactChange()
    {
        string dir = NewTempDir();
        try
        {
            string wf = Path.Combine(dir, "workflow.yml");
            File.WriteAllText(wf, "schemaVersion: 2\nstages:\n  - id: specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n");
            StageModel model = StageModel.Load(wf);

            Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
            string artifact = Path.Combine(dir, "docs", "specs", "001-phase-14.md");
            File.WriteAllText(artifact, "spec body");
            string artifactHash = FileHashing.Sha256OfFile(artifact);

            var proof = new CycleStageProof("specify", CycleStageOutcome.Stamped, "ID", [artifactHash], null);
            var evaluator = new FreshnessEvaluator(dir, model);

            Assert.Equal(StageFreshness.Fresh, evaluator.Evaluate(proof, "001-phase-14", "ID").Freshness);
            Assert.Equal(StageFreshness.Stale, evaluator.Evaluate(proof, "001-phase-14", "DIFFERENT").Freshness); // diff moved

            File.WriteAllText(artifact, "spec body CHANGED");
            Assert.Equal(StageFreshness.Stale, evaluator.Evaluate(proof, "001-phase-14", "ID").Freshness); // artifact changed
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
