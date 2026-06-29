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
        bool dryRun)
    {
        string fullRoot = Path.GetFullPath(root);
        DotiScanResult scan = DotiRepoScanner.Scan(fullRoot, installedToolVersion);

        var results = new List<DotiUpdateOutcome>(scan.Repos.Count);
        foreach (DotiScanEntry entry in scan.Repos)
        {
            try
            {
                results.Add(DotiWorktreeUpdate.Run(
                    payloadRoot, entry.RepoPath, agents, installedToolVersion, force, dryRun));
            }
            catch (Exception ex)
            {
                // Fail-soft backstop: any unexpected per-repo error is recorded, never propagated (FR-017).
                results.Add(new DotiUpdateOutcome(
                    JsonContractDefaults.SchemaVersion, entry.RepoPath, DotiUpdateStatus.Failed,
                    entry.PayloadVersion, entry.PayloadVersion, installedToolVersion,
                    entry.Relation, entry.Relation, dryRun, [], [], ex.Message));
            }
        }

        int updated = results.Count(r => r.Status is DotiUpdateStatus.Updated or DotiUpdateStatus.DryRun);
        int current = results.Count(r => r.Status == DotiUpdateStatus.AlreadyCurrent);
        int failed = results.Count(r =>
            r.Status is DotiUpdateStatus.Failed or DotiUpdateStatus.GitRequired or DotiUpdateStatus.NotARepo);
        return new DotiUpdateAllSummary(
            JsonContractDefaults.SchemaVersion, fullRoot, installedToolVersion, dryRun,
            results.Count, updated, current, failed, results);
    }
}
