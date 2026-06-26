using Hx.Tooling.Contracts;

namespace Hx.Gate.Core;

/// <summary>One product-source occurrence of a forbidden Velopack/vpk construct (FR-007).</summary>
public sealed record NoVelopackFinding(string Path, int Line, string Kind, string Snippet);

/// <summary>Result of the no-Velopack source scan over the product/runtime/release path.</summary>
public sealed record NoVelopackScanResult(StageOutcome Outcome, IReadOnlyList<NoVelopackFinding> Findings, int ScannedFileCount);

/// <summary>
/// Fail-closed scan enforcing SC-005 / FR-007: the product/runtime/release path — the shipped CLI + cores under
/// <c>tools/</c> and <c>src/</c> — MUST contain no Velopack package reference, no <c>VelopackApp</c> startup hook, and
/// no <c>vpk</c> invocation. It matches the three concrete violation forms, NOT the bare word "Velopack", so the
/// contract's deliberately-frozen <c>Velopack*</c> field names (T004), the orphaned-but-frozen <c>ITG0008</c> error
/// code, and stale help/message strings (owned by T028/T030) are not false-positives. Tests (<c>test/</c>), the
/// scaffold template (<c>scaffold/</c>, cleaned in T042), docs, and build output are outside the scanned roots.
/// </summary>
public static class NoVelopackScanner
{
    private static readonly string[] ScanRoots = ["tools", "src"];

    public static NoVelopackScanResult Scan(string repositoryRoot)
    {
        var findings = new List<NoVelopackFinding>();
        int scanned = 0;
        foreach (string root in ScanRoots)
        {
            string rootPath = Path.Combine(repositoryRoot, root);
            if (!Directory.Exists(rootPath))
            {
                continue;
            }

            foreach (string file in EnumerateSourceFiles(rootPath))
            {
                scanned++;
                ScanFile(repositoryRoot, file, findings);
            }
        }

        StageOutcome outcome = findings.Count == 0 ? StageOutcome.Pass : StageOutcome.Fail;
        return new NoVelopackScanResult(outcome, findings, scanned);
    }

    private static IEnumerable<string> EnumerateSourceFiles(string rootPath) =>
        Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
            .Where(path => (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                            || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                           && !IsBuildOutput(path)
                           && !IsRuleDefinition(path));

    // This scanner's own definition necessarily spells out the forbidden tokens (to forbid them); it is the rule,
    // not a product Velopack usage, so it is exempt. A rename breaks this and fails the live-tree test — self-correcting.
    private static bool IsRuleDefinition(string path) =>
        path.EndsWith("NoVelopackScanner.cs", StringComparison.OrdinalIgnoreCase);

    private static bool IsBuildOutput(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase);
    }

    private static void ScanFile(string repositoryRoot, string file, List<NoVelopackFinding> findings)
    {
        string relative = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');
        string[] lines = File.ReadAllLines(file);
        for (int i = 0; i < lines.Length; i++)
        {
            string? kind = ClassifyViolation(lines[i], file);
            if (kind is not null)
            {
                findings.Add(new NoVelopackFinding(relative, i + 1, kind, lines[i].Trim()));
            }
        }
    }

    // The three concrete FR-007 violations. Deliberately specific so VelopackPackageId/Channel/Artifacts (frozen
    // contract fields, T004) and "Velopack"-mentioning strings are never matched.
    private static string? ClassifyViolation(string line, string file)
    {
        if (file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            && line.Contains("Include=\"Velopack\"", StringComparison.OrdinalIgnoreCase))
        {
            return "package-reference";
        }

        if (line.Contains("VelopackApp", StringComparison.Ordinal))
        {
            return "velopackapp-hook";
        }

        if (line.Contains("VelopackTool", StringComparison.Ordinal)
            || line.Contains("VelopackArtifactClassifier", StringComparison.Ordinal)
            || line.Contains("vpk pack", StringComparison.OrdinalIgnoreCase))
        {
            return "vpk-invocation";
        }

        return null;
    }
}
