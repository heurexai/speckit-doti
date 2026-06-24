using Hx.Cycle.Core;
using Hx.Doti.Core;
using Hx.Runner.Core.Platform;
using Hx.Scaffold.Core.Release;
using Hx.Scaffold.Core.Versioning;

namespace Hx.Scaffold.Core.Update;

public static partial class ScaffoldUpdateService
{
    public static ScaffoldUpdateReport Plan(ScaffoldUpdateRequest request, ScaffoldUpdateServices? services = null)
    {
        services ??= new ScaffoldUpdateServices();
        string target = Path.GetFullPath(string.IsNullOrWhiteSpace(request.RepositoryRoot) ? "." : request.RepositoryRoot);
        string gitRoot = ResolveGitRoot(target);
        ScaffoldVersionReport version = ScaffoldVersionReporter.Report(request.RunningVersion, gitRoot);
        string rid = HostPlatformDetector.DetectCurrent().RuntimeIdentifier;
        string expectedAsset = ExpectedAssetName("v*", rid);
        var blockers = new List<string>();
        var diagnostics = new List<ScaffoldUpdateDiagnostic>();
        bool legacyPreVersioned = version.Target is null && version.ManagedAssets is null && IsDotiShaped(gitRoot);
        AddManagedAssetBlockers(version, request.Force, legacyPreVersioned, blockers, diagnostics);
        DotiHookInspection hookPlan = HookInstaller.Inspect(gitRoot);
        AddHookBlockers(hookPlan, blockers, diagnostics);
        List<string> actions = InitialActions(request);
        AddHookAction(hookPlan, actions);
        ReleasePlan release = ResolveReleasePlan(rid, services, actions, blockers, diagnostics);
        expectedAsset = release.ExpectedAsset ?? expectedAsset;
        IReadOnlyList<DesiredManagedFile> desired = release.Desired;
        ManagedFilePlan filePlan = desired.Count > 0 ? BuildFilePlan(gitRoot, desired) : new ManagedFilePlan([], []);
        ReleaseManifestUpdatePlan releaseManifestPlan = PlanReleaseManifest(gitRoot);
        filePlan = IncludeReleaseManifestPlan(filePlan, releaseManifestPlan);
        DotiGitIgnorePlan gitIgnorePlan = DotiGitIgnore.Plan(gitRoot);
        filePlan = IncludeGitIgnorePlan(filePlan, gitIgnorePlan);
        AddDirtyPathBlockers(gitRoot, filePlan, blockers, diagnostics);
        MutationResult mutation = ExecuteMutation(request, services, gitRoot, version, release.Cache, desired,
            releaseManifestPlan, gitIgnorePlan, hookPlan, blockers, diagnostics);
        var possibleOrphans = legacyPreVersioned && desired.Count > 0
            ? PossibleLegacyOrphans(gitRoot, desired.Select(d => d.Path).ToHashSet(StringComparer.OrdinalIgnoreCase))
            : [];
        return BuildReport(request, gitRoot, rid, expectedAsset, version, release, mutation, filePlan,
            blockers, diagnostics, actions, possibleOrphans, legacyPreVersioned, releaseManifestPlan);
    }

    private static string ResolveGitRoot(string target)
    {
        Hx.Runner.Core.Process.ProcessRunResult result = Hx.Runner.Core.Process.ProcessRunner.Run(
            new Hx.Runner.Core.Process.ToolCommand("git", ["rev-parse", "--show-toplevel"], target));
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            bool dotiShaped = IsDotiShaped(target);
            string kind = dotiShaped ? "recognizable doti-shaped target has no Git worktree recovery support" : "target is not a Git repository";
            throw new InvalidOperationException(kind + ": " + target);
        }

        return Path.GetFullPath(result.StandardOutput.Trim());
    }

    private static string ExpectedAssetName(string tag, string rid)
    {
        string ext = OperatingSystem.IsWindows() ? "zip" : "tar.gz";
        return $"speckit-doti-{tag}-{rid}.{ext}";
    }

    private static bool IsDotiShaped(string target) =>
        Directory.Exists(Path.Combine(target, ".doti")) || Directory.Exists(Path.Combine(target, "doti"));

    private static ManagedFilePlan IncludeReleaseManifestPlan(ManagedFilePlan filePlan, ReleaseManifestUpdatePlan releaseManifestPlan)
    {
        if (!releaseManifestPlan.ShouldCreate)
        {
            return filePlan;
        }

        return new ManagedFilePlan(
            filePlan.CreatePaths.Append(ReleaseTargetManifest.RelativePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToArray(),
            filePlan.ReplacePaths);
    }

    private static ManagedFilePlan IncludeGitIgnorePlan(ManagedFilePlan filePlan, DotiGitIgnorePlan gitIgnorePlan)
    {
        if (!gitIgnorePlan.ShouldWrite)
        {
            return filePlan;
        }

        IEnumerable<string> create = gitIgnorePlan.FileExists
            ? filePlan.CreatePaths
            : filePlan.CreatePaths.Append(DotiGitIgnore.RelativePath);
        IEnumerable<string> replace = gitIgnorePlan.FileExists
            ? filePlan.ReplacePaths.Append(DotiGitIgnore.RelativePath)
            : filePlan.ReplacePaths;

        return new ManagedFilePlan(
            create.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.Ordinal).ToArray(),
            replace.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.Ordinal).ToArray());
    }
}
