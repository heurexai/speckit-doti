using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core.Documentation;

public static class ReleaseDocumentationInspector
{
    public const string StepName = "release-documentation";

    public static ReleaseDocumentationProof Inspect(string repositoryRoot, CycleReleaseTrain releaseTrain)
    {
        string repo = Path.GetFullPath(repositoryRoot);
        string[] features = releaseTrain.Features
            .Where(feature => string.Equals(feature.InclusionStatus, "included", StringComparison.OrdinalIgnoreCase))
            .Select(feature => feature.Feature)
            .Where(feature => !string.IsNullOrWhiteSpace(feature))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string releaseNotes = GenerateReleaseNotes(features);
        var documents = new List<ReleaseDocumentationFileProof>();
        var blockers = new List<string>();
        foreach (string relativePath in DocumentationInventory(repo))
        {
            string full = Path.Combine(repo, relativePath.Replace('/', Path.DirectorySeparatorChar));
            bool required = IsRequiredReleaseSurface(relativePath);
            if (!File.Exists(full))
            {
                string status = required ? "missing" : "no-change";
                string reason = required
                    ? "required release documentation surface is missing"
                    : "optional documentation surface is absent";
                documents.Add(new ReleaseDocumentationFileProof(relativePath, status, reason));
                if (required)
                {
                    blockers.Add($"{relativePath}: {reason}");
                }

                continue;
            }

            string text = File.ReadAllText(full);
            string[] missing = features
                .Where(feature => !SlugPattern(feature).IsMatch(text))
                .ToArray();
            if (required && missing.Length > 0)
            {
                string reason = "missing release note feature(s): " + string.Join(", ", missing);
                documents.Add(new ReleaseDocumentationFileProof(relativePath, "stale", reason));
                blockers.Add($"{relativePath}: {reason}");
            }
            else if (required)
            {
                documents.Add(new ReleaseDocumentationFileProof(relativePath, "updated", "contains every included release-train feature slug"));
            }
            else
            {
                documents.Add(new ReleaseDocumentationFileProof(relativePath, "no-change", "not a release-facing documentation surface"));
            }
        }

        StageOutcome outcome = blockers.Count == 0 ? StageOutcome.Pass : StageOutcome.Fail;
        return new ReleaseDocumentationProof(
            JsonContractDefaults.SchemaVersion,
            outcome,
            releaseNotes,
            features,
            documents,
            blockers);
    }

    public static string GenerateReleaseNotes(IReadOnlyList<string> features)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Release notes");
        builder.AppendLine();
        if (features.Count == 0)
        {
            builder.AppendLine("- No completed Doti feature cycles are included in this release train.");
            return builder.ToString().TrimEnd();
        }

        foreach (string feature in features)
        {
            builder.AppendLine("- " + feature);
        }

        return builder.ToString().TrimEnd();
    }

    private static IReadOnlyList<string> DocumentationInventory(string repo)
    {
        var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "README.md",
            "CHANGELOG.md"
        };

        string docs = Path.Combine(repo, "docs");
        if (Directory.Exists(docs))
        {
            foreach (string file in Directory.EnumerateFiles(docs, "*.md", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(repo, file).Replace('\\', '/');
                if (!relative.StartsWith("docs/specs/", StringComparison.OrdinalIgnoreCase)
                    && !relative.StartsWith("docs/plans/", StringComparison.OrdinalIgnoreCase)
                    && !relative.StartsWith("docs/tasks/", StringComparison.OrdinalIgnoreCase))
                {
                    paths.Add(relative);
                }
            }
        }

        return paths.ToArray();
    }

    private static bool IsRequiredReleaseSurface(string relativePath) =>
        string.Equals(relativePath, "README.md", StringComparison.OrdinalIgnoreCase)
        || string.Equals(relativePath, "CHANGELOG.md", StringComparison.OrdinalIgnoreCase);

    // Bug 035 Fix G: a raw `text.Contains(slug)` is a substring match — a short/numeric-prefixed slug like `032-x` is
    // satisfied by an unrelated `1032-x`. Match the slug as a BOUNDED TOKEN instead: no slug-id char (letter/digit/
    // hyphen) immediately before or after the match, so `1032-x` cannot satisfy `032-x`, but the slug stand-alone, in
    // a heading (`## 032-foo`), in a bullet (`- **032-foo**`), or mid-sentence still matches. Compiled once per
    // distinct slug and cached — the release-train slug set is small and stable within one inspection run.
    private static readonly ConcurrentDictionary<string, Regex> SlugPatternCache = new(StringComparer.Ordinal);

    private static Regex SlugPattern(string slug) =>
        SlugPatternCache.GetOrAdd(slug, static s => new Regex(
            "(?<![A-Za-z0-9-])" + Regex.Escape(s) + "(?![A-Za-z0-9-])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
}
