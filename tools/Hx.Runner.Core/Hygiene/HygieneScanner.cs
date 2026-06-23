using System.Text.Json;
using Hx.Runner.Core.Git;
using Hx.Runner.Core.Gitleaks;
using Hx.Runner.Core.Platform;
using Hx.Runner.Core.Process;
using Hx.Runner.Core.Repository;
using Hx.Runner.Core.Tools;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Core.Hygiene;

/// <summary>
/// Orchestrates a deterministic public-hygiene scan: load policy, verify the
/// localized Gitleaks tool (fail closed when enabled but missing), resolve the
/// scoped file set, run scaffold-specific checks plus Gitleaks, and merge the
/// findings into one <see cref="HygieneScanResult"/>.
/// </summary>
public static class HygieneScanner
{
    public static HygieneScanResult Scan(HygieneScanRequest request)
    {
        string root = Path.GetFullPath(request.RepositoryRoot);
        List<string> warnings = [];
        List<string> advisoryGaps = [];
        List<HygieneSkippedFile> skipped = [];

        HygienePolicy policy = HygienePolicyLoader.Load(root, out bool usedDefault);
        if (usedDefault)
        {
            advisoryGaps.Add("rules/hygiene.json not found; using the built-in default hygiene policy.");
        }

        string rid = HostPlatformDetector.DetectCurrent().RuntimeIdentifier;
        StageOutcome outcome = StageOutcome.Pass;

        ToolVerificationResult? gitleaksVerification = VerifyGitleaks(root, rid, policy, advisoryGaps, ref outcome);

        List<HygieneFinding> gitleaksFindings = [];
        StagedBlobMaterializer? materializer = null;

        try
        {
            List<ScanFile>? scanFiles = request.Scope == HygieneScope.All
                ? [.. EnumerateAll(root, policy).Select(e => new ScanFile(e.Relative, e.Full))]
                : ResolveChangedScanFiles(request, root, policy, skipped, warnings, out materializer);

            if (scanFiles is null)
            {
                // changed-file count exceeded the threshold — fail closed with a recommendation
                return new HygieneScanResult(
                    JsonContractDefaults.SchemaVersion, StageOutcome.Blocked, request.Scope, request.Source,
                    0, skipped, gitleaksVerification, [], warnings, advisoryGaps);
            }

            AddGitleaksFindings(root, rid, request, policy, materializer, gitleaksVerification, gitleaksFindings, advisoryGaps, ref outcome);

            IReadOnlyList<HygieneFinding> scaffoldFindings = ScaffoldHygieneChecks.Scan(policy, scanFiles);
            List<HygieneFinding> findings = [.. scaffoldFindings, .. gitleaksFindings];

            if (outcome != StageOutcome.Blocked)
            {
                outcome = findings.Any(f => f.Severity == HygieneSeverity.Error)
                    ? StageOutcome.Fail
                    : StageOutcome.Pass;
            }

            return new HygieneScanResult(
                JsonContractDefaults.SchemaVersion, outcome, request.Scope, request.Source,
                scanFiles.Count, skipped, gitleaksVerification, findings, warnings, advisoryGaps);
        }
        finally
        {
            materializer?.Dispose();
        }
    }

    private static ToolVerificationResult? VerifyGitleaks(
        string root,
        string rid,
        HygienePolicy policy,
        List<string> advisoryGaps,
        ref StageOutcome outcome)
    {
        if (!policy.GitleaksEnabled)
        {
            advisoryGaps.Add("Gitleaks is disabled in rules/hygiene.json; secret detection relies on scaffold checks only.");
            return null;
        }

        ToolVerificationResult verification = GitleaksManifestValidator.Verify(root, rid);
        if (!verification.Verified)
        {
            outcome = StageOutcome.Blocked;
            advisoryGaps.Add("Gitleaks secret scanning is unavailable (fail-closed): "
                + (verification.Message ?? string.Join("; ", verification.Problems)));
        }

        return verification;
    }

    private static void AddGitleaksFindings(
        string root,
        string rid,
        HygieneScanRequest request,
        HygienePolicy policy,
        StagedBlobMaterializer? materializer,
        ToolVerificationResult? verification,
        List<HygieneFinding> findings,
        List<string> advisoryGaps,
        ref StageOutcome outcome)
    {
        if (!policy.GitleaksEnabled || verification is not { Verified: true })
        {
            return;
        }

        try
        {
            findings.AddRange(RunGitleaks(root, rid, request, materializer));
        }
        catch (Exception ex)
        {
            // Gitleaks ran but errored: fail closed rather than reporting a clean scan.
            outcome = StageOutcome.Blocked;
            advisoryGaps.Add($"Gitleaks scan failed (fail-closed): {ex.Message}");
        }
    }

