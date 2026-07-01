using Hx.Tooling.Contracts;

namespace Hx.Doti.Core;

/// <summary>
/// 022 T051 (FR-016/017/018): batch-update every Doti repo under a root. Discovers repos with
/// <see cref="DotiRepoScanner"/>, runs <see cref="DotiWorktreeUpdate"/> per repo, and aggregates a
/// <see cref="DotiUpdateAllSummary"/> (updated / already-current / failed counts). FAIL-SOFT: one repo's failure
/// becomes a <see cref="DotiUpdateStatus.Failed"/> entry and the batch continues — it never aborts the others.
/// </summary>
public static class DotiBatchUpdater
{
    public static DotiUpdateAllSummary Run(
        string payloadRoot,
        string root,
        IReadOnlyList<DotiAgentTarget> agents,
        string installedToolVersion,
        bool force,
        bool dryRun,
        bool commit = true,
        string? sourceOrigin = null)
    {
        string fullRoot = Path.GetFullPath(root);
        DotiScanResult scan = DotiRepoScanner.Scan(fullRoot, installedToolVersion);

        var results = new List<DotiUpdateOutcome>(scan.Repos.Count);
        foreach (DotiScanEntry entry in scan.Repos)
        {
            try
            {
                // 031 FR-007/SC-012: per-repo source/prune/commit behavior, fail-soft, with a per-repo summary.
                results.Add(DotiWorktreeUpdate.Run(
                    payloadRoot, entry.RepoPath, agents, installedToolVersion, force, dryRun, commit, sourceOrigin));
            }
            catch (Exception ex)
            {
                // Fail-soft backstop: any unexpected per-repo error is recorded, never propagated (FR-017).
                results.Add(new DotiUpdateOutcome(
                    JsonContractDefaults.SchemaVersion, entry.RepoPath, DotiUpdateStatus.Failed,
                    entry.PayloadVersion, entry.PayloadVersion, installedToolVersion,
                    entry.Relation, entry.Relation, dryRun, [], [], ex.Message,
                    sourceOrigin, [], [], Commit: null));
            }
        }

        // 032 D1(c) + 035 (E): a repo whose reconcile succeeded but whose self-owned commit failed
        // (DotiCommitStatus.Failed — "reported, never silently swallowed") is FAILED, not updated. Compute the single
        // effective per-repo disposition FIRST so the buckets are mutually exclusive — otherwise such a repo is
        // counted in BOTH `updated` and `failed`, and Updated+AlreadyCurrent+Failed exceeds Total (a self-contradicting
        // summary).
        bool RepoFailed(DotiUpdateOutcome r) =>
            r.Status is DotiUpdateStatus.Failed or DotiUpdateStatus.GitRequired or DotiUpdateStatus.NotARepo
            || r.Commit?.Status == DotiCommitStatus.Failed;
        int failed = results.Count(RepoFailed);
        int updated = results.Count(r => !RepoFailed(r) && r.Status is DotiUpdateStatus.Updated or DotiUpdateStatus.DryRun);
        int current = results.Count(r => !RepoFailed(r) && r.Status == DotiUpdateStatus.AlreadyCurrent);
        return new DotiUpdateAllSummary(
            JsonContractDefaults.SchemaVersion, fullRoot, installedToolVersion, dryRun,
            results.Count, updated, current, failed, results);
    }
}
