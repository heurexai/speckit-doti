using Hx.Tooling.Contracts;

namespace Hx.Doti.Core.ManagedAssets;

public static class ManagedAssetScanner
{
    public static ManagedAssetScanResult Scan(string repoRoot)
    {
        ManagedAssetManifest manifest = ManagedAssetManifestStore.Read(repoRoot)
            ?? throw new InvalidOperationException($"Managed asset manifest is missing: {ManagedAssetManifestStore.RelativePath}");

        if (manifest.SchemaVersion != JsonContractDefaults.SchemaVersion)
        {
            throw new InvalidOperationException($"Managed asset manifest schema version {manifest.SchemaVersion} is unsupported.");
        }

        List<ManagedAssetStatus> statuses = [];
        foreach (ManagedAssetHashEntry entry in manifest.Assets.OrderBy(a => a.Path, StringComparer.Ordinal))
        {
            string full = Path.GetFullPath(Path.Combine(repoRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsInside(repoRoot, full))
            {
                throw new InvalidOperationException($"Managed asset path escapes the repository root: {entry.Path}");
            }

            if (!File.Exists(full))
            {
                statuses.Add(new ManagedAssetStatus(
                    entry.Path,
                    entry.Category,
                    ManagedAssetState.Missing,
                entry.HashProfile,
                entry.Sha256,
                null,
                "managed asset is missing",
                entry.SourceFormat,
                entry.Canonicalizer,
                entry.IdentityPolicy,
                entry.UpdateConflictPolicy));
                continue;
            }

            CanonicalHash current = CanonicalContentHasher.HashFile(full, entry.HashProfile);
            bool clean = string.Equals(entry.Sha256, current.Sha256, StringComparison.Ordinal);
            statuses.Add(new ManagedAssetStatus(
                entry.Path,
                entry.Category,
                clean ? ManagedAssetState.Clean : ManagedAssetState.Modified,
                entry.HashProfile,
                entry.Sha256,
                current.Sha256,
                clean ? null : "canonical hash differs from the installed baseline",
                entry.SourceFormat,
                entry.Canonicalizer,
                entry.IdentityPolicy,
                entry.UpdateConflictPolicy));
        }

        ManagedAssetStatus[] modifiedTemplates = statuses
            .Where(s => s.State == ManagedAssetState.Modified && s.Category == ManagedAssetCategory.WorkflowTemplate)
            .ToArray();
        ManagedAssetStatus[] modifiedSkills = statuses
            .Where(s => s.State == ManagedAssetState.Modified && s.Category == ManagedAssetCategory.SkillGeneratedInstruction)
            .ToArray();
        ManagedAssetStatus[] missing = statuses
            .Where(s => s.State == ManagedAssetState.Missing)
            .ToArray();
        string outcome = modifiedTemplates.Length == 0 && modifiedSkills.Length == 0 && missing.Length == 0
            ? StageOutcome.Pass.ToString().ToLowerInvariant()
            : StageOutcome.Fail.ToString().ToLowerInvariant();

        return new ManagedAssetScanResult(
            JsonContractDefaults.SchemaVersion,
            outcome,
            statuses,
            modifiedTemplates,
            modifiedSkills,
            missing);
    }

    public static ManagedAssetManifest CreateBaseline(
        string repoRoot,
        IReadOnlyList<DotiRenderTarget> generatedTargets)
    {
        List<ManagedAssetHashEntry> entries = [];
        foreach (string path in WorkflowTemplatePaths(repoRoot))
        {
            entries.Add(HashEntry(repoRoot, path, ManagedAssetCategory.WorkflowTemplate));
        }

        foreach (string path in DotiSourcePaths(repoRoot))
        {
            entries.Add(HashEntry(repoRoot, path, ManagedAssetCategory.DotiSource));
        }

        foreach (DotiRenderTarget target in generatedTargets)
        {
            entries.Add(HashEntry(repoRoot, target.RelativePath, ManagedAssetCategory.SkillGeneratedInstruction));
        }

        return new ManagedAssetManifest(
            JsonContractDefaults.SchemaVersion,
            entries
                .GroupBy(e => e.Path, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(e => e.Path, StringComparer.Ordinal)
                .ToArray());
    }

    public static void WriteBaseline(string repoRoot, IReadOnlyList<DotiRenderTarget> generatedTargets) =>
        ManagedAssetManifestStore.Write(repoRoot, CreateBaseline(repoRoot, generatedTargets));

    private static IEnumerable<string> WorkflowTemplatePaths(string repoRoot)
    {
        string[] roots =
        [
            Path.Combine(repoRoot, ".doti", "workflows"),
            Path.Combine(repoRoot, "doti", "core", "templates"),
        ];

        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                yield return Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
            }
        }
    }

    private static IEnumerable<string> DotiSourcePaths(string repoRoot)
    {
        string[] roots =
        [
            Path.Combine(repoRoot, "doti", "core"),
            Path.Combine(repoRoot, "doti", "profiles"),
            Path.Combine(repoRoot, ".doti", "templates"),
            Path.Combine(repoRoot, ".doti", "memory"),
            Path.Combine(repoRoot, ".doti", "integrations"),
            Path.Combine(repoRoot, "tools", "gitleaks"),
            Path.Combine(repoRoot, "tools", "sentrux"),
            Path.Combine(repoRoot, "tools", "gitversion"),
        ];

        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
                if (relative.StartsWith("doti/core/templates/", StringComparison.OrdinalIgnoreCase)
                    || relative.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
                    || relative.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return relative;
            }
        }
    }

    private static ManagedAssetHashEntry HashEntry(string repoRoot, string relativePath, string category)
    {
        string full = Path.GetFullPath(Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string profile = CanonicalContentHasher.ProfileForPath(relativePath);
        CanonicalHash hash = CanonicalContentHasher.HashFile(full, profile);
        return new ManagedAssetHashEntry(
            relativePath,
            category,
            profile,
            hash.Sha256,
            hash.SourceFormat,
            hash.Canonicalizer,
            "canonical-content-hash",
            ConflictPolicyFor(category));
    }

    private static string ConflictPolicyFor(string category) =>
        category is ManagedAssetCategory.WorkflowTemplate or ManagedAssetCategory.SkillGeneratedInstruction
            ? "fail-hard-unless-force"
            : "managed-replace-preserve-live-config";

    private static bool IsInside(string repoRoot, string fullPath)
    {
        string root = Path.GetFullPath(repoRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
