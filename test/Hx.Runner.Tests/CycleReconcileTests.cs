using System.Diagnostics;
using Hx.Cycle.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>
/// 027 T011 + T016 — end-to-end (git-backed, real <see cref="CycleService"/>) coverage of the SAFE auto-rebind
/// invariant: re-binding a staled stamp is allowed ONLY for a pure prereq edge/reorder with byte-identical shared
/// content (and only once every producing upstream is Fresh and the dependent is not review-kind). A real artifact
/// CONTENT change, a review-kind dependent, and a change-set-bound stage all STAY <see cref="RestampSafety.RerunRequired"/>
/// and are NEVER auto-rebound by <c>refresh --apply-safe</c> or the on-stamp cascade.
///
/// Modeled on <c>test/Hx.Cycle.Tests/RefreshTests.cs</c>: a real git repo, a workflow.yml with the
/// specify→clarify→plan→tasks→analyze chain (analyze is review-kind), and a hand-seeded <see cref="CycleState"/> whose
/// dependent proofs bind the upstream CONTENT. A "pure edge/reorder" is seeded by giving a dependent proof a prereq
/// binding that carries an extra DROPPED-edge entry while every shared path's hash is byte-identical to the current
/// closure (the <see cref="StaleReason.PrereqRebindable"/> shape proven at the unit level in
/// <c>FreshnessReasonTests</c>); a "content change" is a real file edit.
/// </summary>
public sealed class CycleReconcileTests
{
    private const string Feature = "001-test";

