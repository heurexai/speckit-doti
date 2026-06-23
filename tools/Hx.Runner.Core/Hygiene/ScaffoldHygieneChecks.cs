using System.Text.RegularExpressions;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Core.Hygiene;

/// <summary>A file to scan: its repo-relative report path and the on-disk path to read content from.</summary>
public sealed record ScanFile(string RepoRelativePath, string ContentPath);

/// <summary>
/// Scaffold-owned hygiene checks (everything that is not delegated to Gitleaks):
/// developer-local paths, private-key/cert material, unexpected binary material,
/// unexpected external URLs, and generated shell-runner markers.
/// </summary>
public static class ScaffoldHygieneChecks
{
    private static readonly Regex UrlPattern = new(
        "https?://[^\\s\"'`)\\]<>]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static IReadOnlyList<HygieneFinding> Scan(HygienePolicy policy, IEnumerable<ScanFile> files)
    {
        List<HygieneFinding> findings = [];

        foreach (ScanFile file in files)
        {
            if (TryAddExtensionFinding(policy, file, findings))
            {
                continue;
            }

            string? content = TryReadText(file.ContentPath);
            if (content is null || content.Contains('\0'))
            {
                continue;
            }

            ScanContent(policy, file.RepoRelativePath, content, findings);
        }

        return findings;
    }

    private static bool TryAddExtensionFinding(HygienePolicy policy, ScanFile file, List<HygieneFinding> findings)
    {
        string extension = Path.GetExtension(file.RepoRelativePath).ToLowerInvariant();
        if (policy.ShellRunnerExtensions.Contains(extension))
        {
            findings.Add(new HygieneFinding(
                HygieneFindingCategory.ShellRunner, HygieneSeverity.Error,
                "scaffold.shell-runner", file.RepoRelativePath, null,
                $"Generated shell runner '{extension}' is not allowed; runner logic must be .NET tooling."));
            return true;
        }

        if (!policy.BinaryExtensions.Contains(extension))
        {
            return false;
        }

        findings.Add(new HygieneFinding(
            HygieneFindingCategory.BinaryMaterial, HygieneSeverity.Error,
            "scaffold.binary-material", file.RepoRelativePath, null,
            $"Unexpected binary material '{extension}'; vendor binaries only with a manifest and hash."));
        return true;
    }

    private static string? TryReadText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static void ScanContent(HygienePolicy policy, string repoRelativePath, string content, List<HygieneFinding> findings)
    {
        ScanPrivateKeyMarkers(policy, repoRelativePath, content, findings);
        ScanLines(policy, repoRelativePath, content, findings);
    }

    private static void ScanPrivateKeyMarkers(
        HygienePolicy policy,
        string repoRelativePath,
        string content,
        List<HygieneFinding> findings)
    {
        foreach (string beginMarker in policy.PrivateKeyMarkers)
        {
            string endMarker = beginMarker.Replace("BEGIN", "END");
            if (content.Contains(beginMarker, StringComparison.Ordinal)
                && content.Contains(endMarker, StringComparison.Ordinal))
            {
                findings.Add(new HygieneFinding(
                    HygieneFindingCategory.PrivateKey, HygieneSeverity.Error,
                    "scaffold.private-key", repoRelativePath, null,
                    "Private key or certificate block detected (value redacted)."));
                break;
            }
        }
    }

    private static void ScanLines(HygienePolicy policy, string repoRelativePath, string content, List<HygieneFinding> findings)
    {
        string[] lines = content.Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            ScanLine(policy, repoRelativePath, lines[index], index + 1, findings);
        }
    }

    private static void ScanLine(
        HygienePolicy policy,
        string repoRelativePath,
        string line,
        int lineNumber,
        List<HygieneFinding> findings)
    {
        AddLocalPathFindings(policy, repoRelativePath, line, lineNumber, findings);
        AddExternalUrlFindings(policy, repoRelativePath, line, lineNumber, findings);
    }

    private static void AddLocalPathFindings(
        HygienePolicy policy,
        string repoRelativePath,
        string line,
        int lineNumber,
        List<HygieneFinding> findings)
    {
        foreach (string marker in policy.LocalPathMarkers)
        {
            if (line.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new HygieneFinding(
                    HygieneFindingCategory.LocalPath, HygieneSeverity.Error,
                    "scaffold.local-path", repoRelativePath, lineNumber,
                    $"Developer-local path marker '{marker}' is not safe for public release."));
            }
        }
    }

    private static void AddExternalUrlFindings(
        HygienePolicy policy,
        string repoRelativePath,
        string line,
        int lineNumber,
        List<HygieneFinding> findings)
    {
        foreach (Match match in UrlPattern.Matches(line))
        {
            string url = match.Value.TrimEnd('.', ',', ')', ']', '>');
            if (!UrlAllowed(policy, url))
            {
                findings.Add(new HygieneFinding(
                    HygieneFindingCategory.ExternalUrl, HygieneSeverity.Warning,
                    "scaffold.external-url", repoRelativePath, lineNumber,
                    $"External URL not in the allowlist: {url}"));
            }
        }
    }

    private static bool UrlAllowed(HygienePolicy policy, string url) =>
        policy.AllowedUrlPrefixes.Any(prefix => url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
