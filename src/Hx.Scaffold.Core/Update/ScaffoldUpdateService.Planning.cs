using Hx.Doti.Core.ManagedAssets;
using Hx.Scaffold.Core.Versioning;
using System.Text.Json;

namespace Hx.Scaffold.Core.Update;

public static partial class ScaffoldUpdateService
{
    private static void AddManagedAssetBlockers(
        ScaffoldVersionReport version,
        bool force,
        bool legacyPreVersioned,
        List<string> blockers,
        List<ScaffoldUpdateDiagnostic> diagnostics)
    {
        if (version.ManagedAssets is null && !legacyPreVersioned)
        {
            AddBlocker(blockers, diagnostics, "update.managed-assets.unavailable",
                "managed asset metadata is missing or invalid");
        }
        else if (version.ManagedAssets is not null && !force)
        {
            AddAssetStatusBlockers(version.ManagedAssets.ModifiedWorkflowTemplates, ManagedAssetCategory.WorkflowTemplate,
                "update.modified.workflow-template", "modified workflow-template", blockers, diagnostics);
            AddAssetStatusBlockers(version.ManagedAssets.ModifiedSkillGeneratedInstructions,
                ManagedAssetCategory.SkillGeneratedInstruction, "update.modified.skill-generated-instruction",
                "modified skill/generated-instruction", blockers, diagnostics);
            AddAssetStatusBlockers(version.ManagedAssets.Missing, null, "update.missing.managed-asset",
                "missing managed asset", blockers, diagnostics);
        }
    }

    private static void AddAssetStatusBlockers(
        IEnumerable<ManagedAssetStatus> statuses,
        string? category,
        string code,
        string label,
        List<string> blockers,
        List<ScaffoldUpdateDiagnostic> diagnostics)
    {
        foreach (ManagedAssetStatus status in statuses)
        {
            AddBlocker(blockers, diagnostics, code, $"{label}: {status.Path}", status.Path, category ?? status.Category);
        }
    }

    private static List<string> InitialActions(ScaffoldUpdateRequest request) =>
    [
        "resolve latest non-prerelease GitHub release from heurexai/speckit-doti",
        request.NoWorktree ? "skip backup worktree because --noworktree was supplied" : "create backup worktree from target HEAD before mutation",
        request.Force ? "replace modified managed Doti assets because --force was supplied" : "preserve modified managed Doti assets by failing before mutation",
        "preserve live configuration and baselines",
    ];

    private static ReleasePlan ResolveReleasePlan(
        string rid,
        ScaffoldUpdateServices services,
        List<string> actions,
        List<string> blockers,
        List<ScaffoldUpdateDiagnostic> diagnostics)
    {
        try
        {
            UpdateCacheResult cache = ResolveAndCacheRelease(rid, services);
            actions.Insert(1, $"select host asset {cache.Release.Archive.Name}");
            return new ReleasePlan(cache, BuildDesiredFiles(cache.PayloadRoot), cache.Release.Archive.Name);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or HttpRequestException or JsonException)
        {
            AddBlocker(blockers, diagnostics, "update.release-resolution.failed",
                "latest release/cache resolution failed: " + ex.Message);
            return new ReleasePlan(null, [], null);
        }
    }

    private static void AddDirtyPathBlockers(
        string gitRoot,
        ManagedFilePlan filePlan,
        int desiredCount,
        List<string> blockers,
        List<ScaffoldUpdateDiagnostic> diagnostics)
    {
        if (desiredCount == 0)
        {
            return;
        }

        foreach (string blocker in DirtyPlannedPathBlockers(gitRoot, filePlan.PlannedWritePaths))
        {
            AddBlocker(blockers, diagnostics, "update.dirty-managed-path",
                blocker, blocker.Split(": ", 2).LastOrDefault());
        }
    }

    private static void AddBlocker(
        List<string> blockers,
        List<ScaffoldUpdateDiagnostic> diagnostics,
        string code,
        string message,
        string? path = null,
        string? category = null)
    {
        blockers.Add(message);
        diagnostics.Add(new ScaffoldUpdateDiagnostic(code, "error", message, path, category));
    }
}
