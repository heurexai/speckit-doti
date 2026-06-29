using Hx.Cli.Kernel;
using Hx.Doti.Core;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    // 022 T032 (FR-005/006/019): table every Doti repo under a root + its version/relation. Read-only, error-tolerant;
    // an empty tree is an explicit success. The JSON `data` carries the machine array for agents.
    public static CliResult DotiScan(CliMeta meta, string? root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return Usage(meta, "doti scan", "doti scan requires --root <dir>.");
        }

        DotiScanResult result = DotiRepoScanner.Scan(root, meta.Version);
        string summary = result.Count == 0
            ? $"No Doti-enabled repositories found under {result.Root}."
            : ScanTable(result);
        return CliResults.Ok(meta, "doti scan", summary, result);
    }

    private static string ScanTable(DotiScanResult result)
    {
        var lines = new List<string>
        {
            $"{result.Count} Doti repo(s) under {result.Root} (installed tool {result.InstalledToolVersion}):",
        };
        foreach (DotiScanEntry e in result.Repos)
        {
            string version = e.PayloadVersion ?? "—";
            lines.Add($"  {e.Relation.ToString().ToLowerInvariant(),-9} {version,-14} {e.RepoPath}");
        }

        return string.Join("\n", lines);
    }
}
