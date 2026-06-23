using Hx.Doti.Core;
using Hx.Doti.Core.ManagedAssets;
using Hx.Runner.Core.Io;
using Hx.Scaffold.Core.Versioning;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Core.Update;

public static partial class ScaffoldUpdateService
{
    private static MutationResult ExecuteMutation(
        ScaffoldUpdateRequest request,
        ScaffoldUpdateServices services,
        string gitRoot,
        ScaffoldVersionReport version,
        UpdateCacheResult? cache,
        IReadOnlyList<DesiredManagedFile> desired,
        List<string> blockers,
        List<ScaffoldUpdateDiagnostic> diagnostics)
    {
        ScaffoldUpdateWorktreeBackup? backup = DisabledBackupIfNeeded(request, gitRoot);
        if (request.DryRun || blockers.Count > 0)
        {
            return new MutationResult(backup, Delegated: false, null, []);
        }

        if (cache is null)
        {
            AddBlocker(blockers, diagnostics, "update.release-cache.unavailable", "release cache is unavailable");
            return new MutationResult(backup, Delegated: false, null, []);
        }

        if (DelegationReason(version, request.RunningVersion, cache.Release.Version) is { } reason)
        {
            return new MutationResult(backup, Delegated: true, RunDelegatedUpdater(cache, gitRoot, request, reason), []);
        }

        backup ??= CreateBackupWorktree(gitRoot, services.WorktreeRoot());
        IReadOnlyList<string> changed = ApplyManagedFiles(gitRoot, desired).ToArray();
        IReadOnlyList<DotiRenderTarget> generatedTargets = DotiRenderer.BuildTargets(gitRoot, DotiAgentTarget.All);
        ManagedAssetScanner.WriteBaseline(gitRoot, generatedTargets);
        WriteUpdatedVersionStamp(gitRoot, cache);
        return new MutationResult(backup, Delegated: false, null, changed);
    }

    private static ScaffoldUpdateReport BuildReport(
        ScaffoldUpdateRequest request,
        string gitRoot,
        string rid,
        string expectedAsset,
        ScaffoldVersionReport version,
        ReleasePlan release,
        MutationResult mutation,
        ManagedFilePlan filePlan,
        IReadOnlyList<string> blockers,
        IReadOnlyList<ScaffoldUpdateDiagnostic> diagnostics,
        IReadOnlyList<string> actions,
        IReadOnlyList<string> possibleOrphans,
        bool legacyPreVersioned)
    {
        string? legacyFollowUp = legacyPreVersioned ? LegacyFollowUpInstruction() : null;
        return new ScaffoldUpdateReport(
            JsonContractDefaults.SchemaVersion, gitRoot, request.DryRun, request.Force, request.NoWorktree,
            rid, expectedAsset, version, release.Cache?.Release.Version, release.Cache?.Action,
            release.Cache?.ArchivePath, release.Cache?.ExtractedPath, mutation.BackupWorktree?.Path,
            mutation.BackupWorktree, mutation.Delegated, mutation.Delegation,
            TargetToLatestRelation(version.Target, release.Cache?.Release.Version), blockers, diagnostics, actions,
            filePlan.CreatePaths, filePlan.ReplacePaths, ForceReplacedPaths(request, version), mutation.ChangedPaths,
            PreservedLivePaths(gitRoot), possibleOrphans, legacyFollowUp, FollowUps(legacyFollowUp));
    }

    private static ScaffoldUpdateWorktreeBackup? DisabledBackupIfNeeded(ScaffoldUpdateRequest request, string gitRoot) =>
        request.NoWorktree
            ? new ScaffoldUpdateWorktreeBackup(
                null,
                TryHeadSha(gitRoot),
                "HEAD",
                null,
                Created: false,
                Disabled: true,
                "No backup worktree was created because --noworktree was supplied; managed files will be replaced directly in the original target checkout.")
            : null;

    private static void WriteUpdatedVersionStamp(string gitRoot, UpdateCacheResult cache) =>
        ScaffoldVersionReporter.WriteStamp(gitRoot, ScaffoldVersionReporter.IdentityFromVersion(
            cache.Release.Version,
            "GitHub release " + cache.Release.TagName,
            cache.Release.Archive.Name,
            FileHashing.Sha256OfFile(cache.ArchivePath)));

    private static IReadOnlyList<string> ForceReplacedPaths(ScaffoldUpdateRequest request, ScaffoldVersionReport version) =>
        request.Force && version.ManagedAssets is not null
            ? version.ManagedAssets.ModifiedWorkflowTemplates
                .Concat(version.ManagedAssets.ModifiedSkillGeneratedInstructions)
                .Concat(version.ManagedAssets.Missing)
                .Select(s => s.Path)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToArray()
            : [];

    private static string LegacyFollowUpInstruction() =>
        "Legacy pre-versioned Doti assets were updated conservatively. Ask an LLM agent to review untouched possible-orphan paths and make repo-specific cleanup through the normal doti workflow before committing.";

    private static IReadOnlyList<string> FollowUps(string? legacyFollowUp) =>
        legacyFollowUp is null
            ? ["hx version --repo <target> --json", "hx update --repo <target> --dry-run --json"]
            : ["hx version --repo <target> --json", "hx update --repo <target> --dry-run --json", legacyFollowUp];
}
