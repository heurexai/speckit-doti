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

            var state = new CycleState(1, "phase-14-doti-cycle-state", "dev", "specify",
                [new CycleStageProof("specify", CycleStageOutcome.Stamped, "identity-1", ["hash-a"], "abc123")]);
            store.Write(state);

            CycleState? read = store.Read();
            Assert.NotNull(read);
            Assert.Equal("phase-14-doti-cycle-state", read!.Feature);
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
    public void Freshness_Fresh_WhenMatching_Stale_OnIdentityOrArtifactChange()
    {
        string dir = NewTempDir();
        try
        {
            string wf = Path.Combine(dir, "workflow.yml");
            File.WriteAllText(wf, "schemaVersion: 2\nstages:\n  - id: specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n");
            StageModel model = StageModel.Load(wf);

            Directory.CreateDirectory(Path.Combine(dir, "docs", "specs"));
            string artifact = Path.Combine(dir, "docs", "specs", "phase-14.md");
            File.WriteAllText(artifact, "spec body");
            string artifactHash = FileHashing.Sha256OfFile(artifact);

            var proof = new CycleStageProof("specify", CycleStageOutcome.Stamped, "ID", [artifactHash], null);
            var evaluator = new FreshnessEvaluator(dir, model);

            Assert.Equal(StageFreshness.Fresh, evaluator.Evaluate(proof, "phase-14", "ID").Freshness);
            Assert.Equal(StageFreshness.Stale, evaluator.Evaluate(proof, "phase-14", "DIFFERENT").Freshness); // diff moved

            File.WriteAllText(artifact, "spec body CHANGED");
            Assert.Equal(StageFreshness.Stale, evaluator.Evaluate(proof, "phase-14", "ID").Freshness); // artifact changed
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
