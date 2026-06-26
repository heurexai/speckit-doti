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
        string payloadRoot,
        string targetRepoRoot,
        IReadOnlyList<DotiAgentTarget> agents,
        string repoName,
        bool force = false)
    {
        DotiTargetClassification classification = DotiTargetClassifier.Classify(targetRepoRoot);
        bool targetCreated = !classification.Exists;
        Directory.CreateDirectory(targetRepoRoot);

        string payloadDoti = Path.Combine(payloadRoot, ".doti");
        if (!File.Exists(Path.Combine(payloadDoti, "core", "skills.json")))
        {
            throw new DirectoryNotFoundException(
                $"Doti payload is missing at '{Path.Combine(payloadDoti, "core", "skills.json")}'; the installed hx payload beside the executable is incomplete.");
        }

        // 007 T030: version-aware reconciliation. Refuse a repo whose recorded .doti payload is AHEAD of the bundled
        // payload (no silent downgrade); absent/equal/older all run the conflict-aware forward copy below.
        string? bundledVersion = ReadBundledPayloadVersion(payloadRoot);
        string? repoVersion = ReadRepoPayloadVersion(targetRepoRoot);
        if (bundledVersion is not null && repoVersion is not null
            && ComparePayloadVersions(repoVersion, bundledVersion) > 0)
        {
            throw new InvalidOperationException(
                $"Doti repo payload version '{repoVersion}' is ahead of the bundled payload version '{bundledVersion}'; " +
                "refusing to downgrade this repo's .doti assets (Integrity_DotiRepoPayloadAhead).");
        }

        // The existing managed-asset baseline drives per-file preservation; scanning it fails closed on an escaping path.
        IReadOnlyDictionary<string, ManagedAssetStatus> managed = ScanExistingManagedAssets(targetRepoRoot);

        // 1. Reconcile the supported .doti payload into the target (operator edits preserved, never blind-overwritten).
        //    agent-context.md + skills are rendered (step 2); integration.json / init-options.json are repo-specific.
        var copied = new List<string>();
        var installed = new List<DotiInstallPathEffect>();
        var preserved = new List<DotiInstallPathEffect>();
        var removed = new List<DotiInstallPathEffect>();
        var skipped = new List<DotiInstallPathEffect>();
        var blocked = new List<DotiInstallPathEffect>();

        foreach (string sub in StaticDotiSubdirectories)
        {
            string from = Path.Combine(payloadRoot, ".doti", sub);
            if (Directory.Exists(from))
            {
                int installedCount = ReconcileManagedTree(
                    payloadRoot, targetRepoRoot, from, managed, force, installed, preserved, skipped, blocked);
                copied.Add($".doti/{sub}");
                if (installedCount > 0)
                {
                    installed.Add(new DotiInstallPathEffect($".doti/{sub}", "managed Doti static asset set installed"));
                }
            }
        }

        // 2. Render agent context + skills + root entrypoints from .doti/core.
        DotiRenderResult render = DotiRenderer.Render(targetRepoRoot, agents, check: false);
        installed.AddRange(render.Written.Select(path => new DotiInstallPathEffect(path, "rendered from Doti workflow source")));

        // 3. Repo-specific metadata.
        WriteMetadata(targetRepoRoot, repoName, agents, classification.Classification);
        installed.Add(new DotiInstallPathEffect(".doti/integration.json", "repo-specific Doti integration metadata written"));
        installed.Add(new DotiInstallPathEffect(".doti/init-options.json", "repo-specific Doti init options written"));
        CopyPrerequisitePolicy(payloadRoot, targetRepoRoot);
        IReadOnlyList<string> gitIgnoreWrites = DotiGitIgnore.Ensure(targetRepoRoot);

        IReadOnlyList<ManagedAssetHashEntry> obsoleteAssets = RemoveObsoleteLegacyDotiAssets(
            targetRepoRoot, force, removed, preserved, blocked);

        ManagedAssetScanner.WriteBaseline(targetRepoRoot, DotiRenderer.BuildTargets(targetRepoRoot, agents), obsoleteAssets);
        installed.Add(new DotiInstallPathEffect(ManagedAssetManifestStore.RelativePath, "canonical managed-asset baseline written"));
        copied.AddRange(gitIgnoreWrites);
        installed.AddRange(gitIgnoreWrites.Select(path => new DotiInstallPathEffect(path, "Doti runtime state ignore entries ensured")));

        // 007 T030: stamp .doti/payload.json with the bundled payload version (verbatim from the descriptor) so the
        // next install can branch absent/equal/older/newer. Written atomically so a crash re-runs to a clean state.
        if (bundledVersion is not null)
        {
            StampRepoPayload(targetRepoRoot, bundledVersion, ReadBundledToolVersion(payloadRoot) ?? bundledVersion);
            installed.Add(new DotiInstallPathEffect(".doti/payload.json", "repo payload version stamped from the bundled descriptor"));
        }

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

    private static void CopyPrerequisitePolicy(string payloadRoot, string targetRepoRoot)
    {
        string source = Path.Combine(payloadRoot, ".doti", "core", "prerequisites.json");
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

    /// <summary>
    /// 007 T030 (FR-015, SC-007/018): reconcile one bundled <c>.doti</c> subtree into the target. Every managed write
    /// routes through <see cref="ResolveInside"/> (fail-closed on escape). A managed asset the operator MODIFIED (per
    /// the baseline) or a pre-existing file with no baseline entry is preserved and the bundled version is staged as a
    /// <c>.new</c> sidecar; an operator-DELETED managed asset is not resurrected without <paramref name="force"/>; a
    /// modified template/skill blocks unless forced; clean / new files are installed. Returns the install count.
    /// </summary>
    private static int ReconcileManagedTree(
        string payloadRoot,
        string targetRepoRoot,
        string sourceDir,
        IReadOnlyDictionary<string, ManagedAssetStatus> managed,
        bool force,
        List<DotiInstallPathEffect> installed,
        List<DotiInstallPathEffect> preserved,
        List<DotiInstallPathEffect> skipped,
        List<DotiInstallPathEffect> blocked)
    {
        string payloadDoti = Path.Combine(payloadRoot, ".doti");
        int installedCount = 0;
        foreach (string sourceFile in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string rel = ".doti/" + Path.GetRelativePath(payloadDoti, sourceFile).Replace('\\', '/');
            string dest = ResolveInside(targetRepoRoot, rel);
            bool exists = File.Exists(dest);
            managed.TryGetValue(rel, out ManagedAssetStatus? status);

            if (status?.State == ManagedAssetState.Modified && !force)
            {
                if (status.UpdateConflictPolicy == "managed-replace-preserve-live-config")
                {
                    StageSidecar(targetRepoRoot, rel, sourceFile);
                    preserved.Add(new DotiInstallPathEffect(rel, "operator-modified managed asset preserved; bundled version staged as .new"));
                    continue;
                }

                blocked.Add(new DotiInstallPathEffect(rel, "modified managed template/skill blocked; rerun with --force to overwrite"));
                continue;
            }

            if (status?.State == ManagedAssetState.Missing && !force)
            {
                skipped.Add(new DotiInstallPathEffect(rel, "operator-deleted managed asset not resurrected without --force"));
                continue;
            }

            if (status is null && exists)
            {
                // Pre-existing operator/foreign content with no managed baseline (brownfield): never blind-overwrite.
                StageSidecar(targetRepoRoot, rel, sourceFile);
                preserved.Add(new DotiInstallPathEffect(rel, "pre-existing operator content preserved; bundled version staged as .new"));
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(sourceFile, dest, overwrite: true);
            installedCount++;
        }

        return installedCount;
    }

    private static void StageSidecar(string targetRepoRoot, string rel, string sourceFile)
    {
        string sidecar = ResolveInside(targetRepoRoot, rel + ".new");
        Directory.CreateDirectory(Path.GetDirectoryName(sidecar)!);
        File.Copy(sourceFile, sidecar, overwrite: true);
    }

    private static IReadOnlyDictionary<string, ManagedAssetStatus> ScanExistingManagedAssets(string targetRepoRoot)
    {
        if (ManagedAssetManifestStore.Read(targetRepoRoot) is null)
        {
            return new Dictionary<string, ManagedAssetStatus>(StringComparer.OrdinalIgnoreCase);
        }

        // Scan fails closed on a managed path that escapes the repository root (SC-018).
        ManagedAssetScanResult scan = ManagedAssetScanner.Scan(targetRepoRoot);
        return scan.Assets.ToDictionary(s => s.Path.Replace('\\', '/'), s => s, StringComparer.OrdinalIgnoreCase);
    }

    private static string? ReadBundledPayloadVersion(string payloadRoot) =>
        ReadPayloadDescriptor(payloadRoot)?.PayloadVersion is { Length: > 0 } v ? v : null;

    private static string? ReadBundledToolVersion(string payloadRoot) =>
        ReadPayloadDescriptor(payloadRoot)?.ToolVersion is { Length: > 0 } v ? v : null;

    private static PayloadDescriptor? ReadPayloadDescriptor(string payloadRoot)
    {
        string manifest = Path.Combine(payloadRoot, "payload.manifest.json");
        if (!File.Exists(manifest))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PayloadDescriptor>(File.ReadAllText(manifest), JsonContractSerializerOptions.Create());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadRepoPayloadVersion(string targetRepoRoot)
    {
        string path = Path.Combine(targetRepoRoot, ".doti", "payload.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            RepoPayloadStamp? stamp = JsonSerializer.Deserialize<RepoPayloadStamp>(
                File.ReadAllText(path), JsonContractSerializerOptions.Create());
            return stamp?.PayloadVersion is { Length: > 0 } v ? v : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void StampRepoPayload(string targetRepoRoot, string payloadVersion, string toolVersion)
    {
        string path = ResolveInside(targetRepoRoot, ".doti/payload.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        JsonSerializerOptions options = JsonContractSerializerOptions.Create();
        options.WriteIndented = true;
        string json = JsonSerializer.Serialize(
            new RepoPayloadStamp(RepoPayloadStamp.CurrentSchemaVersion, payloadVersion, toolVersion), options);

        string temp = path + ".tmp-" + Guid.NewGuid().ToString("n");
        File.WriteAllText(temp, json);
        File.Move(temp, path, overwrite: true);
    }

    // Compare payload versions by their numeric major.minor.patch core, tie-breaking on the full string so a
    // pre-release/build suffix still orders deterministically.
    private static int ComparePayloadVersions(string a, string b)
    {
        int core = VersionCore(a).CompareTo(VersionCore(b));
        return core != 0 ? core : string.CompareOrdinal(a, b);
    }

    private static Version VersionCore(string value)
    {
        string core = value;
        int cut = core.IndexOfAny(['-', '+']);
        if (cut >= 0)
        {
            core = core[..cut];
        }

        return Version.TryParse(core, out Version? parsed) ? parsed : new Version(0, 0, 0, 0);
    }
}
