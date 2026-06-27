using System.Diagnostics;
using Hx.Cycle.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Cycle.Tests;

/// <summary>
/// T008: <c>doti cycle refresh --apply-safe</c> re-stamps ONLY the SafeReinterpret stale prerequisites (reusing the
/// Stamp path); a RerunRequired step is left untouched and reported — no stale-loop dead end. Git-backed because the
/// re-stamp path computes the change-set identity from the repository.
/// </summary>
public sealed class RefreshTests
{
    [Fact]
    public void ApplySafe_restamps_a_missing_artifact_binding_prereq_and_clears_it()
    {
        string dir = NewGitRepo();
        try
        {
            CycleService service = Setup(dir, planArtifactHashes: []); // present file + empty binding ⇒ MissingArtifactBinding

            CycleRefreshResult result = service.Refresh("tasks", applySafe: true);

            Assert.Contains("plan", result.Refreshed);
            Assert.Empty(result.Remaining); // plan now fresh; specify was already fresh
            Assert.DoesNotContain(service.Status().Freshness,
                f => f.Stage == "plan" && f.Freshness == StageFreshness.Stale);
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void ApplySafe_leaves_a_rerun_required_prereq_and_reports_it()
    {
        string dir = NewGitRepo();
        try
        {
            CycleService service = Setup(dir, planArtifactHashes: ["a-stale-hash"]); // present + mismatch ⇒ OwnArtifactChanged

            CycleRefreshResult result = service.Refresh("tasks", applySafe: true);

            Assert.DoesNotContain("plan", result.Refreshed);
            StageRecoveryStep blocker = Assert.Single(result.Remaining, s => s.Stage == "plan");
            Assert.Equal(RestampSafety.RerunRequired, blocker.Safety);
        }
        finally { DeleteDir(dir); }
    }

    [Fact]
    public void Dry_run_does_not_mutate_and_surfaces_the_safe_step()
    {
        string dir = NewGitRepo();
        try
        {
            CycleService service = Setup(dir, planArtifactHashes: []);

            CycleRefreshResult result = service.Refresh("tasks", applySafe: false);

            Assert.Empty(result.Refreshed);
            Assert.Contains(result.Remaining, s => s.Stage == "plan" && s.Safety == RestampSafety.SafeReinterpret);
            Assert.Contains(service.Status().Freshness,
                f => f.Stage == "plan" && f.Freshness == StageFreshness.Stale); // still stale — not applied
        }
        finally { DeleteDir(dir); }
    }

    private const string Feature = "001-test";

    // Build a cycle at "tasks" with specify FRESH and plan stale per the supplied own-artifact binding.
    private static CycleService Setup(string dir, IReadOnlyList<string> planArtifactHashes)
    {
        WriteWorkflow(dir);
        string specHash = Write(dir, "docs/specs/001-test.md", "spec");
        string planHash = Write(dir, "docs/plans/001-test-plan.md", "plan");
        string tasksHash = Write(dir, "docs/tasks/001-test-tasks.md", "tasks");
        Git(dir, "add", "-A");
        Git(dir, "commit", "-q", "-m", "init");
        string head = Git(dir, "rev-parse", "HEAD");

        CycleStageProof Proof(string stage, IReadOnlyList<string> art, IReadOnlyList<string> prereq) =>
            new(stage, CycleStageOutcome.Stamped, "id", art, head, null, prereq);

        var state = new CycleState(1, Feature, head, "tasks",
        [
            Proof("specify", [specHash], []),
            Proof("plan", planArtifactHashes, [$"docs/specs/001-test.md:{specHash}"]),
            Proof("tasks", [tasksHash], [$"docs/plans/001-test-plan.md:{planHash}", $"docs/specs/001-test.md:{specHash}"]),
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
            "  - id: plan\n    command: 03-doti-plan\n    kind: doc\n    produces: docs/plans/{feature}-plan.md\n    prereqs: [specify]\n" +
            "  - id: tasks\n    command: 04-doti-tasks\n    kind: doc\n    produces: docs/tasks/{feature}-tasks.md\n    prereqs: [plan]\n");
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
        string dir = Path.Combine(Path.GetTempPath(), "hx-refresh-" + Guid.NewGuid().ToString("N"));
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
