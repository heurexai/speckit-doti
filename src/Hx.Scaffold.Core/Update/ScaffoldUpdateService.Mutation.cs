using Hx.Cycle.Core;
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
        ReleaseManifestUpdatePlan releaseManifestPlan,
        DotiGitIgnorePlan gitIgnorePlan,
        DotiHookInspection hookPlan,
        List<string> blockers,
        List<ScaffoldUpdateDiagnostic> diagnostics)
    {
        ScaffoldUpdateWorktreeBackup? backup = DisabledBackupIfNeeded(request, gitRoot);
        if (request.DryRun || blockers.Count > 0)
        {
            return new MutationResult(backup, Delegated: false, null, [], ToHookReport(hookPlan));
        }

        if (cache is null)
        {
            AddBlocker(blockers, diagnostics, "update.release-cache.unavailable", "release cache is unavailable");
            return new MutationResult(backup, Delegated: false, null, [], ToHookReport(hookPlan));
        }

        if (DelegationReason(version, request.RunningVersion, cache.Release.Version) is { } reason)
        {
            return new MutationResult(backup, Delegated: true, RunDelegatedUpdater(cache, gitRoot, request, reason), [],
                ToHookReport(hookPlan, action: "delegated"));
        }

        backup ??= CreateBackupWorktree(gitRoot, services.WorktreeRoot());
        IReadOnlyList<string> changed = ApplyManagedFiles(gitRoot, desired)
            .Concat(ApplyReleaseManifest(gitRoot, releaseManifestPlan))
            .Concat(ApplyGitIgnore(gitRoot, gitIgnorePlan))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();
        IReadOnlyList<DotiRenderTarget> generatedTargets = DotiRenderer.BuildTargets(gitRoot, DotiAgentTarget.All);
        ManagedAssetScanner.WriteBaseline(gitRoot, generatedTargets);
        WriteUpdatedVersionStamp(gitRoot, cache);
        DotiHookInstallResult hook = HookInstaller.InstallIfSafe(gitRoot);
        if (!hook.Success)
        {
            AddBlocker(blockers, diagnostics, "update.hook.install-failed", hook.Message,
                hook.Inspection.HookPath, "git-hook");
        }

        return new MutationResult(backup, Delegated: false, null, changed, ToHookReport(hook));
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
        bool legacyPreVersioned,
        ReleaseManifestUpdatePlan releaseManifestPlan)
    {
        string? legacyFollowUp = legacyPreVersioned ? LegacyFollowUpInstruction() : null;
        return new ScaffoldUpdateReport(
            JsonContractDefaults.SchemaVersion, gitRoot, request.DryRun, request.Force, request.NoWorktree,
            rid, expectedAsset, version, release.Cache?.Release.Version, release.Cache?.Action,
            release.Cache?.ArchivePath, release.Cache?.ExtractedPath, mutation.BackupWorktree?.Path,
            mutation.BackupWorktree, mutation.Delegated, mutation.Delegation,
            TargetToLatestRelation(version.Target, release.Cache?.Release.Version), blockers, diagnostics, actions,
            filePlan.CreatePaths, filePlan.ReplacePaths, ForceReplacedPaths(request, version), mutation.ChangedPaths,
            mutation.Hook,
            PreservedLivePaths(gitRoot), possibleOrphans, legacyFollowUp, FollowUps(legacyFollowUp, releaseManifestPlan));
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

    private static IReadOnlyList<string> ApplyGitIgnore(string gitRoot, DotiGitIgnorePlan gitIgnorePlan) =>
        gitIgnorePlan.ShouldWrite ? DotiGitIgnore.Ensure(gitRoot) : [];

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

    private static string NumberedSpecUpgradeFollowUpInstruction() =>
        "After update, review project-owned feature docs: leave implemented/completed legacy specs unchanged; migrate any open, unimplemented unnumbered spec to the new NNN-short-name slug (rename matching spec/plan/tasks artifacts and re-stamp specify) before continuing; create all subsequent specs with numbered slugs.";

    private static IReadOnlyList<string> FollowUps(string? legacyFollowUp, ReleaseManifestUpdatePlan releaseManifestPlan)
    {
        var followUps = new List<string>
        {
            "hx version --repo <target> --json",
            "hx update --repo <target> --dry-run --json",
            NumberedSpecUpgradeFollowUpInstruction(),
        };
        if (releaseManifestPlan.FollowUp is not null)
        {
            followUps.Add(releaseManifestPlan.FollowUp);
        }

        if (legacyFollowUp is not null)
        {
            followUps.Add(legacyFollowUp);
        }

        return followUps;
    }

    private static ScaffoldHookReport ToHookReport(DotiHookInspection inspection, string? action = null) =>
        new(
            inspection.Verdict,
            inspection.HookPath,
            inspection.ExpectedSha256,
            inspection.CurrentSha256,
            inspection.CanInstallOrRefresh,
            Changed: false,
            action ?? PlannedHookAction(inspection),
            inspection.Message);

    private static ScaffoldHookReport ToHookReport(DotiHookInstallResult result) =>
        new(
            result.Inspection.Verdict,
            result.Inspection.HookPath,
            result.Inspection.ExpectedSha256,
            result.Inspection.CurrentSha256,
            result.Inspection.CanInstallOrRefresh,
            result.Changed,
            result.Action,
            result.Message);

    private static string PlannedHookAction(DotiHookInspection inspection) => inspection.Verdict switch
    {
        HookInstaller.VerdictMissing => "install",
        HookInstaller.VerdictDotiOwned => "refresh",
        HookInstaller.VerdictExpected => "already-current",
        HookInstaller.VerdictExternal => "blocked",
        HookInstaller.VerdictNotGitRepository => "skipped",
        _ => "unknown",
    };
}
