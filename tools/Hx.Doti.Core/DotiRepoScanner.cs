using Hx.Tooling.Contracts;

namespace Hx.Doti.Core;

/// <summary>
/// 022 T031 (FR-005/006/007): recursively discover every Doti-enabled repo under a root (a directory containing
/// <c>.doti/payload.json</c>) and report each one's version + relation. Read-only and error-tolerant: a directory
/// that cannot be enumerated, or a repo whose payload cannot be read, becomes an <see cref="DotiVersionRelation.Unknown"/>
/// entry with a reason — never an aborted scan. An empty tree is an explicit success with zero entries. Does not
/// descend INTO a discovered repo (one entry per repo), and skips <c>.git</c> and common vendored/build directories.
/// </summary>
public static class DotiRepoScanner
{
    private static readonly string[] PrunedDirectories =
        [".git", "node_modules", "bin", "obj", ".vs", ".idea", "TestResults"];

    public static DotiScanResult Scan(string root, string installedToolVersion)
    {
        string fullRoot = Path.GetFullPath(root);
        var entries = new List<DotiScanEntry>();
        Walk(fullRoot, installedToolVersion, entries);
        entries.Sort((a, b) => string.CompareOrdinal(a.RepoPath, b.RepoPath));
        return new DotiScanResult(
            JsonContractDefaults.SchemaVersion, fullRoot, installedToolVersion, entries.Count, entries);
    }

    private static void Walk(string dir, string installedToolVersion, List<DotiScanEntry> entries)
    {
        if (File.Exists(Path.Combine(dir, ".doti", "payload.json")))
        {
            DotiRepoVersion v = DotiVersionInspector.Inspect(dir, installedToolVersion);
            entries.Add(new DotiScanEntry(
                v.RepoPath, v.Status, v.PayloadVersion, v.InstalledToolVersion, v.Relation, v.Reason));
            return; // one entry per repo; do not descend into a discovered repo's nested directories.
        }

        string[] children;
        try
        {
            children = Directory.GetDirectories(dir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            entries.Add(new DotiScanEntry(
                Path.GetFullPath(dir), DotiVersionStatus.VersionUnknown, null, installedToolVersion,
                DotiVersionRelation.Unknown, "Directory could not be enumerated: " + ex.Message));
            return;
        }

        foreach (string child in children)
        {
            string name = Path.GetFileName(child);
            if (Array.Exists(PrunedDirectories, p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            Walk(child, installedToolVersion, entries);
        }
    }
}
