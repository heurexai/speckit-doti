using System.Text.Json;
using Hx.Doti.Core.ManagedAssets;
using Hx.Tooling.Contracts;

namespace Hx.Doti.Core;

/// <summary>
/// Installs the full Doti workflow asset set into a target repo so it is self-hosting exactly like the
/// scaffold: copies the supported <c>.doti/</c> source/payload trees, renders the skills + agent
/// context + root entrypoints, removes manifest-proven obsolete legacy <c>doti/</c> assets, and
/// writes the repo-specific integration metadata. Used by <c>Hx.Runner.Cli doti install</c> and by
/// the scaffold-CLI finisher.
/// </summary>
public static class DotiInstaller
{
    private static readonly string[] StaticDotiSubdirectories = ["core", "profiles", "templates", "memory", "workflows", "integrations"];

    public static DotiInstallResult Install(
        string sourceRepoRoot,
        string targetRepoRoot,
        IReadOnlyList<DotiAgentTarget> agents,
        string repoName,
        bool force = false)
    {
        DotiTargetClassification classification = DotiTargetClassifier.Classify(targetRepoRoot);
        bool targetCreated = !classification.Exists;
        Directory.CreateDirectory(targetRepoRoot);

        string sourceDoti = Path.Combine(sourceRepoRoot, ".doti");
        if (!File.Exists(Path.Combine(sourceDoti, "core", "skills.json")))
        {
            throw new DirectoryNotFoundException(
                $"Doti source is missing at '{Path.Combine(sourceDoti, "core", "skills.json")}'; run install from the scaffold repo root.");
        }

        // 1. Copy the supported .doti payload. agent-context.md + skills are rendered (step 2);
        //    integration.json / init-options.json are repo-specific (step 4).
        var copied = new List<string>();
        var installed = new List<DotiInstallPathEffect>();
        var preserved = new List<DotiInstallPathEffect>();
        var removed = new List<DotiInstallPathEffect>();
        var skipped = new List<DotiInstallPathEffect>();
        var blocked = new List<DotiInstallPathEffect>();

        foreach (string sub in StaticDotiSubdirectories)
        {
            string from = Path.Combine(sourceRepoRoot, ".doti", sub);
            if (Directory.Exists(from))
            {
                CopyDirectory(from, Path.Combine(targetRepoRoot, ".doti", sub));
                copied.Add($".doti/{sub}");
                installed.Add(new DotiInstallPathEffect($".doti/{sub}", "managed Doti static asset set installed"));
            }
        }

        // 2. Render agent context + skills + root entrypoints from .doti/core.
        DotiRenderResult render = DotiRenderer.Render(targetRepoRoot, agents, check: false);
        installed.AddRange(render.Written.Select(path => new DotiInstallPathEffect(path, "rendered from Doti workflow source")));

        // 3. Repo-specific metadata.
        WriteMetadata(targetRepoRoot, repoName, agents, classification.Classification);
        installed.Add(new DotiInstallPathEffect(".doti/integration.json", "repo-specific Doti integration metadata written"));
        installed.Add(new DotiInstallPathEffect(".doti/init-options.json", "repo-specific Doti init options written"));
        CopyPrerequisitePolicy(sourceRepoRoot, targetRepoRoot);
        IReadOnlyList<string> gitIgnoreWrites = DotiGitIgnore.Ensure(targetRepoRoot);

        IReadOnlyList<ManagedAssetHashEntry> obsoleteAssets = RemoveObsoleteLegacyDotiAssets(
            targetRepoRoot, force, removed, preserved, blocked);

        ManagedAssetScanner.WriteBaseline(targetRepoRoot, DotiRenderer.BuildTargets(targetRepoRoot, agents), obsoleteAssets);
        installed.Add(new DotiInstallPathEffect(ManagedAssetManifestStore.RelativePath, "canonical managed-asset baseline written"));
        copied.AddRange(gitIgnoreWrites);
        installed.AddRange(gitIgnoreWrites.Select(path => new DotiInstallPathEffect(path, "Doti runtime state ignore entries ensured")));

        string? nextStep = classification.Classification is DotiInstallClassification.InstalledNewTarget
            or DotiInstallClassification.InstalledEmptyTarget
            ? "Run `hx new --output <target> --name <project-name>` or the scaffold creation command for this repository."
            : null;

        return new DotiInstallResult(
            JsonContractDefaults.SchemaVersion,
            render.Outcome,
            classification.Classification,
            targetCreated,
            nextStep,
            render.Written,
            copied,
            installed,
            preserved,
            removed,
            skipped,
            blocked);
    }

    private static void WriteMetadata(
        string targetRepoRoot, string repoName, IReadOnlyList<DotiAgentTarget> agents, string classification)
    {
        JsonSerializerOptions options = JsonContractSerializerOptions.Create();
        options.WriteIndented = true;
        string[] agentKeys = agents.Select(a => a.Key).ToArray();
        string dotiDir = Path.Combine(targetRepoRoot, ".doti");
        Directory.CreateDirectory(dotiDir);

        string profile = ResolveInstallProfile(classification, targetRepoRoot);
        var integration = new DotiIntegration(
            JsonContractDefaults.SchemaVersion, repoName, profile, "command-aware-advisory",
            agentKeys, ".doti/agent-context.md", ".doti/workflows/doti/workflow.yml",
            ".doti/memory/constitution.md", new DotiGeneratedBy(8, "scaffold-cli-new"));
        File.WriteAllText(Path.Combine(dotiDir, "integration.json"), JsonSerializer.Serialize(integration, options));

        var init = new DotiInitOptions(
            JsonContractDefaults.SchemaVersion, profile, agentKeys, "command-aware-advisory",
            $".doti/profiles/{profile}/profile.json");
        File.WriteAllText(Path.Combine(dotiDir, "init-options.json"), JsonSerializer.Serialize(init, options));
    }

