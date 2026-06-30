using System.Diagnostics;
using Hx.Cycle.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Cycle.Tests;

/// <summary>
/// 027 T012 + T014: end-to-end (git-backed) coverage of the codified stamp reconciliation — the self-modifying
/// workflow / edge-only rebind (a byte-identical prereq SET move auto-reconciles via <c>refresh --apply-safe</c>
/// with no re-run), the <c>inserted-stage</c> verdict (the current graph requires a stage absent from cycle-state),
/// the on-stamp auto-cascade (re-stamping ONE upstream rebinds its content-equal dependents with zero manual stamps;
/// a RerunRequired dependent is never auto-stamped), and <see cref="CycleService.RebaseProofsToHead"/> recomputing
/// <see cref="CycleStageProof.PrerequisiteArtifactHashes"/> across a transition (no false PrereqArtifactChanged).
/// Git-backed because the stamp/transition path computes the change-set identity and writes transition commits.
/// </summary>
public sealed class ReconcileCascadeTests
{
    private const string Feature = "001-test";

    // ---- T012: edge-only / self-modifying workflow + inserted-stage ----

    [Fact]
    public void EdgeOnly_byte_identical_prereq_set_move_classifies_as_ReBindContentEqual()
    {
        string dir = NewGitRepo();
        try
        {
            // plan's bound prereq SET omits the spec edge the current graph now requires, but no shared path's
            // content differs (the only divergence is the SET/edge) — a pure edge move, never a content change.
            CycleService service = EdgeOnlyCycle(dir);

            CycleRecoveryPlan plan = service.RecoveryPlan("tasks");

            StageRecoveryStep step = Assert.Single(plan.Steps, s => s.Stage == "plan");
            Assert.Equal(RestampSafety.ReBindContentEqual, step.Safety);
            Assert.Equal("doti cycle refresh --target tasks --apply-safe", step.NextCommand);
            Assert.True(plan.Recoverable); // a content-equal rebind is fully recoverable by --apply-safe
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void EdgeOnly_refresh_apply_safe_clears_the_rebindable_stage_with_no_rerun()
    {
        string dir = NewGitRepo();
        try
        {
            CycleService service = EdgeOnlyCycle(dir);

            CycleRefreshResult result = service.Refresh("tasks", applySafe: true);

            Assert.Contains("plan", result.Refreshed); // re-stamped (re-bound), not re-run
            Assert.Empty(result.Remaining);            // the whole chain settled in one pass
            Assert.DoesNotContain(service.Status().Freshness,
                f => f.Stage == "plan" && f.Freshness == StageFreshness.Stale);
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void EdgeOnly_dry_run_surfaces_the_rebindable_step_without_mutating()
    {
        string dir = NewGitRepo();
        try
        {
            CycleService service = EdgeOnlyCycle(dir);

            CycleRefreshResult result = service.Refresh("tasks", applySafe: false);

            Assert.Empty(result.Refreshed);
            Assert.Contains(result.Remaining, s => s.Stage == "plan" && s.Safety == RestampSafety.ReBindContentEqual);
            Assert.Contains(service.Status().Freshness,
                f => f.Stage == "plan" && f.Freshness == StageFreshness.Stale); // still stale — nothing applied
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void InsertedStage_required_by_the_graph_but_absent_from_state_is_an_inserted_stage_verdict()
    {
        string dir = NewGitRepo();
        try
        {
            // Stamp specify+plan only, then re-render the workflow to insert `clarify` between them. The current
            // graph now requires `clarify` (a prereq of plan) but cycle-state has no proof for it.
            CycleService service = InsertedStageCycle(dir);

            CycleRecoveryPlan plan = service.RecoveryPlan("plan");

            StageRecoveryStep inserted = Assert.Single(plan.Steps, s => s.Stage == "clarify");
            Assert.Equal(CycleRecoveryPlanner.InsertedStageStatus, inserted.Status);
            Assert.Equal("/02-doti-clarify", inserted.NextCommand); // names the single /NN command to produce+stamp it
            Assert.Null(inserted.Safety);                            // not a stale-with-known-safety step
            Assert.False(plan.Recoverable);                          // --apply-safe cannot author a missing artifact
        }
        finally { DeleteDir(dir); }
    }

    // ---- T014: on-stamp auto-cascade + transition recomputes the prereq-artifact binding ----

    [Fact]
    public void Cascade_restamping_one_upstream_auto_rebinds_content_equal_dependents_with_no_explicit_refresh()
    {
        string dir = NewGitRepo();
        try
        {
            // tasks is content-equal-rebindable behind plan (its bound prereq SET omits the spec edge). Re-stamping
            // plan (the one changed upstream) must auto-cascade the safe rebind onto tasks — no explicit Refresh call.
            CycleService service = CascadeCycle(dir);
            Assert.Contains(service.Status().Freshness,
                f => f.Stage == "tasks" && f.Freshness == StageFreshness.Stale); // stale before the upstream re-stamp

            service.Stamp("plan", feature: null, baseRef: null); // the ONLY explicit stamp

            // The on-stamp cascade rebound tasks to the unchanged content — no manual `refresh` and no tasks re-run.
            Assert.DoesNotContain(service.Status().Freshness,
                f => f.Stage == "tasks" && f.Freshness == StageFreshness.Stale);
            Assert.True(service.Check("verify").Passed); // verify's prereq closure (incl. tasks) is all fresh
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void Cascade_never_auto_stamps_a_rerun_required_dependent()
    {
        string dir = NewGitRepo();
        try
        {
            // tasks has a real OWN content change (its bound own-artifact hash mismatches the file) — RerunRequired.
            // Re-stamping the upstream plan must NOT auto-stamp tasks: a genuine content change still earns a re-run.
            CycleService service = CascadeCycle(dir, tasksOwnArtifactHashes: ["a-stale-own-hash"]);

            service.Stamp("plan", feature: null, baseRef: null);

            Assert.Contains(service.Status().Freshness,
                f => f.Stage == "tasks" && f.Freshness == StageFreshness.Stale); // still RerunRequired — not rubber-stamped
            StageRecoveryStep blocker = Assert.Single(service.RecoveryPlan("verify").Steps, s => s.Stage == "tasks");
            Assert.Equal(RestampSafety.RerunRequired, blocker.Safety);
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void Transition_recomputes_PrerequisiteArtifactHashes_so_no_false_PrereqArtifactChanged()
    {
        string dir = NewGitRepo();
        try
        {
            // Stamp specify+plan at CurrentStage=plan, then advance to tasks: the tasks stamp fires a stage
            // transition that rebases the prior proofs (specify, plan) to the new HEAD. FR-007: the rebase must
            // recompute PrerequisiteArtifactHashes too — without it, plan keeps its pre-transition prereq-artifact
            // binding and the next check reads a FALSE PrereqArtifactChanged even though the spec never changed.
            CycleService service = TransitionCycle(dir);

            service.Stamp("tasks", feature: null, baseRef: null); // transition plan -> tasks (a real commit)

            // Every stamped stage is fresh after the transition — plan does NOT read a phantom prereq-artifact stale.
            Assert.DoesNotContain(service.Status().Freshness, f => f.Freshness == StageFreshness.Stale);
            Assert.True(service.Check("verify").Passed); // plan's prereq (spec content) reads fresh post-rebase
        }
        finally { DeleteDir(dir); }
    }

    // ---- fixtures ----

    // specify -> plan -> tasks -> release(diff): plan stamped with a prereq SET that omits the spec edge the
    // current graph requires; every shared path is byte-identical and specify is Fresh, so plan is ReBindContentEqual.
    private static CycleService EdgeOnlyCycle(string dir)
    {
        WriteFourStageWorkflow(dir);
        (string specHash, string planHash, string tasksHash, string head) = WriteArtifactsAndCommit(dir);

        var state = new CycleState(JsonContractDefaults.SchemaVersion, Feature, head, "tasks",
        [
            Proof("specify", [specHash], [], head),
            // plan's bound prereq set is EMPTY; the current graph binds the spec — a pure edge add, no shared path differs.
            Proof("plan", [planHash], [], head),
            Proof("tasks", [tasksHash], [$"docs/plans/{Feature}-plan.md:{planHash}", $"docs/specs/{Feature}.md:{specHash}"], head),
        ]);
        new CycleStateStore(dir).Write(state);
        return new CycleService(dir);
    }

    // specify -> plan -> tasks -> release(diff): tasks' bound prereq set omits the spec edge; plan + specify are
    // Fresh so tasks is ReBindContentEqual. Re-stamping plan must cascade the rebind onto tasks.
    private static CycleService CascadeCycle(string dir, IReadOnlyList<string>? tasksOwnArtifactHashes = null)
    {
        WriteFourStageWorkflow(dir);
        (string specHash, string planHash, string tasksHash, string head) = WriteArtifactsAndCommit(dir);
        string verifyHash = Write(dir, $"docs/verify/{Feature}-verify.md", "verify");
        Git(dir, "add", "-A");
        Git(dir, "commit", "-q", "-m", "verify");
        head = Git(dir, "rev-parse", "HEAD");

        var state = new CycleState(JsonContractDefaults.SchemaVersion, Feature, head, "verify",
        [
            Proof("specify", [specHash], [], head),
            Proof("plan", [planHash], [$"docs/specs/{Feature}.md:{specHash}"], head),
            // tasks' bound prereq set omits the spec edge (a byte-identical SET move) ⇒ ReBindContentEqual,
            // UNLESS its own artifact binding is deliberately stale (the RerunRequired case).
            Proof("tasks", tasksOwnArtifactHashes ?? [tasksHash],
                [$"docs/plans/{Feature}-plan.md:{planHash}"], head),
            Proof("verify", [verifyHash],
                [$"docs/tasks/{Feature}-tasks.md:{tasksHash}", $"docs/plans/{Feature}-plan.md:{planHash}", $"docs/specs/{Feature}.md:{specHash}"], head),
        ]);
        new CycleStateStore(dir).Write(state);
        return new CycleService(dir);
    }

    // Stamp specify+plan under a graph WITHOUT clarify, then re-render the workflow to insert clarify between them,
    // so the current graph requires a `clarify` stage absent from cycle-state.
    private static CycleService InsertedStageCycle(string dir)
    {
        // Initial graph: specify -> plan (no clarify).
        string path = WorkflowPath(dir);
        File.WriteAllText(path,
            "schemaVersion: 2\nname: t\nstages:\n" +
            "  - id: specify\n    command: 01-doti-specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n" +
            "  - id: plan\n    command: 03-doti-plan\n    kind: doc\n    produces: docs/plans/{feature}-plan.md\n    prereqs: [specify]\n");
        (string specHash, string planHash, _, string head) = WriteArtifactsAndCommit(dir);

        var state = new CycleState(JsonContractDefaults.SchemaVersion, Feature, head, "plan",
        [
            Proof("specify", [specHash], [], head),
            Proof("plan", [planHash], [$"docs/specs/{Feature}.md:{specHash}"], head),
        ]);
        new CycleStateStore(dir).Write(state);

        // Re-render the workflow to insert clarify between specify and plan (declaration order matters).
        File.WriteAllText(path,
            "schemaVersion: 2\nname: t\nstages:\n" +
            "  - id: specify\n    command: 01-doti-specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n" +
            "  - id: clarify\n    command: 02-doti-clarify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: [specify]\n" +
            "  - id: plan\n    command: 03-doti-plan\n    kind: doc\n    produces: docs/plans/{feature}-plan.md\n    prereqs: [clarify]\n");
        return new CycleService(dir);
    }

    // specify+plan stamped at CurrentStage=plan, with the tasks file present + committed so the tasks stamp can
    // transition. plan carries a real PrerequisiteArtifactHashes (the spec) that the rebase must recompute.
    private static CycleService TransitionCycle(string dir)
    {
        WriteFourStageWorkflow(dir);
        (string specHash, string planHash, _, string head) = WriteArtifactsAndCommit(dir);

        var state = new CycleState(JsonContractDefaults.SchemaVersion, Feature, head, "plan",
        [
            Proof("specify", [specHash], [], head),
            Proof("plan", [planHash], [$"docs/specs/{Feature}.md:{specHash}"], head),
        ]);
        new CycleStateStore(dir).Write(state);
        return new CycleService(dir);
    }

    private static CycleStageProof Proof(string stage, IReadOnlyList<string> art, IReadOnlyList<string> prereq, string head) =>
        new(stage, CycleStageOutcome.Stamped, "id", art, head, null, prereq);

    // Four doc stages: specify -> plan -> tasks -> verify. All kind:doc, so none is change-set-bound (no diff stage
    // in any prereq closure) — the precondition for the PrereqRebindable arm. `verify` is the most-downstream stage,
    // so the on-stamp cascade after re-stamping plan refreshes through it (rebinding tasks).
    private static void WriteFourStageWorkflow(string dir)
    {
        File.WriteAllText(WorkflowPath(dir),
            "schemaVersion: 2\nname: t\nstages:\n" +
            "  - id: specify\n    command: 01-doti-specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n" +
            "  - id: plan\n    command: 03-doti-plan\n    kind: doc\n    produces: docs/plans/{feature}-plan.md\n    prereqs: [specify]\n" +
            "  - id: tasks\n    command: 05-doti-tasks\n    kind: doc\n    produces: docs/tasks/{feature}-tasks.md\n    prereqs: [plan]\n" +
            "  - id: verify\n    command: 06-doti-verify\n    kind: doc\n    produces: docs/verify/{feature}-verify.md\n    prereqs: [tasks]\n");
    }

    private static string WorkflowPath(string dir)
    {
        string path = Path.Combine(dir, ".doti", "workflows", "doti", "workflow.yml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }

    private static (string SpecHash, string PlanHash, string TasksHash, string Head) WriteArtifactsAndCommit(string dir)
    {
        string specHash = Write(dir, $"docs/specs/{Feature}.md", "spec");
        string planHash = Write(dir, $"docs/plans/{Feature}-plan.md", "plan");
        string tasksHash = Write(dir, $"docs/tasks/{Feature}-tasks.md", "tasks");
        Git(dir, "add", "-A");
        Git(dir, "commit", "-q", "-m", "init");
        string head = Git(dir, "rev-parse", "HEAD");
        return (specHash, planHash, tasksHash, head);
    }

    private static string Write(string dir, string relative, string content)
    {
        string full = Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return CanonicalArtifactHasher.CanonicalHashOfText(content);
    }

    // git marks loose-object files read-only on Windows; clear the attribute before recursive delete.
    private static void DeleteDir(string dir)
    {
        foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(dir, recursive: true);
    }

    private static string NewGitRepo()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-reconcile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Git(dir, "init", "-q");
        Git(dir, "config", "user.email", "t@example.com");
        Git(dir, "config", "user.name", "Test");
        Git(dir, "config", "commit.gpgsign", "false");
        // The cycle-state file is gitignored in a real repo; ignore it here so a transition's clean-tree guard
        // does not trip on the state CycleStateStore.Write leaves untracked.
        File.WriteAllText(Path.Combine(dir, ".gitignore"), ".doti/cycle-state.json\n");
        return dir;
    }

    private static string Git(string dir, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using Process process = Process.Start(psi)!;
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output.Trim();
    }
}
