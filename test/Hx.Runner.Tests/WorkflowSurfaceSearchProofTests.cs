using Hx.Doti.Core.Workflow;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class WorkflowSurfaceSearchProofTests
{
    [Fact]
    public void Live_workflow_surfaces_do_not_expose_removed_commit_or_release_root_controls()
    {
        string repo = FindRepoRoot();
        string[] forbidden =
        [
            "/doti-commit",
            "doti cycle commit",
            "--release-root",
            "--release-root-env",
            "--save-release-root",
            "source-archive-only",
            "archive-only release"
        ];

        List<string> violations = [];
        foreach (string file in LiveSurfaceFiles(repo))
        {
            string text = File.ReadAllText(file);
            foreach (string token in forbidden)
            {
                if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{Path.GetRelativePath(repo, file)} contains {token}");
                }
            }
        }

        Assert.Empty(violations);

        CliDescribeWorkflow workflow = DotiWorkflowDescribe.Build(repo);
        Assert.DoesNotContain(workflow.Stages, stage => stage.CommandName.Contains("commit", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(workflow.Stages, stage => stage.StageId.Contains("commit", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> LiveSurfaceFiles(string repo)
    {
        yield return Path.Combine(repo, "README.md");
        yield return Path.Combine(repo, "AGENTS.md");
        yield return Path.Combine(repo, "CLAUDE.md");
        yield return Path.Combine(repo, ".doti", "agent-context.md");
        yield return Path.Combine(repo, ".doti", "profiles", "dotnet-cli", "profile.json");

        foreach (string directory in new[]
        {
            Path.Combine(repo, ".doti", "core", "templates", "commands"),
            Path.Combine(repo, ".agents", "skills"),
            Path.Combine(repo, ".claude", "skills")
        })
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*.md", SearchOption.AllDirectories))
            {
                yield return file;
            }
        }
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "scaffold-dotnet.slnx"))
                && File.Exists(Path.Combine(dir.FullName, ".doti", "core", "skills.json")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the speckit-doti repo root.");
    }
}
