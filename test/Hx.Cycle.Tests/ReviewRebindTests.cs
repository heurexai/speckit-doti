using System.Diagnostics;
using Hx.Cycle.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Cycle.Tests;

/// <summary>
/// 028 T005/T006/T020 (SC-001/002/003/007): the agent-gated reviewed-no-impact rebind, end-to-end on a real git repo
/// (the change-set identity is repository-derived). The specify→clarify→plan→tasks→analyze chain is all doc/review, so
/// freshness is content-bound and a spec edit stales the downstream plan as <see cref="StaleReason.PrereqArtifactChanged"/>
/// — the attestable shape. Asserts: a bare stamp on the attestable stale THROWS; <c>refresh --apply-safe</c> never
/// applies the reviewed-no-impact tier; <see cref="CycleService.ReviewRebind"/> rebinds the target only, writes the
/// record in one write, and the rebound proof DECAYS on a later upstream edit (with and without an intervening rebase).
/// </summary>
public sealed class ReviewRebindTests
{
    private const string Feature = "001-test";

    [Fact]
    public void Bare_stamp_on_an_attestable_prereq_only_stale_throws_and_routes_to_review_rebind()
    {
        string dir = NewGitRepo();
        try
        {
            CycleService service = SeedFreshChain(dir, out string specPath);
            EditCanonical(specPath, "# spec\nFR-001 do X with a clarifying note\n");

            // plan is stale ONLY because the spec (its prerequisite) content changed; its own plan.md is untouched.
            Assert.Equal(StaleReason.PrereqArtifactChanged, StaleOf(service.Status(), "plan"));

            CycleReviewRebindRequiredException ex = Assert.Throws<CycleReviewRebindRequiredException>(
                () => service.Stamp("plan", feature: null, baseRef: null));
            Assert.Equal("plan", ex.Target);
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void A_real_reauthor_of_the_stage_stamps_normally()
    {
        string dir = NewGitRepo();
        try
        {
            CycleService service = SeedFreshChain(dir, out _);

            // A genuine re-author: the plan's OWN artifact content changes (its upstream spec is untouched, so its
            // prerequisites stay fresh) ⇒ OwnArtifactChanged, NOT attestable ⇒ the fence does not fire; the stamp
            // succeeds and records no reviewed-rebind.
            EditCanonical(Path.Combine(dir, "docs/plans/001-test-plan.md"), "# plan\nrevised approach\n");
            Assert.Equal(StaleReason.OwnArtifactChanged, StaleOf(service.Status(), "plan"));

            CycleState after = service.Stamp("plan", feature: null, baseRef: null);

            Assert.Equal(StageFreshness.Fresh, FreshnessOf(new CycleService(dir).Status(), "plan"));
            Assert.Null(after.ReviewedRebinds); // a real stamp records no reviewed-rebind
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void Refresh_apply_safe_never_applies_the_reviewed_no_impact_tier()
    {
        string dir = NewGitRepo();
        try
        {
            CycleService service = SeedFreshChain(dir, out string specPath);
            EditCanonical(specPath, "# spec\nFR-001 do X with a clarifying note\n");

            CycleRefreshResult refresh = service.Refresh("analyze", applySafe: true);

            // The reviewed-no-impact tier requires an agent decision — it is never auto-applied by refresh.
            Assert.DoesNotContain("plan", refresh.Refreshed);
            Assert.Contains(refresh.Remaining, s => s.Stage == "plan" && s.Safety == RestampSafety.ReviewedNoImpact);
            Assert.Equal(StageFreshness.Stale, FreshnessOf(service.Status(), "plan"));
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void ReviewRebind_rebinds_the_target_records_one_write_and_does_not_cascade()
    {
        string dir = NewGitRepo();
        try
        {
            CycleService service = SeedFreshChain(dir, out string specPath);
            EditCanonical(specPath, "# spec\nFR-001 do X with a clarifying note\n");

            // tasks also binds the spec content transitively ⇒ it is stale too (it must surface for its OWN attestation).
            Assert.Equal(StaleReason.PrereqArtifactChanged, StaleOf(service.Status(), "plan"));
            Assert.Equal(StaleReason.PrereqArtifactChanged, StaleOf(service.Status(), "tasks"));

            CycleState reb = service.ReviewRebind("plan", "no-impact", "the clarifying note does not change the plan");

            // plan is Fresh; the record is written; tasks is NOT cascaded (B7 — each downstream attests for itself).
            Assert.Equal(StageFreshness.Fresh, FreshnessOf(new CycleService(dir).Status(), "plan"));
            Assert.Equal(StageFreshness.Stale, FreshnessOf(new CycleService(dir).Status(), "tasks"));

            CycleReviewedRebindRecord record = Assert.Single(reb.ReviewedRebinds!);
            Assert.Equal("plan", record.DependentStage);
            Assert.Equal("no-impact", record.Verdict);
            Assert.Equal("the clarifying note does not change the plan", record.Reason);
            Assert.Contains("specify", record.ChangedUpstreams); // specify produces the changed spec
            Assert.NotEqual(record.BeforeHashes, record.AfterHashes);

            // SC-007: the persisted state carries BOTH the rebound proof outcome AND the record (one whole-state write).
            CycleState persisted = new CycleStateStore(dir).Read()!;
            CycleStageProof planProof = persisted.Stages.Single(s => s.Stage == "plan");
            Assert.Equal(CycleStageOutcome.ReviewedNoImpactRebound, planProof.Outcome);
            Assert.Single(persisted.ReviewedRebinds!);
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void ReviewRebind_decays_when_the_upstream_is_edited_again()
    {
        string dir = NewGitRepo();
        try
        {
            CycleService service = SeedFreshChain(dir, out string specPath);
            EditCanonical(specPath, "# spec\nFR-001 do X with a clarifying note\n");
            service.ReviewRebind("plan", "no-impact", null);
            Assert.Equal(StageFreshness.Fresh, FreshnessOf(new CycleService(dir).Status(), "plan"));

            // A LATER upstream edit re-stales plan: the rebound proof is an ordinary content-bound proof (no snapshot).
            EditCanonical(specPath, "# spec\nFR-001 do X COMPLETELY DIFFERENTLY\n");
            Assert.Equal(StaleReason.PrereqArtifactChanged, StaleOf(new CycleService(dir).Status(), "plan"));
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void ReviewRebind_decays_across_an_intervening_commit()
    {
        // SC-003: an unrelated transition (a new commit advancing HEAD) does not break decay — a later upstream move
        // still re-stales the rebound proof, since freshness is re-derived from content, never a stored snapshot.
        string dir = NewGitRepo();
        try
        {
            CycleService service = SeedFreshChain(dir, out string specPath);
            EditCanonical(specPath, "# spec\nFR-001 do X with a clarifying note\n");
            service.ReviewRebind("plan", "no-impact", null);

            // Commit the spec edit + an unrelated file, advancing HEAD (the "rebase" analogue).
            Write(dir, "unrelated.txt", "unrelated change");
            Git(dir, "add", "-A");
            Git(dir, "commit", "-q", "-m", "advance");
            Assert.Equal(StageFreshness.Fresh, FreshnessOf(new CycleService(dir).Status(), "plan")); // still fresh

            // Now a genuine later upstream edit re-stales it.
            EditCanonical(specPath, "# spec\nFR-001 do X yet AGAIN differently\n");
            Assert.Equal(StaleReason.PrereqArtifactChanged, StaleOf(new CycleService(dir).Status(), "plan"));
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void ReviewRebind_refuses_a_non_attestable_stage()
    {
        string dir = NewGitRepo();
        try
        {
            CycleService service = SeedFreshChain(dir, out _);
            // analyze is review-kind — never attestable, even on a prerequisite content change.
            EditCanonical(Path.Combine(dir, "docs/tasks/001-test-tasks.md"), "# tasks\n- [ ] `T001` build X DIFFERENTLY\n");
            Assert.Equal(StaleReason.PrereqArtifactChanged, StaleOf(service.Status(), "analyze"));

            CycleReviewRebindIneligibleException ex = Assert.Throws<CycleReviewRebindIneligibleException>(
                () => service.ReviewRebind("analyze", "no-impact", null));
            Assert.Equal(ReviewRebindRefusal.Ineligible, ex.Refusal);
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void ReviewRebind_refuses_a_fresh_stage_as_not_stale()
    {
        string dir = NewGitRepo();
        try
        {
            CycleService service = SeedFreshChain(dir, out _);
            CycleReviewRebindIneligibleException ex = Assert.Throws<CycleReviewRebindIneligibleException>(
                () => service.ReviewRebind("plan", "no-impact", null));
            Assert.Equal(ReviewRebindRefusal.NotStale, ex.Refusal);
        }
        finally { DeleteDir(dir); }
    }

    // ----- harness (modeled on CycleReconcileTests) -----

    private static StaleReason? StaleOf(CycleStatusReport status, string stage) =>
        status.Freshness.Single(f => f.Stage == stage).StaleReason;

    private static StageFreshness FreshnessOf(CycleStatusReport status, string stage) =>
        status.Freshness.Single(f => f.Stage == stage).Freshness;

    private static StageModel WriteWorkflow(string dir)
    {
        string path = Path.Combine(dir, ".doti", "workflows", "doti", "workflow.yml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path,
            "schemaVersion: 2\nname: t\nstages:\n" +
            "  - id: specify\n    command: 01-doti-specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n    next: [clarify]\n" +
            "  - id: clarify\n    command: 02-doti-clarify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: [specify]\n    next: [plan]\n" +
            "  - id: plan\n    command: 03-doti-plan\n    kind: doc\n    produces: docs/plans/{feature}-plan.md\n    prereqs: [clarify]\n    next: [tasks]\n" +
            "  - id: tasks\n    command: 05-doti-tasks\n    kind: doc\n    produces: docs/tasks/{feature}-tasks.md\n    prereqs: [plan]\n    next: [analyze]\n" +
            "  - id: analyze\n    command: 06-doti-analyze\n    kind: review\n    produces: docs/reviews/{feature}-analyze-report.md\n    prereqs: [tasks]\n    next: []\n");
        return StageModel.Load(path);
    }

    private static CycleService SeedFreshChain(string dir, out string specPath)
    {
        StageModel model = WriteWorkflow(dir);
        specPath = Write(dir, "docs/specs/001-test.md", "# spec\nFR-001 do X\n");
        Write(dir, "docs/plans/001-test-plan.md", "# plan\napproach\n");
        Write(dir, "docs/tasks/001-test-tasks.md", "# tasks\n- [ ] `T001` build X\n");
        Write(dir, "docs/reviews/001-test-analyze-report.md", "# analyze\nno blockers\n");
        Git(dir, "add", "-A");
        Git(dir, "commit", "-q", "-m", "init");
        string head = Git(dir, "rev-parse", "HEAD");

        var stages = model.Stages.Select(s => FreshProof(dir, model, s.Id, head)).ToList();
        new CycleStateStore(dir).Write(new CycleState(1, Feature, head, "analyze", stages));
        return new CycleService(dir);
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

    private static void EditCanonical(string fullPath, string content) => File.WriteAllText(fullPath, content);

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
        string dir = Path.Combine(Path.GetTempPath(), "hx-reviewrebind-" + Guid.NewGuid().ToString("N"));
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
