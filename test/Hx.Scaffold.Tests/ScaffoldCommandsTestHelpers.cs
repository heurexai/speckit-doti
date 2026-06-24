using Hx.Doti.Core.ManagedAssets;
using Hx.Runner.Core.Process;
using Hx.Scaffold.Core.Versioning;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Tests;

public sealed partial class ScaffoldCommandsTests
{
    private static string NewVersionedRepo()
    {
        string repo = Path.Combine(Path.GetTempPath(), "hx-scaffold-version-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(Path.Combine(repo, ".doti", "workflows", "doti"));
        Directory.CreateDirectory(Path.Combine(repo, ".agents", "skills", "doti-specify"));
        string workflow = Path.Combine(repo, ".doti", "workflows", "doti", "workflow.yml");
        string skill = Path.Combine(repo, ".agents", "skills", "doti-specify", "SKILL.md");
        File.WriteAllText(workflow, "schemaVersion: 2\nstages:\n  - id: specify\n");
        File.WriteAllText(skill, "# skill\n");

        ScaffoldVersionReporter.WriteStamp(repo, ScaffoldVersionReporter.IdentityFromVersion("1.0.0", "test"));
        ManagedAssetManifestStore.Write(repo, new ManagedAssetManifest(
            JsonContractDefaults.SchemaVersion,
            [
                Entry(repo, ".doti/workflows/doti/workflow.yml", ManagedAssetCategory.WorkflowTemplate),
                Entry(repo, ".agents/skills/doti-specify/SKILL.md", ManagedAssetCategory.SkillGeneratedInstruction),
            ]));
        return repo;
    }

    private static string NewTempDir(string prefix)
    {
        string dir = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Git(string dir, params string[] args)
    {
        ProcessRunResult r = ProcessRunner.Run(new ToolCommand("git", args, dir));
        if (r.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {r.StandardError.Trim()}");
        }
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
            catch { /* best-effort temp cleanup */ }
        }

        try { Directory.Delete(dir, recursive: true); }
        catch (IOException) { /* temp dir; OS cleanup is enough */ }
        catch (UnauthorizedAccessException) { /* temp dir; OS cleanup is enough */ }
    }

    private static ManagedAssetHashEntry Entry(string repo, string relativePath, string category)
    {
        string full = Path.Combine(repo, relativePath.Replace('/', Path.DirectorySeparatorChar));
        string profile = CanonicalContentHasher.ProfileForPath(relativePath);
        CanonicalHash hash = CanonicalContentHasher.HashFile(full, profile);
        return new ManagedAssetHashEntry(relativePath, category, profile, hash.Sha256);
    }
}