    [Fact]
    public void Arch_blocker_edits_spec_a_real_content_change_keeps_plan_tasks_analyze_never_auto_rebound()
    {
        // SC-001 / W1 scenario 1: a spec CONTENT edit is a real input change. plan/tasks/analyze bind the spec
        // content, so they all stale as PrereqArtifactChanged, and `refresh --apply-safe` must re-stamp NONE of them
        // (it would otherwise rubber-stamp a downstream against an upstream it never re-derived against — the exact
        // inversion the SAFE invariant forbids). 028: the doc dependents now route to the agent-gated ReviewedNoImpact
        // tier (never auto-applied), while the review-kind analyze stays RerunRequired — neither is auto-rebound.
        string dir = NewGitRepo();
        try
        {
            CycleService service = SeedFreshChain(dir, out string specPath, out _, out _);

            // A real content change to the spec — every dependent binds it.
            File.WriteAllText(specPath, "# spec\nFR-001 do X DIFFERENTLY\n");

            // Status: specify AND clarify read OwnArtifactChanged (clarify shares the spec file as its own
            // produced artifact); plan/tasks/analyze bind the spec only as a prerequisite ⇒ PrereqArtifactChanged.
            CycleStatusReport status = service.Status();
            Assert.Equal(StaleReason.OwnArtifactChanged, StaleOf(status, "specify"));
            Assert.Equal(StaleReason.OwnArtifactChanged, StaleOf(status, "clarify"));
            foreach (string dependent in new[] { "plan", "tasks", "analyze" })
            {
                Assert.Equal(StaleReason.PrereqArtifactChanged, StaleOf(status, dependent));
            }

            // The recovery plan for analyze (its stale PREREQUISITES) is not recoverable by --apply-safe. 028: the doc
            // dependents (plan, tasks) are the agent-gated ReviewedNoImpact tier; specify/clarify changed their OWN
            // artifact ⇒ RerunRequired. NEITHER is a SafeReinterpret/ReBindContentEqual auto-rebind tier.
            CycleRecoveryPlan planForAnalyze = service.RecoveryPlan("analyze");
            Assert.False(planForAnalyze.Recoverable);
            Assert.Equal(RestampSafety.ReviewedNoImpact, planForAnalyze.Steps.Single(s => s.Stage == "plan").Safety);
            Assert.Equal(RestampSafety.ReviewedNoImpact, planForAnalyze.Steps.Single(s => s.Stage == "tasks").Safety);
            Assert.Equal(RestampSafety.RerunRequired, planForAnalyze.Steps.Single(s => s.Stage == "specify").Safety);
            Assert.Equal(RestampSafety.RerunRequired, planForAnalyze.Steps.Single(s => s.Stage == "clarify").Safety);
            Assert.All(planForAnalyze.Steps, s => Assert.True(
                s.Safety is RestampSafety.ReviewedNoImpact or RestampSafety.RerunRequired));

            // refresh --apply-safe re-stamps NOTHING — neither a review-kind RerunRequired nor the agent-gated
            // ReviewedNoImpact tier is auto-applied.
            CycleRefreshResult refresh = service.Refresh("analyze", applySafe: true);
            Assert.Empty(refresh.Refreshed);
            Assert.All(refresh.Remaining, s => Assert.True(
                s.Safety is RestampSafety.ReviewedNoImpact or RestampSafety.RerunRequired));
            // The dependents are still stale after the safe pass (the file edit stands; nothing rubber-stamped it).
            CycleStatusReport after = service.Status();
            foreach (string dependent in new[] { "plan", "tasks", "analyze" })
            {
                Assert.Equal(StageFreshness.Stale, FreshnessOf(after, dependent));
            }
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void Arch_blocker_edits_plan_a_pure_edge_move_keeps_specify_clarify_Fresh_and_rebinds_tasks_after_plan_restamp()
    {
        // arch-blocker-edits-plan: the plan's prereq EDGE set lagged (a dropped edge) but every shared upstream
        // artifact is byte-identical (a pure edge/reorder, NOT a content change). specify/clarify (upstream of plan)
        // stay Fresh; plan is ReBindContentEqual; re-stamping plan rebinds it AND auto-cascades to its content-equal
        // dependent tasks (the genuinely-mechanical case the operator no longer hand-re-stamps).
        string dir = NewGitRepo();
        try
        {
            CycleService service = SeedEdgeLag(dir, lagStages: ["plan", "tasks"]);

            CycleStatusReport status = service.Status();
            // Upstream of the lagged plan stay Fresh — the edge move did not touch their bindings.
            Assert.Equal(StageFreshness.Fresh, FreshnessOf(status, "specify"));
            Assert.Equal(StageFreshness.Fresh, FreshnessOf(status, "clarify"));
            // plan + tasks read the rebindable lag (own content unchanged, only the prereq SET moved).
            Assert.Equal(StaleReason.PrereqRebindable, StaleOf(status, "plan"));
            Assert.Equal(StaleReason.PrereqRebindable, StaleOf(status, "tasks"));

            // Re-stamping the changed stage (plan) auto-cascades the safe rebind to its content-equal dependent
            // tasks — zero manual stamp of tasks (the on-stamp cascade, FR-006).
            service.Stamp("plan", feature: null, baseRef: null);

            CycleStatusReport after = service.Status();
            Assert.Equal(StageFreshness.Fresh, FreshnessOf(after, "plan"));
            Assert.Equal(StageFreshness.Fresh, FreshnessOf(after, "tasks"));
            // specify/clarify never moved.
            Assert.Equal(StageFreshness.Fresh, FreshnessOf(after, "specify"));
            Assert.Equal(StageFreshness.Fresh, FreshnessOf(after, "clarify"));
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void Revise_tasks_after_analyze_negative_a_tasks_content_edit_without_restamp_leaves_analyze_RerunRequired()
    {
        // T016 negative + the review-kind carve-out (SC-003): editing tasks.md content stales analyze (it binds the
        // tasks content). analyze is review-kind, so it is NEVER auto-rebound — it stays RerunRequired until its
        // single input change is actually re-run, even though only the prereq binding diverged.
        string dir = NewGitRepo();
        try
        {
            CycleService service = SeedFreshChain(dir, out _, out _, out string tasksPath);

            // A real content change to tasks (not a box-check — change the task TEXT so the canonical hash moves).
            File.WriteAllText(tasksPath, "# tasks\n- [ ] `T001` build X DIFFERENTLY\n");

            CycleStatusReport status = service.Status();
            Assert.Equal(StaleReason.OwnArtifactChanged, StaleOf(status, "tasks"));
            Assert.Equal(StaleReason.PrereqArtifactChanged, StaleOf(status, "analyze"));

            // The recovery plan for analyze is not recoverable: tasks (its changed prerequisite) is RerunRequired,
            // so `refresh --apply-safe` cannot reach a green analyze.
            CycleRecoveryPlan plan = service.RecoveryPlan("analyze");
            Assert.False(plan.Recoverable);
            StageRecoveryStep tasksStep = Assert.Single(plan.Steps, s => s.Stage == "tasks");
            Assert.Equal(RestampSafety.RerunRequired, tasksStep.Safety);

            // refresh --apply-safe re-stamps neither tasks (a content change) nor the review-kind analyze; analyze
            // stays stale until the changed stage is genuinely re-run.
            CycleRefreshResult refresh = service.Refresh("analyze", applySafe: true);
            Assert.Empty(refresh.Refreshed);
            Assert.Equal(StageFreshness.Stale, FreshnessOf(service.Status(), "analyze"));
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void Revise_tasks_after_analyze_analyze_is_Fresh_again_only_after_tasks_then_analyze_are_re_run()
    {
        // T016 positive: the operator re-runs the ONE genuinely-changed stage (tasks). The cascade does NOT
        // auto-stamp analyze (review-kind). Re-running analyze itself (a real re-run, not a rubber-stamp) re-binds
        // it to the new tasks content and it is Fresh again — the honest path.
        string dir = NewGitRepo();
        try
        {
            CycleService service = SeedFreshChain(dir, out _, out _, out string tasksPath);
            File.WriteAllText(tasksPath, "# tasks\n- [ ] `T001` build X DIFFERENTLY\n");

            // Re-run tasks (the changed stage). Its on-stamp cascade must NOT auto-stamp the review-kind analyze.
            service.Stamp("tasks", feature: null, baseRef: null);
            Assert.Equal(StageFreshness.Fresh, FreshnessOf(service.Status(), "tasks"));
            Assert.Equal(StageFreshness.Stale, FreshnessOf(service.Status(), "analyze"));

            // Re-running analyze itself re-derives its verdict over the new input — now Fresh.
            service.Stamp("analyze", feature: null, baseRef: null);
            Assert.Equal(StageFreshness.Fresh, FreshnessOf(service.Status(), "analyze"));
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void Multi_round_arch_review_thrash_round_two_auto_rebinds_and_the_rebindable_set_does_not_regrow()
    {
        // T016 multi-round: a pure edge/reorder lag on plan+tasks is auto-reconciled by re-stamping plan (round 1);
        // a SECOND, independent edge lag is again auto-reconciled (round 2) — the cascade terminates each round
        // (the rebindable set strictly shrinks to empty, never regrows into an oscillation).
        string dir = NewGitRepo();
        try
        {
            CycleService service = SeedEdgeLag(dir, lagStages: ["plan", "tasks"]);

            // Round 1.
            Assert.Equal(StaleReason.PrereqRebindable, StaleOf(service.Status(), "plan"));
            service.Stamp("plan", feature: null, baseRef: null);
            CycleStatusReport afterRound1 = service.Status();
            Assert.Equal(StageFreshness.Fresh, FreshnessOf(afterRound1, "plan"));
            Assert.Equal(StageFreshness.Fresh, FreshnessOf(afterRound1, "tasks"));
            // No regrowth: nothing is stale, so the next check is a clean no-op.
            Assert.DoesNotContain(afterRound1.Freshness, f => f.Freshness == StageFreshness.Stale);

            // Round 2: induce a fresh edge lag on tasks only, then reconcile via `refresh --apply-safe`.
            ReseedEdgeLag(dir, lagStages: ["tasks"]);
            service = new CycleService(dir);
            Assert.Equal(StaleReason.PrereqRebindable, StaleOf(service.Status(), "tasks"));

            CycleRefreshResult refresh = service.Refresh("analyze", applySafe: true);
            Assert.Contains("tasks", refresh.Refreshed);
            CycleStatusReport afterRound2 = service.Status();
            Assert.Equal(StageFreshness.Fresh, FreshnessOf(afterRound2, "tasks"));
            // Terminated cleanly — no rebindable step remains and the set did not regrow.
            Assert.DoesNotContain(afterRound2.Freshness, f => f.Freshness == StageFreshness.Stale);
        }
        finally { DeleteDir(dir); }
    }

    // ----- harness -----

    private static StaleReason? StaleOf(CycleStatusReport status, string stage) =>
        status.Freshness.Single(f => f.Stage == stage).StaleReason;

    private static StageFreshness FreshnessOf(CycleStatusReport status, string stage) =>
        status.Freshness.Single(f => f.Stage == stage).Freshness;

    private static StageModel WriteWorkflow(string dir)
    {
        string path = Path.Combine(dir, ".doti", "workflows", "doti", "workflow.yml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // All doc/review stages (no diff/implement) so freshness is content-bound (requireChangeSetIdentity:false),
        // which is what lets the PrereqRebindable arm fire. analyze is review-kind (never auto-rebound).
        File.WriteAllText(path,
            "schemaVersion: 2\nname: t\nstages:\n" +
            "  - id: specify\n    command: 01-doti-specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n" +
            "  - id: clarify\n    command: 02-doti-clarify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: [specify]\n" +
            "  - id: plan\n    command: 03-doti-plan\n    kind: doc\n    produces: docs/plans/{feature}-plan.md\n    prereqs: [clarify]\n" +
            "  - id: tasks\n    command: 05-doti-tasks\n    kind: doc\n    produces: docs/tasks/{feature}-tasks.md\n    prereqs: [plan]\n" +
            "  - id: analyze\n    command: 06-doti-analyze\n    kind: review\n    produces: docs/reviews/{feature}-analyze-report.md\n    prereqs: [tasks]\n");
        return StageModel.Load(path);
    }

    // A fully-Fresh seeded chain: every stage's own artifact bound, every prereq binding matching the live closure.
    private static CycleService SeedFreshChain(string dir, out string specPath, out string planPath, out string tasksPath)
    {
        StageModel model = WriteWorkflow(dir);
        specPath = Write(dir, "docs/specs/001-test.md", "# spec\nFR-001 do X\n");
        planPath = Write(dir, "docs/plans/001-test-plan.md", "# plan\napproach\n");
        tasksPath = Write(dir, "docs/tasks/001-test-tasks.md", "# tasks\n- [ ] `T001` build X\n");
        Write(dir, "docs/reviews/001-test-analyze-report.md", "# analyze\nno blockers\n");
        Git(dir, "add", "-A");
        Git(dir, "commit", "-q", "-m", "init");
        string head = Git(dir, "rev-parse", "HEAD");

        var stages = model.Stages.Select(s => FreshProof(dir, model, s.Id, head)).ToList();
        new CycleStateStore(dir).Write(new CycleState(1, Feature, head, "analyze", stages));
        return new CycleService(dir);
    }

    // Seed a fully-fresh chain, then make the named stages stale with a PURE EDGE LAG: their prereq binding carries
    // an extra DROPPED-edge entry on top of the live (byte-identical) shared paths — the rebindable shape.
    private static CycleService SeedEdgeLag(string dir, IReadOnlyList<string> lagStages)
    {
        SeedFreshChain(dir, out _, out _, out _);
        ReseedEdgeLag(dir, lagStages);
        return new CycleService(dir);
    }

    // Rewrite cycle-state so the named stages carry an edge-lagged prereq binding (live shared paths + a phantom
    // dropped edge), leaving every other stage's binding matching the live closure.
    private static void ReseedEdgeLag(string dir, IReadOnlyList<string> lagStages)
    {
        var store = new CycleStateStore(dir);
        CycleState state = store.Read()!;
        StageModel model = StageModel.Load(Path.Combine(dir, ".doti", "workflows", "doti", "workflow.yml"));
        string head = Git(dir, "rev-parse", "HEAD");

        var rewritten = state.Stages.Select(proof =>
        {
            if (!lagStages.Contains(proof.Stage, StringComparer.OrdinalIgnoreCase))
            {
                return proof;
            }

            IReadOnlyList<string> live =
                CanonicalArtifactHasher.PrerequisiteArtifactHashes(dir, model, proof.Stage, Feature);
            // A phantom dropped edge: a path NOT in the live closure ⇒ shared paths stay byte-identical, only the
            // SET moved ⇒ PrereqRebindable (own artifact + every shared content unchanged).
            var lagged = live.Append("docs/specs/001-dropped-edge.md:OLD-EDGE-HASH").ToList();
            return proof with { PrerequisiteArtifactHashes = lagged };
        }).ToList();

        store.Write(state with { Stages = rewritten });
    }

    private static CycleStageProof FreshProof(string dir, StageModel model, string stageId, string head)
    {
        CycleStage stage = model.Find(stageId);
        IReadOnlyList<string> own = stage.Produces is { } pattern
            ? [CanonicalArtifactHasher.CanonicalHashOfFile(Path.Combine(
                dir, StageModel.ResolveProduces(pattern, Feature).Replace('/', Path.DirectorySeparatorChar)))]
            : [];
        IReadOnlyList<string> prereqs =
            CanonicalArtifactHasher.PrerequisiteArtifactHashes(dir, model, stageId, Feature);
        return new CycleStageProof(stageId, CycleStageOutcome.Stamped, "id", own, head, null, prereqs);
    }

    private static string Write(string dir, string relative, string content)
    {
        string full = Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    private static void DeleteDir(string dir)
    {
        foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            try { File.SetAttributes(file, FileAttributes.Normal); }
            catch { /* best-effort */ }
        }

        try { Directory.Delete(dir, recursive: true); }
        catch (IOException) { /* temp dir; the OS reclaims it */ }
        catch (UnauthorizedAccessException) { /* temp dir; the OS reclaims it */ }
    }

    private static string NewGitRepo()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-reconcile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Git(dir, "init", "-q");
        Git(dir, "config", "user.email", "t@example.com");
        Git(dir, "config", "user.name", "Test");
        Git(dir, "config", "commit.gpgsign", "false");
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
