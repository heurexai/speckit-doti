using System.IO.Compression;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Core.Packaging;

/// <summary>One staged-artifact entry that carries the tool's build tree (FR-005).</summary>
public sealed record ReleaseSourceFinding(string Artifact, string Entry, string Marker);

/// <summary>Result of the no-source scan over a staged release layout.</summary>
public sealed record ReleaseSourceScanResult(StageOutcome Outcome, IReadOnlyList<ReleaseSourceFinding> Findings, int ScannedEntryCount);

/// <summary>
/// FR-005/FR-006/SC-004: a release artifact MUST NOT carry the tool's own build tree. Recursively walks a staged
/// release layout — and looks INSIDE every <c>.nupkg</c>/<c>.zip</c> (the template pack is a zip) — for the tool's
/// source markers: the solution (<c>scaffold-dotnet.slnx</c>), a tool source-tree path (<c>src/Hx.</c> — with the
/// dot), or a tool project file (<c>Hx.*.csproj</c>). The <c>Hx.Scaffold.Templates</c> template-pack content is
/// legitimate payload: its files are <c>HxScaffoldSample</c>-prefixed (no <c>Hx.</c> dot) so they never match — a
/// blanket <c>src/</c> or <c>.csproj</c> ban would false-positive on the required template pack (SC-004). Compiled
/// runtime payload (<c>Hx.*.dll</c>/<c>.pdb</c>) is allowed.
/// </summary>
public static class ReleaseSourceInspector
{
    /// <summary>The registry code emitted on a violation. Bound to <c>ErrorCodes.Integrity_ReleaseArtifactContainsSource</c>
    /// by a test (this assembly cannot reference Hx.Cli.Kernel). Keep in sync with errorcodes/registry.json.</summary>
    public const string ViolationCode = "ITG0013";

    public static ReleaseSourceScanResult Scan(string stagedRoot)
    {
        var findings = new List<ReleaseSourceFinding>();
        int scanned = 0;
        if (!Directory.Exists(stagedRoot))
        {
            return new ReleaseSourceScanResult(StageOutcome.Pass, findings, 0);
        }

        foreach (string file in Directory.EnumerateFiles(stagedRoot, "*", SearchOption.AllDirectories))
        {
            scanned++;
            string relative = Path.GetRelativePath(stagedRoot, file).Replace('\\', '/');
            if (SourceMarker(relative) is { } marker)
            {
                findings.Add(new ReleaseSourceFinding(relative, relative, marker));
            }

            if (file.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                scanned += ScanArchive(file, relative, findings);
            }
        }

        StageOutcome outcome = findings.Count == 0 ? StageOutcome.Pass : StageOutcome.Fail;
        return new ReleaseSourceScanResult(outcome, findings, scanned);
    }

    private static int ScanArchive(string archivePath, string archiveRelative, List<ReleaseSourceFinding> findings)
    {
        int scanned = 0;
        try
        {
            using ZipArchive zip = ZipFile.OpenRead(archivePath);
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                scanned++;
                string normalized = entry.FullName.Replace('\\', '/');
                if (SourceMarker(normalized) is { } marker)
                {
                    findings.Add(new ReleaseSourceFinding(archiveRelative, normalized, marker));
                }
            }
        }
        catch (InvalidDataException)
        {
            // A non-zip file that happens to carry a .nupkg/.zip name — opaque payload, not a source carrier.
        }

        return scanned;
    }

    private static string? SourceMarker(string normalizedPath)
    {
        string name = normalizedPath.Contains('/', StringComparison.Ordinal)
            ? normalizedPath[(normalizedPath.LastIndexOf('/') + 1)..]
            : normalizedPath;

        if (string.Equals(name, "scaffold-dotnet.slnx", StringComparison.OrdinalIgnoreCase))
        {
            return "tool-solution";
        }

        // `src/Hx.` (with the dot) is the tool's source projects; `src/HxScaffoldSample` (template content) is not.
        if (normalizedPath.Contains("src/Hx.", StringComparison.Ordinal))
        {
            return "tool-source-tree";
        }

        // `Hx.*.csproj` is a tool project file; `HxScaffoldSample.Cli.csproj` (template) does not start with `Hx.`.
        if (name.StartsWith("Hx.", StringComparison.Ordinal) && name.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return "tool-project-file";
        }

        return null;
    }
}