    /// <summary>
    /// Resolves the scoped changed-file set (staged or ref-range). Returns null when
    /// the changed-file count exceeds the policy threshold (caller fails closed).
    /// </summary>
    private static List<ScanFile>? ResolveChangedScanFiles(
        HygieneScanRequest request, string root, HygienePolicy policy,
        List<HygieneSkippedFile> skipped, List<string> warnings, out StagedBlobMaterializer? materializer)
    {
        materializer = null;
        IReadOnlyList<ChangedFile> changed = request.Source == HygieneSource.Range
            ? GitChangedFileDiscovery.DiscoverRange(root, request.BaseRef ?? "HEAD~1", request.HeadRef ?? "HEAD")
            : GitChangedFileDiscovery.DiscoverStaged(root);

        List<string> live = [];
        foreach (ChangedFile file in changed)
        {
            if (IsExcluded(file.Path, policy.ExcludePaths))
            {
                continue;
            }

            if (file.Kind == ChangeKind.Deleted)
            {
                skipped.Add(new HygieneSkippedFile(file.Path, "deleted"));
                continue;
            }

            live.Add(file.Path);
        }

        if (live.Count > policy.ChangedFileThreshold)
        {
            warnings.Add($"Changed-file count {live.Count} exceeds threshold {policy.ChangedFileThreshold}; run --scope all instead.");
            return null;
        }

        List<ScanFile> scanFiles = [];
        if (request.Source == HygieneSource.Staged)
        {
            HashSet<string> unstaged = UnstagedPaths(root);
            foreach (string path in live)
            {
                if (unstaged.Contains(path))
                {
                    warnings.Add($"Staged file '{path}' has unstaged working-tree changes that are not scanned.");
                }
            }

            materializer = StagedBlobMaterializer.Create(root, live);
            foreach (string rel in materializer.MaterializedPaths)
            {
                string contentPath = Path.Combine(materializer.Root, rel.Replace('/', Path.DirectorySeparatorChar));
                scanFiles.Add(new ScanFile(rel, contentPath));
            }
        }
        else
        {
            foreach (string rel in live)
            {
                string full = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(full))
                {
                    scanFiles.Add(new ScanFile(rel, full));
                }
                else
                {
                    skipped.Add(new HygieneSkippedFile(rel, "not-in-working-tree"));
                }
            }
        }

        return scanFiles;
    }

    private static IReadOnlyList<HygieneFinding> RunGitleaks(
        string root, string rid, HygieneScanRequest request, StagedBlobMaterializer? materializer)
    {
        RepositoryPath manifestPath = RepositoryPathResolver.ResolveInside(root, GitleaksManifestValidator.ManifestRelativePath);
        GitleaksManifest? manifest = JsonSerializer.Deserialize<GitleaksManifest>(
            File.ReadAllText(manifestPath.FullPath), JsonContractSerializerOptions.Create());
        GitleaksAsset? asset = manifest?.Assets.FirstOrDefault(a =>
            string.Equals(a.Rid, rid, StringComparison.OrdinalIgnoreCase));
        if (manifest is null || asset is null)
        {
            return [];
        }

        string inRepoExe = RepositoryPathResolver.ResolveInside(root, asset.ExecutablePath).FullPath;
        string executable = ToolStoreResolver.ResolveOrFallback(
            GitleaksManifestValidator.Tool, manifest.Version, rid, asset.ExecutableName, asset.ExecutableSha256 ?? string.Empty, inRepoExe);
        string config = RepositoryPathResolver.ResolveInside(root, manifest.ConfigPath).FullPath;
        string reportPath = Path.Combine(Path.GetTempPath(), "hx-gitleaks-" + Guid.NewGuid().ToString("n") + ".json");

        try
        {
            ToolCommand command;
            Func<string, string> remap;
            if (request.Scope == HygieneScope.All)
            {
                command = GitleaksProcessAdapter.BuildDirScan(executable, config, root, reportPath);
                remap = path => path.Replace('\\', '/');
            }
            else
            {
                string scanRoot = materializer?.Root ?? root;
                command = GitleaksProcessAdapter.BuildDirScan(executable, config, scanRoot, reportPath);
                remap = path => materializer is not null ? materializer.ToRepoRelative(path) : path.Replace('\\', '/');
            }

            ProcessRunResult run = ProcessRunner.Run(command);

            // Never treat a gitleaks failure as a clean scan (fail closed, not open).
            if (GitleaksExitClassifier.Classify(run.ExitCode, File.Exists(reportPath)) == GitleaksRunStatus.Error)
            {
                throw new InvalidOperationException(
                    $"gitleaks exited with code {run.ExitCode}: {Summarize(run.StandardError)}");
            }

            return File.Exists(reportPath)
                ? GitleaksReportParser.Parse(File.ReadAllText(reportPath), remap)
                : [];
        }
        finally
        {
            if (File.Exists(reportPath))
            {
                File.Delete(reportPath);
            }
        }
    }

    private static IEnumerable<(string Relative, string Full)> EnumerateAll(string root, HygienePolicy policy)
    {
        Stack<string> directories = new();
        directories.Push(root);
        while (directories.Count > 0)
        {
            string current = directories.Pop();

            foreach (string sub in Directory.EnumerateDirectories(current))
            {
                string rel = Path.GetRelativePath(root, sub).Replace('\\', '/');
                if (!IsExcluded(rel, policy.ExcludePaths))
                {
                    directories.Push(sub);
                }
            }

            foreach (string file in Directory.EnumerateFiles(current))
            {
                string rel = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (!IsExcluded(rel, policy.ExcludePaths))
                {
                    yield return (rel, file);
                }
            }
        }
    }

    private static HashSet<string> UnstagedPaths(string root)
    {
        ProcessRunResult result = ProcessRunner.Run(new ToolCommand("git", ["diff", "--name-only", "-z"], root));
        return result.StandardOutput
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(path => path.Replace('\\', '/'))
            .ToHashSet();
    }

    private static string Summarize(string text)
    {
        string oneLine = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return oneLine.Length > 200 ? oneLine[..200] + "…" : oneLine;
    }

    private static bool IsExcluded(string repoRelativePath, IReadOnlyList<string> excludes)
    {
        string[] segments = repoRelativePath.Split('/');
        foreach (string exclude in excludes)
        {
            if (exclude.Contains('/'))
            {
                if (repoRelativePath == exclude || repoRelativePath.StartsWith(exclude + "/", StringComparison.Ordinal))
                {
                    return true;
                }
            }
            else if (segments.Contains(exclude))
            {
                return true;
            }
        }

        return false;
    }
}
