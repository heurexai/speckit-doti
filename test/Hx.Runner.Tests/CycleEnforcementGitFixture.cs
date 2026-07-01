using Hx.Runner.Core.Process;

namespace Hx.Runner.Tests;

public sealed partial class CycleEnforcementTests
{
    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-cycle15-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void ForceDelete(string dir)
    {
        if (!Directory.Exists(dir))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            try { File.SetAttributes(file, FileAttributes.Normal); }
            catch { /* best-effort */ }
        }

        try { Directory.Delete(dir, recursive: true); }
        catch (IOException) { /* temp dir; the OS reclaims it */ }
        catch (UnauthorizedAccessException) { /* temp dir; the OS reclaims it */ }
    }

    private static void Git(string dir, params string[] args)
    {
        ProcessRunResult r = ProcessRunner.Run(new ToolCommand("git", args, dir));
        if (r.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {r.StandardError.Trim()}");
        }
    }

    private static string InitRepo()
    {
        string dir = NewTempDir();
        Git(dir, "init", "-q");
        Git(dir, "config", "user.email", "t@example.com");
        Git(dir, "config", "user.name", "Test");
        Git(dir, "config", "commit.gpgsign", "false");

        string wfDir = Path.Combine(dir, ".doti", "workflows", "doti");
        Directory.CreateDirectory(wfDir);
        File.WriteAllText(Path.Combine(wfDir, "workflow.yml"),
            "schemaVersion: 2\nstages:\n  - id: specify\n    kind: doc\n    produces: docs/specs/{feature}.md\n    prereqs: []\n  - id: drift-review\n    kind: review\n    prereqs: [specify]\n    next: [release, specify]\n  - id: release\n    kind: release\n    prereqs: [drift-review]\n");
        File.WriteAllText(Path.Combine(dir, "test.slnx"), "<Solution></Solution>\n");
        File.WriteAllText(Path.Combine(dir, ".gitignore"), ".doti/cycle-state.json\n.doti/gate-proof.json\n");
        File.WriteAllText(Path.Combine(dir, "seed.txt"), "seed");
        Git(dir, "add", "-A");
        Git(dir, "commit", "-q", "-m", "seed");
        return dir;
    }

    private static string GitHead(string dir)
    {
        ProcessRunResult r = ProcessRunner.Run(new ToolCommand("git", ["rev-parse", "HEAD"], dir));
        if (r.ExitCode != 0)
        {
            throw new InvalidOperationException(r.StandardError.Trim());
        }

        return r.StandardOutput.Trim();
    }

    private static string GitOut(string dir, params string[] args) =>
        ProcessRunner.Run(new ToolCommand("git", args, dir)).StandardOutput;

    private static bool IsTracked(string dir, string rel) =>
        ProcessRunner.Run(new ToolCommand("git", ["ls-files", "--error-unmatch", "--", rel], dir)).ExitCode == 0;

    // InitRepo with a caller-supplied `stages:` block (for the WI1 same-produces regression test); overwrites the
    // default model and re-commits so `new CycleService(dir)` loads it.
    private static string InitRepoWithStages(string stagesYaml)
    {
        string dir = InitRepo();
        File.WriteAllText(
            Path.Combine(dir, ".doti", "workflows", "doti", "workflow.yml"),
            "schemaVersion: 2\nstages:\n" + stagesYaml);
        Git(dir, "add", "-A");
        Git(dir, "commit", "-q", "-m", "model");
        return dir;
    }
}
