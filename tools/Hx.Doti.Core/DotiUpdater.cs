using Hx.Doti.Core.ManagedAssets;
using Hx.Tooling.Contracts;

namespace Hx.Doti.Core;

/// <summary>
/// 022 T043 (FR-008/009/010/011/012/015): bring one repo's managed Doti assets up to the installed payload and
/// report the before→after version — the headline the user was missing (<c>install</c> never told them the version
/// it landed on). REUSES <see cref="DotiInstaller.Install"/> for the actual reconciliation, so there is no second
/// customization scheme: operator-modified managed assets are preserved (a <c>.new</c> sidecar) and reported, or
/// overwritten with <paramref name="force"/>; operator-owned content (the constitution) is untouched; a repo with no
/// managed baseline still reconciles forward. Refuses to downgrade a repo that is AHEAD of the tool (FR-011), and a
/// non-Doti directory is reported, never mutated.
/// </summary>
public static class DotiUpdater
{
    public static DotiUpdateOutcome Update(
        string payloadRoot,
        string repoRoot,
        IReadOnlyList<DotiAgentTarget> agents,
        string installedToolVersion,
        bool force)
    {
        string root = Path.GetFullPath(repoRoot);
        if (!Directory.Exists(Path.Combine(root, ".doti")))
        {
            return Outcome(root, DotiUpdateStatus.NotARepo, null, null, installedToolVersion,
                DotiVersionRelation.Unknown, DotiVersionRelation.Unknown, [], [],
                "No .doti directory — not a Doti-enabled repository.");
        }

        string? before = RepoPayloadStore.ReadPayloadVersion(root);
        DotiVersionRelation beforeRelation = DotiVersionRelationCalculator.Relate(before, installedToolVersion);
        // FR-012: no managed-asset baseline → degrade + warn (still reconcile forward, operator content preserved).
        bool noBaseline = ManagedAssetManifestStore.Read(root) is null;
        if (beforeRelation == DotiVersionRelation.Ahead)
        {
            return Outcome(root, DotiUpdateStatus.Ahead, before, before, installedToolVersion,
                beforeRelation, beforeRelation, [], [],
                $"Repo payload {before} is newer than the installed tool {installedToolVersion}; refusing to downgrade.");
        }

        DotiInstallResult install;
        try
        {
            install = DotiInstaller.Install(payloadRoot, root, agents, ProjectNameResolver.Resolve(root, null), force);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or DirectoryNotFoundException)
        {
            return Outcome(root, DotiUpdateStatus.Failed, before, before, installedToolVersion,
                beforeRelation, beforeRelation, [], [], ex.Message);
        }

        if (install.Outcome != StageOutcome.Pass)
        {
            return Outcome(root, DotiUpdateStatus.Failed, before, before, installedToolVersion,
                beforeRelation, beforeRelation, Customizations(install), Changes(install),
                "Reconciliation did not complete cleanly.");
        }

        string? after = RepoPayloadStore.ReadPayloadVersion(root);
        DotiVersionRelation afterRelation = DotiVersionRelationCalculator.Relate(after, installedToolVersion);
        string status = string.Equals(before, after, StringComparison.Ordinal)
            ? DotiUpdateStatus.AlreadyCurrent
            : DotiUpdateStatus.Updated;
        string? reason = noBaseline
            ? "No managed-asset baseline was present; reconciled forward with operator content preserved as .new sidecars."
            : null;
        return Outcome(root, status, before, after, installedToolVersion, beforeRelation, afterRelation,
            Customizations(install), Changes(install), reason);
    }

    // FR-009/010: the operator customizations the reconcile preserved (or blocked without --force) — kept + reported.
    private static IReadOnlyList<DotiAssetOutcome> Customizations(DotiInstallResult install) =>
        install.Preserved.Select(e => new DotiAssetOutcome(e.Path, "preserved", e.Reason))
            .Concat(install.Blocked.Select(e => new DotiAssetOutcome(e.Path, "blocked", e.Reason)))
            .ToArray();

    // The managed-asset changes the reconcile applied (installed/removed/skipped).
    private static IReadOnlyList<DotiAssetOutcome> Changes(DotiInstallResult install) =>
        install.Installed.Select(e => new DotiAssetOutcome(e.Path, "installed", e.Reason))
            .Concat(install.Removed.Select(e => new DotiAssetOutcome(e.Path, "removed", e.Reason)))
            .Concat(install.Skipped.Select(e => new DotiAssetOutcome(e.Path, "skipped", e.Reason)))
            .ToArray();

    private static DotiUpdateOutcome Outcome(
        string root, string status, string? before, string? after, string installedToolVersion,
        DotiVersionRelation beforeRelation, DotiVersionRelation afterRelation,
        IReadOnlyList<DotiAssetOutcome> customizations, IReadOnlyList<DotiAssetOutcome> changes, string? reason) =>
        new(JsonContractDefaults.SchemaVersion, root, status, before, after, installedToolVersion,
            beforeRelation, afterRelation, DryRun: false, customizations, changes, reason);
}
