using System.Text.Json;
using Hx.Doti.Core.ManagedAssets;
using Hx.Tooling.Contracts;

namespace Hx.Doti.Core;

/// <summary>
/// Installs the full Doti workflow asset set into a target repo so it is self-hosting exactly like the
/// scaffold: copies the <c>doti/</c> source tree (so the installed skills' <c>doti/core/...</c>
/// references resolve and the repo can re-render), copies the static <c>.doti/</c> installed bits,
/// renders the skills + agent context + root entrypoints, and writes the repo-specific integration
/// metadata. Used by <c>Hx.Runner.Cli doti install</c> and by the scaffold-CLI finisher.
/// </summary>
public static class DotiInstaller
{
    private static readonly string[] StaticDotiSubdirectories = ["templates", "memory", "workflows", "integrations"];

    public static DotiInstallResult Install(
        string sourceRepoRoot, string targetRepoRoot, IReadOnlyList<DotiAgentTarget> agents, string repoName)
    {
        string sourceDoti = Path.Combine(sourceRepoRoot, "doti");
        if (!Directory.Exists(Path.Combine(sourceDoti, "core")))
        {
            throw new DirectoryNotFoundException(
                $"Doti source is missing at '{Path.Combine(sourceDoti, "core")}'; run install from the scaffold repo root.");
        }

        // 1. Copy the doti/ source tree so the generated repo is self-hosting: the installed skills'
        //    doti/core/templates/commands/*.md references resolve, and `doti render-skills` can run.
        CopyDirectory(sourceDoti, Path.Combine(targetRepoRoot, "doti"));

        // 2. Copy the static installed .doti bits. agent-context.md + skills are rendered (step 3);
        //    integration.json / init-options.json are repo-specific (step 4).
        var copied = new List<string>();
        foreach (string sub in StaticDotiSubdirectories)
        {
            string from = Path.Combine(sourceRepoRoot, ".doti", sub);
            if (Directory.Exists(from))
            {
                CopyDirectory(from, Path.Combine(targetRepoRoot, ".doti", sub));
                copied.Add($".doti/{sub}");
            }
        }

        // 3. Render agent context + skills + root entrypoints (the target now has doti/core).
        DotiRenderResult render = DotiRenderer.Render(targetRepoRoot, agents, check: false);

        // 4. Repo-specific metadata.
        WriteMetadata(targetRepoRoot, repoName, agents);
        CopyPrerequisitePolicy(sourceRepoRoot, targetRepoRoot);
        ManagedAssetScanner.WriteBaseline(targetRepoRoot, DotiRenderer.BuildTargets(targetRepoRoot, agents));

        return new DotiInstallResult(JsonContractDefaults.SchemaVersion, render.Outcome, render.Written, copied);
    }

    private static void WriteMetadata(string targetRepoRoot, string repoName, IReadOnlyList<DotiAgentTarget> agents)
    {
        JsonSerializerOptions options = JsonContractSerializerOptions.Create();
        options.WriteIndented = true;
        string[] agentKeys = agents.Select(a => a.Key).ToArray();
        string dotiDir = Path.Combine(targetRepoRoot, ".doti");
        Directory.CreateDirectory(dotiDir);

        var integration = new DotiIntegration(
            JsonContractDefaults.SchemaVersion, repoName, "dotnet-cli", "command-aware-advisory",
            agentKeys, ".doti/agent-context.md", ".doti/workflows/doti/workflow.yml",
            ".doti/memory/constitution.md", new DotiGeneratedBy(8, "scaffold-cli-new"));
        File.WriteAllText(Path.Combine(dotiDir, "integration.json"), JsonSerializer.Serialize(integration, options));

        var init = new DotiInitOptions(
            JsonContractDefaults.SchemaVersion, "dotnet-cli", agentKeys, "command-aware-advisory",
            "doti/profiles/dotnet-cli/profile.json");
        File.WriteAllText(Path.Combine(dotiDir, "init-options.json"), JsonSerializer.Serialize(init, options));
    }

    private static void CopyPrerequisitePolicy(string sourceRepoRoot, string targetRepoRoot)
    {
        string source = Path.Combine(sourceRepoRoot, "doti", "core", "prerequisites.json");
        if (!File.Exists(source))
        {
            return;
        }

        string target = Path.Combine(targetRepoRoot, ".doti", "prerequisites.json");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target, overwrite: true);
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (string file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), overwrite: true);
        }

        foreach (string sub in Directory.GetDirectories(sourceDir))
        {
            CopyDirectory(sub, Path.Combine(targetDir, Path.GetFileName(sub)));
        }
    }
}
