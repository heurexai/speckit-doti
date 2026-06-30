using System.Diagnostics;
using Hx.Cycle.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Cycle.Tests;

/// <summary>
/// 031 T012 (FR-016, SC-015, resolves #42): the doc-stamp scope-consistency lock. Authoring a stage's produces doc
/// AHEAD of stamping the transition INTO that stage must NOT trip the transition's untracked-changes guard (the
/// doc-dance) — <see cref="CycleService.ValidateTransitionReadiness"/> now excludes the active feature's owned
/// produces paths exactly as <c>Check()</c> does. But a genuinely FOREIGN untracked file present at the same time
/// MUST still block the transition (fail-closed transition scope preserved): the exclusion is scoped to
/// <see cref="FeatureArtifactScope.OwnedPaths"/>, never a blanket "ignore untracked".
/// </summary>
public sealed class DocStampScopeTests
{
    private const string Feature = "001-test";

    [Fact]
    public void Authoring_next_stage_produces_doc_ahead_then_stamping_into_it_succeeds_with_no_dance()
    {
        string dir = NewGitRepo();
        try
        {
            CycleService service = SetupAtSpecify(dir);

            // The doc-dance trigger: author the PLAN produces doc (the next stage's artifact) ahead of stamping,
            // leaving it UNTRACKED — historically this tripped "untracked changes present" in the transition guard.
            Write(dir, "docs/plans/001-test-plan.md", "plan authored ahead");

            // Stamp the transition specify -> plan. With FR-016 the owned plan doc is excluded from the untracked
            // scope guard, so this no longer throws and needs no set-aside/stage/restore dance.
            CycleState state = service.Stamp("plan", Feature, baseRef: null);

            Assert.Equal("plan", state.CurrentStage);
            Assert.Contains(state.Stages, s => string.Equals(s.Stage, "plan", StringComparison.OrdinalIgnoreCase));
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void A_foreign_untracked_file_present_at_the_same_time_still_blocks_the_transition()
    {
        string dir = NewGitRepo();
        try
        {
            CycleService service = SetupAtSpecify(dir);

            // The feature's own produces doc (excluded) AND a genuinely foreign untracked file (NOT owned).
            Write(dir, "docs/plans/001-test-plan.md", "plan authored ahead");
            Write(dir, "stray-foreign-file.txt", "not a feature produces doc");

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => service.Stamp("plan", Feature, baseRef: null));

            // Fail-closed scope preserved: the foreign untracked path still blocks (the exclusion is scoped to the
            // feature's owned produces paths, not a blanket untracked bypass).
            Assert.Contains("untracked changes present", ex.Message, StringComparison.OrdinalIgnoreCase);
            // The cycle did NOT advance — it is still at specify.
            Assert.Equal("specify", new CycleStateStore(dir).Read()!.CurrentStage);
        }
        finally { DeleteDir(dir); }
    }

    // A committed cycle at "specify" (stamped, fresh) on a clean tree — the precondition for stamping into "plan".
    private static CycleService SetupAtSpecify(string dir)
    {
        WriteWorkflow(dir);
        string specHash = Write(dir, "docs/specs/001-test.md", "spec");
        // A committed sibling under docs/plans/ so the dir is tracked — mirrors a real repo where prior features'
        // plans live there, so git reports a newly-authored plan doc by its FULL path (not a collapsed `docs/plans/`
        // dir). This is the scenario FR-016 targets: the ahead-authored produces doc surfaces as its own owned path.
        Write(dir, "docs/plans/.gitkeep", "");
        // Ignore the Doti runtime state exactly as a real repo does (DotiGitIgnore.RuntimeStateEntries) so the
        // cycle-state.json the store writes is not itself an untracked "foreign" change that would block.
        Write(dir, ".gitignore", ".doti/cycle-state.json\n.doti/gate-proof.json\n");
        Git(dir, "add", "-A");
        Git(dir, "commit", "-q", "-m", "init");
        string head = Git(dir, "rev-parse", "HEAD");

        var state = new CycleState(JsonContractDefaults.SchemaVersion, Feature, head, "specify",
        [
            new CycleStageProof("specify", CycleStageOutcome.Stamped, "id", [specHash], head, null, []),
        ]);
        new CycleStateStore(dir).Write(state);
        return new CycleService(dir);
    }

    private static void WriteWorkflow(string dir)
    {
        string path = Path.Combine(dir, ".doti", "workflows", "doti", "workflow.yml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path,
            "schemaVersion: 2\nname: t\nstages:\n" +
            "  - id: specify\n    command: 01-doti-specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n" +
            "  - id: plan\n    command: 03-doti-plan\n    kind: doc\n    produces: docs/plans/{feature}-plan.md\n    prereqs: [specify]\n");
    }

    private static string Write(string dir, string relative, string content)
    {
        string full = Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return CanonicalArtifactHasher.CanonicalHashOfText(content);
    }

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
        string dir = Path.Combine(Path.GetTempPath(), "hx-docscope-" + Guid.NewGuid().ToString("N"));
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
