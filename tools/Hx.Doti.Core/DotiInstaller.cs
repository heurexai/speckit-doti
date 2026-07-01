using System.Text.Json;
using Hx.Doti.Core.ManagedAssets;
using Hx.Doti.Core.Setup;
using Hx.Tooling.Contracts;
using Hx.Tooling.Contracts.Setup;

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

    // 032 D2(e): the vendored-tool dirs ManagedAssetScanner.DotiSourcePaths already scans into managed-assets.json
    // (category DotiSource) but that, until now, no consumer loop ever reconciled — `tools/{sub}/bin/` (the
    // gitignored, network-fetched exe) is excluded the same way the scanner excludes it, so a reconcile never
    // touches the binary; only the manifest/license/config/grammar files are managed.
    private static readonly string[] StaticToolSubdirectories = ["gitleaks", "sentrux", "gitversion"];

    public static DotiInstallResult Install(
        string payloadRoot,
        string targetRepoRoot,
        IReadOnlyList<DotiAgentTarget> agents,
        string repoName,
        bool force = false,
        string? projectNameOverride = null,
        ResolvedSetupConfig? setup = null)
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
        string? repoVersion = RepoPayloadStore.ReadPayloadVersion(targetRepoRoot);
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
        var mergePending = new List<DotiInstallPathEffect>();

        foreach (string sub in StaticDotiSubdirectories)
        {
            // FR-014/016: `.doti/templates` is MATERIALIZED from `.doti/core/templates` (single source), not copied
            // from a committed twin — handled after the obsolete-asset sweep so it is never reconciled or removed.
            if (string.Equals(sub, "templates", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ReconcileStaticSubdirectory(payloadRoot, targetRepoRoot, ".doti", sub, managed, force,
                copied, installed, preserved, skipped, blocked, mergePending,
                "managed Doti static asset set installed");
        }

        // 032 D2(e): the SAME conflict-aware reconcile for the vendored-tool dirs ManagedAssetScanner already scans
        // into managed-assets.json (category DotiSource) — gitleaks/sentrux/gitversion — closing the gap where a
        // stale vendored-tool manifest was recorded-managed but never actually reconciled (the ergon Sentrux v0.5.11
        // incident). `bin/` (the gitignored, network-fetched exe) is excluded by ReconcileToolSubdirectory exactly as
        // ManagedAssetScanner already excludes it from the baseline.
        foreach (string sub in StaticToolSubdirectories)
        {
            ReconcileToolSubdirectory(payloadRoot, targetRepoRoot, sub, managed, force,
                copied, installed, preserved, skipped, blocked, mergePending);
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

        FinalizeManagedBaseline(
            payloadRoot, targetRepoRoot, agents, gitIgnoreWrites, bundledVersion, force,
            copied, installed, preserved, removed, blocked);

        MaterializeRepoTemplates(payloadRoot, targetRepoRoot, copied);

        // 009 FR-009/010/015: initialize the constitution from the §1/§2 template (the payload excludes this repo's
        // own constitution), filling the auto-derived project name; preserve an operator-edited one (managed-asset).
        InitializeConstitution(targetRepoRoot, projectNameOverride, installed, preserved);

        // 029 FR-002/D8/D10: project the Install-subset setup config (the .doti-layer fields) + persist the intent.
        // D10 no-op fence: a null setup early-returns inside Project before any write (install no-config byte-identical).
        SetupConfigEffect? setupEffect = ProjectSetup(targetRepoRoot, setup);

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
            blocked,
            setupEffect,
            mergePending);
    }

    /// <summary>
    /// 029 FR-002/FR-003/D8/D10: run the Install-subset setup projection (the doti-layer writers — GitVersion seed,
    /// release env-var, constitution §2) and persist the repo-portable intent to <c>.doti/setup.json</c>. New-only
    /// fields reached here are reported as ignored (never silently dropped). The no-op fence is enforced by
    /// <see cref="SetupConfigProjector.Project"/> (a null <paramref name="setup"/> touches nothing → byte-identical).
    /// </summary>
    private static SetupConfigEffect? ProjectSetup(string targetRepoRoot, ResolvedSetupConfig? setup)
    {
        if (setup is null)
        {
            return null;
        }

        SetupProjectionResult projection = SetupConfigProjector.Project(
            setup, targetRepoRoot, SetupTargetWriters.ForInstall());
        string? persisted = SetupConfigStore.WriteFromResolved(targetRepoRoot, setup);
        IReadOnlyList<DotiInstallPathEffect> ignored = projection.Ignored
            .Select(i => new DotiInstallPathEffect(i.Key, i.Reason))
            .ToList();
        return new SetupConfigEffect(projection.Written, ignored, persisted);
    }

    /// <summary>
    /// Finalize the managed-asset baseline after the payload reconcile + render: sweep obsolete assets (legacy
    /// <c>doti/</c> twin + 027 FR-008 orphaned renamed skill dirs the new render no longer targets), write the
    /// canonical managed-asset baseline over the current render targets, record the gitignore effects, and stamp
    /// <c>.doti/payload.json</c> with the bundled version. Extracted from <see cref="Install"/> as one cohesive
    /// finalize step so <c>Install</c> stays within the Sentrux function-size budget.
    /// </summary>
    private static void FinalizeManagedBaseline(
        string payloadRoot,
        string targetRepoRoot,
        IReadOnlyList<DotiAgentTarget> agents,
        IReadOnlyList<string> gitIgnoreWrites,
        string? bundledVersion,
        bool force,
        List<string> copied,
        List<DotiInstallPathEffect> installed,
        List<DotiInstallPathEffect> preserved,
        List<DotiInstallPathEffect> removed,
        List<DotiInstallPathEffect> blocked)
    {
        IReadOnlyList<DotiRenderTarget> renderTargets = DotiRenderer.BuildTargets(targetRepoRoot, agents);

        IReadOnlyList<ManagedAssetHashEntry> legacyObsolete = RemoveObsoleteLegacyDotiAssets(
            targetRepoRoot, force, removed, preserved, blocked);

        // 027 FR-008: prune managed skill dirs the NEW render no longer targets (a stage renumber renames the dir
        // and would otherwise leave the old one), sourced from an on-disk scan so a pre-category repo with an empty
        // prior manifest is still covered; only baseline-clean files are deleted (operator-edited orphans preserved).
        IReadOnlyList<ManagedAssetHashEntry> orphanObsolete = PruneOrphanedManagedSkillDirs(
            targetRepoRoot, agents, renderTargets, force, removed, preserved, blocked);

        IReadOnlyList<ManagedAssetHashEntry> obsoleteAssets = [.. legacyObsolete, .. orphanObsolete];

        ManagedAssetScanner.WriteBaseline(targetRepoRoot, renderTargets, obsoleteAssets);
        installed.Add(new DotiInstallPathEffect(ManagedAssetManifestStore.RelativePath, "canonical managed-asset baseline written"));
        copied.AddRange(gitIgnoreWrites);
        installed.AddRange(gitIgnoreWrites.Select(path => new DotiInstallPathEffect(path, "Doti runtime state ignore entries ensured")));

        // 007 T030: stamp .doti/payload.json with the bundled payload version (verbatim from the descriptor) so the
        // next install can branch absent/equal/older/newer. Written atomically so a crash re-runs to a clean state.
        if (bundledVersion is not null)
        {
            RepoPayloadStore.Write(targetRepoRoot, bundledVersion, ReadBundledToolVersion(payloadRoot) ?? bundledVersion);
            installed.Add(new DotiInstallPathEffect(".doti/payload.json", "repo payload version stamped from the bundled descriptor"));
        }
    }

    /// <summary>
    /// 009 FR-009/010/015: initialize the target's <c>.doti/memory/constitution.md</c> from the installed §1/§2
    /// template (this repo's own constitution is excluded from the shipped payload, so a generated repo never inherits
    /// it), filling the auto-derived <c>{PROJECT_NAME}</c> title (an explicit <c>hx new --name</c> wins; else the
    /// solution/dir name). Managed-asset preservation: <see cref="ConstitutionInitializer"/> writes ONLY when absent,
    /// so a re-install over an operator-edited constitution preserves it (SC-006). Never blocks the install.
    /// </summary>
    private static void InitializeConstitution(
        string targetRepoRoot,
        string? projectNameOverride,
        List<DotiInstallPathEffect> installed,
        List<DotiInstallPathEffect> preserved)
    {
        string templatePath = Path.Combine(targetRepoRoot, ".doti", "core", "templates", "constitution-template.md");
        if (!File.Exists(templatePath))
        {
            return; // older payload without the structured template — nothing to initialize from; never block install.
        }

        string projectName = ProjectNameResolver.Resolve(targetRepoRoot, projectNameOverride);
        ConstitutionInitResult result = ConstitutionInitializer.Initialize(
            targetRepoRoot, File.ReadAllText(templatePath), projectName);
        if (result.Outcome == ConstitutionInitOutcome.Initialized)
        {
            installed.Add(new DotiInstallPathEffect(result.Path, $"constitution initialized from template ('{result.ProjectName}')"));
        }
        else
        {
            preserved.Add(new DotiInstallPathEffect(result.Path, "operator-authored constitution preserved"));
        }
    }

    /// <summary>
    /// FR-016: materialize <c>.doti/templates</c> from the payload's single-source <c>.doti/core/templates</c>, run
    /// AFTER the obsolete-asset sweep so the just-materialized copy is never removed. Installed repos keep
    /// <c>.doti/templates</c>; recorded as a COPIED effect only (035 (B): never an `installed` commit candidate).
    /// </summary>
    private static void MaterializeRepoTemplates(
        string payloadRoot, string targetRepoRoot, List<string> copied)
    {
        IReadOnlyList<string> materialized = DotiTemplateMaterializer.MaterializeFromTo(
            Path.Combine(payloadRoot, ".doti", "core", "templates"),
            Path.Combine(targetRepoRoot, ".doti", "templates"));
        if (materialized.Count > 0)
        {
            // 035 (B): `.doti/templates` is materialized, GITIGNORED runtime state — reported as copied, but NEVER
            // added to `installed` (the reconcile-commit candidate set via DotiReconcileCommit.TouchedPaths),
            // symmetric with .doti/cycle-state.json / gate-proof.json which are likewise never commit candidates.
            // Adding the bare gitignored dir made `git add -- .doti/templates` fail the whole sanctioned commit on a
            // consumer repo whose .gitignore uses the trailing-slash pattern `.doti/templates/`.
            copied.Add(".doti/templates");
        }
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

    /// <summary>
    /// 027 FR-008: prune managed skill dirs (category skill-generated-instruction, under an agent
    /// <see cref="DotiAgentTarget.SkillsRoot"/>) the new render no longer targets — the renumber-orphan fix. Candidate
    /// dirs come from an ON-DISK scan of each SkillsRoot for <c>*doti-*</c> dirs (so a pre-category repo whose prior
    /// manifest is empty is still covered, the chicken-and-egg the adversarial pass flagged), minus the dirs the
    /// current render targets. Each orphan's files are deleted ONLY when baseline-clean (the prior manifest's recorded
    /// canonical hash matches); an operator-edited or unbaselined orphan file is preserved and its dir is blocked (no
    /// <c>--force</c> destruction of operator edits). Emptied dirs are pruned; the removed files are returned so the
    /// new baseline records them in <c>ObsoleteAssets</c>.
    /// </summary>
    private static IReadOnlyList<ManagedAssetHashEntry> PruneOrphanedManagedSkillDirs(
        string targetRepoRoot,
        IReadOnlyList<DotiAgentTarget> agents,
        IReadOnlyList<DotiRenderTarget> renderTargets,
        bool force,
        List<DotiInstallPathEffect> removed,
        List<DotiInstallPathEffect> preserved,
        List<DotiInstallPathEffect> blocked)
    {
        HashSet<string> keptDirs = RenderedSkillDirs(renderTargets);
        ManagedAssetManifest? priorManifest = ManagedAssetManifestStore.Read(targetRepoRoot);
        Dictionary<string, ManagedAssetHashEntry> baseline = priorManifest is null
            ? new Dictionary<string, ManagedAssetHashEntry>(StringComparer.OrdinalIgnoreCase)
            : priorManifest.Assets
                .GroupBy(a => a.Path.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var obsolete = new List<ManagedAssetHashEntry>();
        foreach (DotiAgentTarget agent in agents)
        {
            string skillsRootFull = ResolveInside(targetRepoRoot, agent.SkillsRoot);
            if (!Directory.Exists(skillsRootFull))
            {
                continue;
            }

            foreach (string dir in Directory.GetDirectories(skillsRootFull))
            {
                string dirName = Path.GetFileName(dir);
                string relDir = $"{agent.SkillsRoot}/{dirName}".Replace('\\', '/');
                if (!dirName.Contains("doti-", StringComparison.OrdinalIgnoreCase) || keptDirs.Contains(relDir))
                {
                    continue; // not a managed Doti skill dir, or the current render still targets it
                }

                PruneOneOrphanDir(targetRepoRoot, dir, relDir, baseline, force, removed, preserved, blocked, obsolete);
            }
        }

        return obsolete;
    }

    /// <summary>The set of skill dirs (<c>{SkillsRoot}/{skillId}</c>) the current render targets — the immediate
    /// child dir under any agent SkillsRoot for every rendered file. A dir absent from this set but present on disk
    /// is an orphan.</summary>
    private static HashSet<string> RenderedSkillDirs(IReadOnlyList<DotiRenderTarget> renderTargets)
    {
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (DotiAgentTarget agent in DotiAgentTarget.All)
        {
            string prefix = agent.SkillsRoot.Replace('\\', '/') + "/";
            foreach (DotiRenderTarget target in renderTargets)
            {
                string path = target.RelativePath.Replace('\\', '/');
                if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string remainder = path[prefix.Length..];
                int slash = remainder.IndexOf('/');
                if (slash > 0)
                {
                    dirs.Add(prefix + remainder[..slash]);
                }
            }
        }

        return dirs;
    }

    private static void PruneOneOrphanDir(
        string targetRepoRoot,
        string dirFull,
        string relDir,
        IReadOnlyDictionary<string, ManagedAssetHashEntry> baseline,
        bool force,
        List<DotiInstallPathEffect> removed,
        List<DotiInstallPathEffect> preserved,
        List<DotiInstallPathEffect> blocked,
        List<ManagedAssetHashEntry> obsolete)
    {
        bool anyPreserved = false;
        foreach (string file in Directory.GetFiles(dirFull, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(targetRepoRoot, file).Replace('\\', '/');
            if (force)
            {
                File.Delete(file);
                removed.Add(new DotiInstallPathEffect(rel, "orphaned managed skill asset force-removed (render no longer targets this dir)"));
                continue;
            }

            if (!baseline.TryGetValue(rel, out ManagedAssetHashEntry? entry))
            {
                // 031 T004/FR-004/FR-005 (D2): a no-baseline orphan reaching here is a *doti-* rendered-skill file
                // under an agent SkillsRoot that the current render no longer targets (filtered in
                // PruneOrphanedManagedSkillDirs). A rendered Doti skill is NOT operator content — "hand-edits are
                // drift" (agent context); the source-bug regression erased these husks' baseline, and the old
                // conservative "preserve as operator-owned" is exactly what let them survive. Prune it. (Operator
                // POLICY assets — skills.json under managed-replace-preserve-live-config, the constitution — are a
                // different code path and are never reached here, so they stay preserved.)
                File.Delete(file);
                removed.Add(new DotiInstallPathEffect(rel, "orphaned managed Doti skill dir the render no longer targets; rendered skills are not operator-owned"));
                continue;
            }

            CanonicalHash current = CanonicalContentHasher.HashFile(file, entry.HashProfile);
            if (!string.Equals(current.Sha256, entry.Sha256, StringComparison.Ordinal))
            {
                anyPreserved = true;
                blocked.Add(new DotiInstallPathEffect(rel, "orphaned managed skill asset was operator-modified; preserved, rerun with --force to remove"));
                continue;
            }

            File.Delete(file);
            removed.Add(new DotiInstallPathEffect(rel, "obsolete managed skill asset removed after canonical baseline match (render renamed this dir away)"));
            obsolete.Add(entry);
        }

        PruneEmptyOrphanDir(dirFull, relDir, anyPreserved, removed, preserved);
    }

    private static void PruneEmptyOrphanDir(
        string dirFull,
        string relDir,
        bool anyPreserved,
        List<DotiInstallPathEffect> removed,
        List<DotiInstallPathEffect> preserved)
    {
        foreach (string sub in Directory.GetDirectories(dirFull, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(sub).Any())
            {
                Directory.Delete(sub);
            }
        }

        if (!Directory.EnumerateFileSystemEntries(dirFull).Any())
        {
            Directory.Delete(dirFull);
            removed.Add(new DotiInstallPathEffect(relDir + "/", "orphaned managed Doti skill dir removed; the render no longer targets it"));
        }
        else if (anyPreserved)
        {
            preserved.Add(new DotiInstallPathEffect(relDir + "/", "orphaned skill dir preserved; contains operator-edited or unbaselined content"));
        }
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
    /// Reconcile one <c>.doti/{sub}</c> static subdirectory (the original 007 T030 loop body, extracted so the 032
    /// D2(e) <c>tools/{sub}</c> pass can share the copied/installed bookkeeping shape without duplicating it).
    /// </summary>
    private static void ReconcileStaticSubdirectory(
        string payloadRoot,
        string targetRepoRoot,
        string topLevel,
        string sub,
        IReadOnlyDictionary<string, ManagedAssetStatus> managed,
        bool force,
        List<string> copied,
        List<DotiInstallPathEffect> installed,
        List<DotiInstallPathEffect> preserved,
        List<DotiInstallPathEffect> skipped,
        List<DotiInstallPathEffect> blocked,
        List<DotiInstallPathEffect> mergePending,
        string installedReason)
    {
        string from = Path.Combine(payloadRoot, topLevel, sub);
        if (!Directory.Exists(from))
        {
            return;
        }

        int installedCount = ReconcileManagedTree(
            payloadRoot, targetRepoRoot, from, managed, force, installed, preserved, skipped, blocked, mergePending);
        string rel = $"{topLevel}/{sub}";
        copied.Add(rel);
        if (installedCount > 0)
        {
            installed.Add(new DotiInstallPathEffect(rel, installedReason));
        }
    }

    /// <summary>
    /// 032 D2(e): reconcile one vendored-tool dir (<c>tools/gitleaks</c>|<c>sentrux</c>|<c>gitversion</c>) — the
    /// manifest/license/config/grammar files <see cref="ManagedAssets.ManagedAssetScanner.DotiSourcePaths"/> already
    /// scans into the baseline (category <c>doti-source</c>) but which, before this fix, no reconcile loop ever
    /// touched. Reuses the SAME <see cref="ReconcileManagedTree"/> preserve/<c>.new</c>/force machinery the
    /// <c>.doti</c> loop uses (the <c>managed-replace-preserve-live-config</c> policy
    /// <see cref="ManagedAssets.ManagedAssetScanner"/> already assigns every <c>doti-source</c> asset). <c>bin/</c>
    /// (the gitignored, per-RID, network-fetched executable) is walked SEPARATELY from a filtered file list rather
    /// than handed to <see cref="ReconcileManagedTree"/>'s <c>AllDirectories</c> walk, so the binary is never staged,
    /// preserved, or sidecar'd — it stays exclusively <c>hx tools fetch</c>'s concern (FR offline/deterministic
    /// update — see <see cref="DotiUpdater"/>'s D2(g) advisory).
    /// </summary>
    private static void ReconcileToolSubdirectory(
        string payloadRoot,
        string targetRepoRoot,
        string sub,
        IReadOnlyDictionary<string, ManagedAssetStatus> managed,
        bool force,
        List<string> copied,
        List<DotiInstallPathEffect> installed,
        List<DotiInstallPathEffect> preserved,
        List<DotiInstallPathEffect> skipped,
        List<DotiInstallPathEffect> blocked,
        List<DotiInstallPathEffect> mergePending)
    {
        string from = Path.Combine(payloadRoot, "tools", sub);
        if (!Directory.Exists(from))
        {
            return;
        }

        int installedCount = ReconcileManagedToolFiles(
            payloadRoot, targetRepoRoot, from, managed, force, installed, preserved, skipped, blocked, mergePending);
        string rel = $"tools/{sub}";
        copied.Add(rel);
        if (installedCount > 0)
        {
            installed.Add(new DotiInstallPathEffect(rel, "managed vendored-tool asset set installed"));
        }
    }

    /// <summary>
    /// 032 D2(e): like <see cref="ReconcileManagedTree"/>'s file enumeration, but filters out every path under a
    /// <c>bin/</c> segment BEFORE reconciling — the one behavioral difference from the <c>.doti</c> loop, required
    /// because a vendored-tool dir's <c>bin/{rid}/{tool}.exe</c> must never be staged/preserved/sidecar'd (it is
    /// gitignored and fetched separately, never a Doti-managed asset).
    /// </summary>
    private static int ReconcileManagedToolFiles(
        string payloadRoot,
        string targetRepoRoot,
        string sourceDir,
        IReadOnlyDictionary<string, ManagedAssetStatus> managed,
        bool force,
        List<DotiInstallPathEffect> installed,
        List<DotiInstallPathEffect> preserved,
        List<DotiInstallPathEffect> skipped,
        List<DotiInstallPathEffect> blocked,
        List<DotiInstallPathEffect> mergePending)
    {
        int installedCount = 0;
        foreach (string sourceFile in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relFromPayload = Path.GetRelativePath(payloadRoot, sourceFile).Replace('\\', '/');
            if (relFromPayload.Contains("/bin/", StringComparison.OrdinalIgnoreCase))
            {
                continue; // the gitignored, per-RID vendored executable — never a managed reconcile target.
            }

            installedCount += ReconcileManagedFile(
                payloadRoot, targetRepoRoot, sourceFile, managed, force, installed, preserved, skipped, blocked, mergePending);
        }

        return installedCount;
    }

    /// <summary>
    /// 007 T030 (FR-015, SC-007/018): reconcile one bundled <c>.doti/{sub}</c> managed subtree into the target by
    /// applying <see cref="ReconcileManagedFile"/> to every file under <paramref name="sourceDir"/>. Returns the
    /// install count.
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
        List<DotiInstallPathEffect> blocked,
        List<DotiInstallPathEffect> mergePending)
    {
        int installedCount = 0;
        foreach (string sourceFile in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            installedCount += ReconcileManagedFile(
                payloadRoot, targetRepoRoot, sourceFile, managed, force, installed, preserved, skipped, blocked, mergePending);
        }

        return installedCount;
    }

    /// <summary>
    /// 007 T030 (FR-015, SC-007/018) + 032 D2(e): the conflict-aware reconcile decision for ONE managed file —
    /// shared by the <c>.doti/{sub}</c> tree walk (<see cref="ReconcileManagedTree"/>) and the <c>tools/{sub}</c>
    /// vendored-tool walk (<see cref="ReconcileManagedToolFiles"/>), so there is exactly one customization scheme.
    /// Every managed write routes through <see cref="ResolveInside"/> (fail-closed on escape). A managed asset the
    /// operator MODIFIED (per the baseline) or a pre-existing file with no baseline entry is preserved and the
    /// bundled version is staged as a <c>.new</c> sidecar; an operator-DELETED managed asset is not resurrected
    /// without <paramref name="force"/>; a modified template/skill blocks unless forced; clean / new files are
    /// installed. Returns 1 when installed, else 0.
    /// </summary>
    private static int ReconcileManagedFile(
        string payloadRoot,
        string targetRepoRoot,
        string sourceFile,
        IReadOnlyDictionary<string, ManagedAssetStatus> managed,
        bool force,
        List<DotiInstallPathEffect> installed,
        List<DotiInstallPathEffect> preserved,
        List<DotiInstallPathEffect> skipped,
        List<DotiInstallPathEffect> blocked,
        List<DotiInstallPathEffect> mergePending)
    {
        string rel = Path.GetRelativePath(payloadRoot, sourceFile).Replace('\\', '/');
        string dest = ResolveInside(targetRepoRoot, rel);
        bool exists = File.Exists(dest);
        managed.TryGetValue(rel, out ManagedAssetStatus? status);

        if (status?.State == ManagedAssetState.Modified && !force)
        {
            if (status.UpdateConflictPolicy == "managed-replace-preserve-live-config")
            {
                PreserveWithSidecar(targetRepoRoot, rel, sourceFile, preserved, mergePending,
                    "operator-modified managed asset preserved");
                return 0;
            }

            blocked.Add(new DotiInstallPathEffect(rel, "modified managed template/skill blocked; rerun with --force to overwrite"));
            return 0;
        }

        if (status?.State == ManagedAssetState.Missing && !force)
        {
            skipped.Add(new DotiInstallPathEffect(rel, "operator-deleted managed asset not resurrected without --force"));
            return 0;
        }

        if (status is null && exists)
        {
            // Pre-existing operator/foreign content with no managed baseline (brownfield): never blind-overwrite.
            PreserveWithSidecar(targetRepoRoot, rel, sourceFile, preserved, mergePending,
                "pre-existing operator content preserved");
            return 0;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.Copy(sourceFile, dest, overwrite: true);
        return 1;
    }

    /// <summary>
    /// 031 T005/FR-006 (D3): preserve an operator file and stage the bundled version as a <c>.new</c> merge-helper —
    /// but ONLY when the bundled content genuinely differs (the byte-equality guard in <see cref="StageSidecar"/>).
    /// When a sidecar was written it is surfaced as a DISTINCT merge-pending item (so the operator merges it and the
    /// self-commit excludes it); when the content matched, the file is preserved with no spurious stray and no
    /// merge-pending entry.
    /// </summary>
    private static void PreserveWithSidecar(
        string targetRepoRoot,
        string rel,
        string sourceFile,
        List<DotiInstallPathEffect> preserved,
        List<DotiInstallPathEffect> mergePending,
        string preservedReason)
    {
        bool wroteSidecar = StageSidecar(targetRepoRoot, rel, sourceFile);
        preserved.Add(new DotiInstallPathEffect(rel, wroteSidecar
            ? preservedReason + "; bundled version staged as .new"
            : preservedReason + " (matches bundled; no .new staged)"));
        if (wroteSidecar)
        {
            mergePending.Add(new DotiInstallPathEffect(rel + ".new", "bundled version staged as a merge-helper; merge into " + rel + " then delete"));
        }
    }

    /// <summary>
    /// 031 T005/FR-006 (D3): stage the bundled version as a <c>.new</c> sidecar beside the preserved operator file —
    /// SKIPPING the write (and reporting no merge-pending) when the bundled content is BYTE-IDENTICAL to the
    /// operator's existing file (no spurious stray). Returns true iff a sidecar was written (a genuine difference the
    /// operator must merge), so the caller can surface it as a distinct merge-pending item. A stale prior
    /// <c>.new</c> is removed when the content has since converged, so a re-run never leaves a phantom merge-helper.
    /// </summary>
    private static bool StageSidecar(string targetRepoRoot, string rel, string sourceFile)
    {
        string dest = ResolveInside(targetRepoRoot, rel);
        if (File.Exists(dest) && FilesAreByteIdentical(sourceFile, dest))
        {
            // The operator's content already equals the bundled version — there is nothing to merge.
            string stale = ResolveInside(targetRepoRoot, rel + ".new");
            if (File.Exists(stale))
            {
                File.Delete(stale);
            }

            return false;
        }

        string sidecar = ResolveInside(targetRepoRoot, rel + ".new");
        Directory.CreateDirectory(Path.GetDirectoryName(sidecar)!);
        File.Copy(sourceFile, sidecar, overwrite: true);
        return true;
    }

    private static bool FilesAreByteIdentical(string a, string b)
    {
        var infoA = new FileInfo(a);
        var infoB = new FileInfo(b);
        if (!infoA.Exists || !infoB.Exists || infoA.Length != infoB.Length)
        {
            return false;
        }

        return File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
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

    // Compare payload versions by their numeric major.minor.patch core, tie-breaking on the full string so a
    // pre-release/build suffix still orders deterministically.
    private static int ComparePayloadVersions(string a, string b)
    {
        int core = VersionCore(a).CompareTo(VersionCore(b));
        return core != 0 ? core : string.CompareOrdinal(a, b);
    }

    private static System.Version VersionCore(string value)
    {
        string core = value;
        int cut = core.IndexOfAny(['-', '+']);
        if (cut >= 0)
        {
            core = core[..cut];
        }

        return System.Version.TryParse(core, out System.Version? parsed) ? parsed : new System.Version(0, 0, 0, 0);
    }
}