    /// <summary>
    /// FR-030: <c>doti install --repo</c> must not impose the Heurex (Tier-3) structure on existing foreign code.
    /// An existing non-Doti repo gets the non-imposing <c>workflow-only</c> tier; an upgrade preserves the repo's
    /// already-declared tier; a new/empty scaffold target adopts the full <c>dotnet-cli</c> (Tier-3) tier.
    /// </summary>
    internal static string ResolveInstallProfile(string classification, string targetRepoRoot)
    {
        if (classification == DotiInstallClassification.InstalledNonEmptyNonDotiTarget)
        {
            return "workflow-only";
        }

        if (classification == DotiInstallClassification.UpgradedExistingDotiRepo)
        {
            return ReadExistingProfile(targetRepoRoot) ?? "dotnet-cli";
        }

        return "dotnet-cli";
    }

    private static string? ReadExistingProfile(string targetRepoRoot)
    {
        string path = Path.Combine(targetRepoRoot, ".doti", "integration.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.TryGetProperty("profile", out JsonElement p) ? p.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void CopyPrerequisitePolicy(string sourceRepoRoot, string targetRepoRoot)
    {
        string source = Path.Combine(sourceRepoRoot, ".doti", "core", "prerequisites.json");
        if (!File.Exists(source))
        {
            return;
        }

        string target = Path.Combine(targetRepoRoot, ".doti", "prerequisites.json");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target, overwrite: true);
    }

    private static IReadOnlyList<ManagedAssetHashEntry> RemoveObsoleteLegacyDotiAssets(
        string targetRepoRoot,
        bool force,
        List<DotiInstallPathEffect> removed,
        List<DotiInstallPathEffect> preserved,
        List<DotiInstallPathEffect> blocked)
    {
        string legacyRoot = Path.Combine(targetRepoRoot, "doti");
        if (!Directory.Exists(legacyRoot))
        {
            return [];
        }

        ManagedAssetManifest? existingManifest = ManagedAssetManifestStore.Read(targetRepoRoot);
        IReadOnlyList<ManagedAssetHashEntry> obsoleteEntries = (existingManifest?.ObsoleteAssets ?? [])
            .Concat(existingManifest?.Assets.Where(a => a.Path.StartsWith("doti/", StringComparison.OrdinalIgnoreCase)) ?? [])
            .Where(a => a.Path.StartsWith("doti/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.Path.Length)
            .ToArray();

        if (obsoleteEntries.Count == 0 && !force)
        {
            blocked.Add(new DotiInstallPathEffect("doti/", "legacy root doti directory has no managed baseline; rerun with --force only when replacement is intended"));
            return [];
        }

        if (force)
        {
            DeleteDirectoryInside(targetRepoRoot, "doti");
            removed.Add(new DotiInstallPathEffect("doti/", "legacy root doti directory force-removed after current .doti payload was installed"));
            return obsoleteEntries;
        }

        foreach (ManagedAssetHashEntry entry in obsoleteEntries.OrderByDescending(e => e.Path.Length))
        {
            string full = ResolveInside(targetRepoRoot, entry.Path);
            if (!File.Exists(full))
            {
                continue;
            }

            CanonicalHash current = CanonicalContentHasher.HashFile(full, entry.HashProfile);
            if (!string.Equals(current.Sha256, entry.Sha256, StringComparison.Ordinal))
            {
                blocked.Add(new DotiInstallPathEffect(entry.Path, "obsolete managed Doti asset was modified; refusing removal without --force"));
                continue;
            }

            File.Delete(full);
            removed.Add(new DotiInstallPathEffect(entry.Path, "obsolete managed Doti asset removed after canonical baseline match"));
        }

        RemoveEmptyDirectories(targetRepoRoot, legacyRoot, removed);
        if (Directory.Exists(legacyRoot))
        {
            preserved.Add(new DotiInstallPathEffect("doti/", "legacy root doti directory still contains unproven or repo-owned content"));
        }

        return obsoleteEntries;
    }

    private static void RemoveEmptyDirectories(string repoRoot, string root, List<DotiInstallPathEffect> removed)
    {
        foreach (string directory in Directory.GetDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }

        if (!Directory.EnumerateFileSystemEntries(root).Any())
        {
            Directory.Delete(root);
            removed.Add(new DotiInstallPathEffect("doti/", "obsolete legacy root doti directory removed after all managed files matched baseline"));
        }
    }

    private static void DeleteDirectoryInside(string repoRoot, string relativePath)
    {
        string full = ResolveInside(repoRoot, relativePath);
        if (Directory.Exists(full))
        {
            Directory.Delete(full, recursive: true);
        }
    }

    private static string ResolveInside(string repoRoot, string relativePath)
    {
        string root = Path.GetFullPath(repoRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Doti managed path escapes repository root: {relativePath}");
        }

        return full;
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
