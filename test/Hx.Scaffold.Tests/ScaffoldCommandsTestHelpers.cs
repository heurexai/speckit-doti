using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Hx.Doti.Core.ManagedAssets;
using Hx.Runner.Core.Process;
using Hx.Scaffold.Core.Update;
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

    private static string NewReleaseArchive(string version, out string checksum)
    {
        string root = NewTempDir("hx-release-src-");
        string payload = Path.Combine(root, $"speckit-doti-v{version}-win-x64");
        Directory.CreateDirectory(payload);
        WriteReleasePayload(payload);
        string archive = Path.Combine(root, $"speckit-doti-v{version}-win-x64.zip");
        ZipFile.CreateFromDirectory(payload, archive);
        checksum = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archive))).ToLowerInvariant()
            + "  " + Path.GetFileName(archive);
        return archive;
    }

    private static void WriteReleasePayload(string payload)
    {
        Directory.CreateDirectory(Path.Combine(payload, "doti", "core", "templates", "commands"));
        Directory.CreateDirectory(Path.Combine(payload, "doti", "profiles", "dotnet-cli"));
        Directory.CreateDirectory(Path.Combine(payload, ".doti", "workflows", "doti"));
        File.WriteAllText(Path.Combine(payload, "doti", "core", "skills.json"),
            """
            {
              "schemaVersion": 1,
              "maturity": "command-aware-advisory",
              "commandTemplateDir": "doti/core/templates/commands",
              "agentContextRef": ".doti/agent-context.md",
              "introTemplate": "Read `{agentContextRef}`, then follow `{commandTemplate}`.",
              "skills": [
                { "name": "doti-specify", "description": "New Spec.", "argumentHint": "[goal]", "highlights": [], "nextStage": "Run `/doti-clarify`." }
              ]
            }
            """);
        File.WriteAllText(Path.Combine(payload, "doti", "profiles", "dotnet-cli", "profile.json"),
            """{ "selfHostingStatus": { "commandAvailabilityFootnote": "Footnote.", "rootMaturityNote": "Maturity." } }""");
        File.WriteAllText(Path.Combine(payload, "doti", "core", "templates", "agent-context-template.md"), "new context\n");
        File.WriteAllText(Path.Combine(payload, "doti", "core", "templates", "commands", "doti-specify.md"), "# new command\n");
        File.WriteAllText(Path.Combine(payload, ".doti", "workflows", "doti", "workflow.yml"),
            "schemaVersion: 2\nstages:\n  - id: clarify\n    prereqs: []\n");
    }

    private static ScaffoldUpdateServices FakeReleaseServices(
        string cacheRoot,
        string archive,
        string checksum,
        Action? onDownload = null,
        string? worktreeRoot = null) =>
        new()
        {
            ResolveLatest = _ => new UpdateRelease(
                "v1.0.0",
                "1.0.0",
                new UpdateReleaseAsset(Path.GetFileName(archive), new Uri("https://github.com/heurexai/speckit-doti/releases/download/v1.0.0/" + Path.GetFileName(archive))),
                new UpdateReleaseAsset(Path.GetFileName(archive) + ".sha256", new Uri("https://github.com/heurexai/speckit-doti/releases/download/v1.0.0/" + Path.GetFileName(archive) + ".sha256"))),
            DownloadBytes = uri =>
            {
                onDownload?.Invoke();
                return uri.AbsolutePath.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase)
                    ? Encoding.UTF8.GetBytes(checksum)
                    : File.ReadAllBytes(archive);
            },
            CacheRoot = () => cacheRoot,
            WorktreeRoot = () => worktreeRoot ?? NewTempDir("hx-update-worktrees-"),
        };

    private static string NewVersionedGitRepo()
    {
        string repo = NewVersionedRepo();
        Git(repo, "init", "-q");
        Git(repo, "config", "user.email", "t@example.com");
        Git(repo, "config", "user.name", "Test");
        Git(repo, "config", "commit.gpgsign", "false");
        Git(repo, "add", "-A");
        Git(repo, "commit", "-q", "-m", "seed");
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
