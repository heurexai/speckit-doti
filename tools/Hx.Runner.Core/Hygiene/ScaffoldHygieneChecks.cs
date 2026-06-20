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
            string extension = Path.GetExtension(file.RepoRelativePath).ToLowerInvariant();

            if (policy.ShellRunnerExtensions.Contains(extension))
            {
                findings.Add(new HygieneFinding(
                    HygieneFindingCategory.ShellRunner, HygieneSeverity.Error,
                    "scaffold.shell-runner", file.RepoRelativePath, null,
                    $"Generated shell runner '{extension}' is not allowed; runner logic must be .NET tooling."));
                continue;
            }

            if (policy.BinaryExtensions.Contains(extension))
            {
                findings.Add(new HygieneFinding(
                    HygieneFindingCategory.BinaryMaterial, HygieneSeverity.Error,
                    "scaffold.binary-material", file.RepoRelativePath, null,
                    $"Unexpected binary material '{extension}'; vendor binaries only with a manifest and hash."));
                continue;
            }

            string content;
            try
            {
                content = File.ReadAllText(file.ContentPath);
            }
            catch (IOException)
            {
                continue;
            }

            if (content.Contains('\0'))
            {
                continue; // binary content; extension allowlist governs deliberate binaries
            }

            ScanContent(policy, file.RepoRelativePath, content, findings);
        }

        return findings;
    }

    private static void ScanContent(HygienePolicy policy, string repoRelativePath, string content, List<HygieneFinding> findings)
    {
        // Private key / certificate material: require a real BEGIN...END block so
        // marker definitions in source/policy files are not self-flagged.
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

        string[] lines = content.Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            int lineNumber = index + 1;

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

            foreach (Match match in UrlPattern.Matches(line))
            {
                string url = match.Value.TrimEnd('.', ',', ')', ']', '>');
                bool allowed = policy.AllowedUrlPrefixes.Any(prefix =>
                    url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                if (!allowed)
                {
                    findings.Add(new HygieneFinding(
                        HygieneFindingCategory.ExternalUrl, HygieneSeverity.Warning,
                        "scaffold.external-url", repoRelativePath, lineNumber,
                        $"External URL not in the allowlist: {url}"));
                }
            }
        }
    }
}
