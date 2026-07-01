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
        bool force,
        string? sourceOrigin = null)
    {
        string root = Path.GetFullPath(repoRoot);
        if (!Directory.Exists(Path.Combine(root, ".doti")))
        {
            return Outcome(root, DotiUpdateStatus.NotARepo, null, null, installedToolVersion,
                DotiVersionRelation.Unknown, DotiVersionRelation.Unknown, [], [], null, sourceOrigin, [], []);
        }

        string? before = RepoPayloadStore.ReadPayloadVersion(root);
        DotiVersionRelation beforeRelation = DotiVersionRelationCalculator.Relate(before, installedToolVersion);
        // FR-012: no managed-asset baseline → degrade + warn (still reconcile forward, operator content preserved).
        bool noBaseline = ManagedAssetManifestStore.Read(root) is null;
        if (beforeRelation == DotiVersionRelation.Ahead)
        {
            return Outcome(root, DotiUpdateStatus.Ahead, before, before, installedToolVersion,
                beforeRelation, beforeRelation, [], [],
                $"Repo payload {before} is newer than the installed tool {installedToolVersion}; refusing to downgrade.",
                sourceOrigin, [], []);
        }

        // 032 D2(g): snapshot each vendored-tool manifest's bytes BEFORE the reconcile — install.Installed records a
        // "tools/{sub}" entry on EVERY call (clean files are still copied, the same pre-existing characteristic the
        // .doti loop already has), so it cannot signal "this tool's content genuinely changed." A direct
        // before/after byte comparison of the manifest is the correct, narrow signal.
        IReadOnlyDictionary<string, byte[]?> manifestsBefore = ReadVendoredToolManifests(root);

        DotiInstallResult install;
        try
        {
            install = DotiInstaller.Install(payloadRoot, root, agents, ProjectNameResolver.Resolve(root, null), force);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or DirectoryNotFoundException)
        {
            return Outcome(root, DotiUpdateStatus.Failed, before, before, installedToolVersion,
                beforeRelation, beforeRelation, [], [], ex.Message, sourceOrigin, [], []);
        }

        if (install.Outcome != StageOutcome.Pass)
        {
            return Outcome(root, DotiUpdateStatus.Failed, before, before, installedToolVersion,
                beforeRelation, beforeRelation, Customizations(install), Changes(install),
                "Reconciliation did not complete cleanly.", sourceOrigin, Pruned(install), MergePending(install));
        }

        string? after = RepoPayloadStore.ReadPayloadVersion(root);
        DotiVersionRelation afterRelation = DotiVersionRelationCalculator.Relate(after, installedToolVersion);
        string status = string.Equals(before, after, StringComparison.Ordinal)
            ? DotiUpdateStatus.AlreadyCurrent
            : DotiUpdateStatus.Updated;
        string? reason = CombineReasons(
            noBaseline
                ? "No managed-asset baseline was present; reconciled forward with operator content preserved as .new sidecars."
                : null,
            ToolAdvisory(root, manifestsBefore));
        return Outcome(root, status, before, after, installedToolVersion, beforeRelation, afterRelation,
            Customizations(install), Changes(install), reason, sourceOrigin, Pruned(install), MergePending(install));
    }

    // 032 D2(g): the manifest filename per vendored tool — the file whose bytes changing is the authoritative
    // "this tool moved to a new release" signal (the grammar/license/config files move in lockstep with it, but the
    // manifest is the single canonical version record per SentruxManifestValidator/the gitleaks/gitversion analogs).
    private static readonly IReadOnlyDictionary<string, string> VendoredToolManifestFileNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["gitleaks"] = "gitleaks.version.json",
        ["sentrux"] = "sentrux.version.json",
        ["gitversion"] = "gitversion.version.json",
    };

    private static IReadOnlyDictionary<string, byte[]?> ReadVendoredToolManifests(string root)
    {
        var snapshot = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        foreach ((string tool, string fileName) in VendoredToolManifestFileNames)
        {
            string path = Path.Combine(root, "tools", tool, fileName);
            snapshot[tool] = File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        return snapshot;
    }

    // 032 D2(g): when a tools/{sub} reconcile genuinely changed a vendored-tool manifest's bytes (a real release
    // move, not just "the dir was reconciled" — see the before/after snapshot above), surface a per-tool advisory
    // naming the exact `hx tools fetch` command to refresh the binary. Update NEVER auto-fetches the exe itself —
    // install/update must stay offline/deterministic (DotiPayloadParityChecker spins a no-network temp install);
    // SentruxManifestValidator.Verify already fail-closes with a clear stale-binary error if the manifest moved but
    // the exe has not been re-fetched yet, replacing the old cryptic "Could not read baseline".
    private static string? ToolAdvisory(string root, IReadOnlyDictionary<string, byte[]?> manifestsBefore)
    {
        string[] changedTools = manifestsBefore
            .Where(kv => !BytesEqual(kv.Value, ReadManifestNow(root, kv.Key)))
            .Select(kv => kv.Key)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return changedTools.Length == 0
            ? null
            : string.Join(" ", changedTools.Select(tool =>
                $"vendored tool manifest updated ({tool}) — run `hx tools fetch --tool {tool}` to refresh the binary."));
    }

    private static byte[]? ReadManifestNow(string root, string tool)
    {
        string path = Path.Combine(root, "tools", tool, VendoredToolManifestFileNames[tool]);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    private static bool BytesEqual(byte[]? a, byte[]? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        return a.AsSpan().SequenceEqual(b);
    }

    private static string? CombineReasons(params string?[] parts)
    {
        string[] present = parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!).ToArray();
        return present.Length == 0 ? null : string.Join(" ", present);
    }

    // FR-009/010: the operator customizations the reconcile preserved (or blocked without --force) — kept + reported.
    private static IReadOnlyList<DotiAssetOutcome> Customizations(DotiInstallResult install) =>
        install.Preserved.Select(e => new DotiAssetOutcome(e.Path, "preserved", e.Reason))
            .Concat(install.Blocked.Select(e => new DotiAssetOutcome(e.Path, "blocked", e.Reason)))
            .ToArray();

    // The managed-asset changes the reconcile applied (installed/removed/skipped). Rendered files are already in
    // Installed (DotiInstaller adds the render Written set there), so this is the full touched set + skips.
    private static IReadOnlyList<DotiAssetOutcome> Changes(DotiInstallResult install) =>
        install.Installed.Select(e => new DotiAssetOutcome(e.Path, "installed", e.Reason))
            .Concat(install.Removed.Select(e => new DotiAssetOutcome(e.Path, "removed", e.Reason)))
            .Concat(install.Skipped.Select(e => new DotiAssetOutcome(e.Path, "skipped", e.Reason)))
            .ToArray();

    // 031 FR-004: the pruned-orphan paths (the subset of Removed) — reported on the outcome + named in the commit msg.
    private static IReadOnlyList<string> Pruned(DotiInstallResult install) =>
        install.Removed.Select(e => e.Path.Replace('\\', '/')).ToList();

    // 031 FR-006: the .new merge-pending sidecars staged because an operator's version was preserved (D3).
    private static IReadOnlyList<DotiAssetOutcome> MergePending(DotiInstallResult install) =>
        (install.MergePending ?? []).Select(e => new DotiAssetOutcome(e.Path, "merge-pending", e.Reason)).ToArray();

    private static DotiUpdateOutcome Outcome(
        string root, string status, string? before, string? after, string installedToolVersion,
        DotiVersionRelation beforeRelation, DotiVersionRelation afterRelation,
        IReadOnlyList<DotiAssetOutcome> customizations, IReadOnlyList<DotiAssetOutcome> changes, string? reason,
        string? sourceOrigin, IReadOnlyList<string> pruned, IReadOnlyList<DotiAssetOutcome> mergePending) =>
        new(JsonContractDefaults.SchemaVersion, root, status, before, after, installedToolVersion,
            beforeRelation, afterRelation, DryRun: false, customizations, changes, reason,
            sourceOrigin, pruned, mergePending, Commit: null);
}
