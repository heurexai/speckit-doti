using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Hx.Cycle.Core;

/// <summary>
/// Canonical content hashing for cycle doc artifacts (Living-Spec, FR-027). A doc/review stage's proof
/// binds the DESIGN content of its own artifact and of its transitive prerequisite artifacts — NOT
/// implementation-progress churn. Task checkbox state (<c>- [x]</c> vs <c>- [ ]</c>) and
/// <c>doti-task-hash</c> markers are normalized out, and line endings + per-line trailing whitespace are
/// normalized, so:
/// <list type="bullet">
/// <item>checking task boxes during <c>/07-doti-implement</c> does not stale the tasks / analyze /
/// arch-review stages (their canonical content is unchanged);</item>
/// <item>re-stamping an upstream stage WITHOUT a content change does not cascade — a dependent binds the
/// upstream <em>content</em>, not the upstream proof object, so only a real upstream edit stales it.</item>
/// </list>
/// </summary>
public static partial class CanonicalArtifactHasher
{
    /// <summary>Canonical SHA-256 (lowercase hex) of a doc artifact file's design content.</summary>
    public static string CanonicalHashOfFile(string fullPath) =>
        CanonicalHashOfText(File.ReadAllText(fullPath));

    /// <summary>Canonical SHA-256 (lowercase hex) of doc text — EOL/trailing-whitespace-insensitive, with
    /// task checkbox state and <c>doti-task-hash</c> markers normalized out.</summary>
    public static string CanonicalHashOfText(string text)
    {
        string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        normalized = TaskHashMarkerRegex().Replace(normalized, "");
        normalized = TaskCheckboxRegex().Replace(normalized, "${prefix} ${suffix}");
        // Per-line trailing-whitespace + trailing-newline normalization, so EOL style and a final newline
        // (neither a design change) do not move the hash.
        string canonical = string.Join('\n', normalized.Split('\n').Select(line => line.TrimEnd())).TrimEnd('\n');
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>
    /// The canonical content hashes of the transitive prerequisite stages' produced artifacts, as
    /// <c>&lt;relativePath&gt;:&lt;hash&gt;</c> entries — one per distinct produced artifact path, ordered. Editing
    /// any upstream artifact changes this set; re-stamping an upstream stage (no content change) does not.
    /// </summary>
    public static IReadOnlyList<string> PrerequisiteArtifactHashes(
        string repositoryRoot, StageModel stageModel, string stageId, string feature)
    {
        var byPath = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (CycleStage prereq in stageModel.TransitivePrereqStages(stageId))
        {
            if (prereq.Produces is not { } pattern)
            {
                continue;
            }

            string relativePath = FreshnessEvaluator.ResolveProduces(pattern, feature);
            if (byPath.ContainsKey(relativePath))
            {
                continue; // specify + clarify both produce the spec; bind the artifact once
            }

            string full = Path.GetFullPath(
                Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            byPath[relativePath] = File.Exists(full) ? CanonicalHashOfFile(full) : "absent";
        }

        return byPath.Select(kv => $"{kv.Key}:{kv.Value}").ToList();
    }

    [GeneratedRegex(@"\s*<!--\s*doti-task-hash:\s*[a-fA-F0-9]{64}\s*-->")]
    private static partial Regex TaskHashMarkerRegex();

    [GeneratedRegex(@"(?m)^(?<prefix>- \[)[ xX](?<suffix>\]\s+`T[0-9A-Za-z]+`)")]
    private static partial Regex TaskCheckboxRegex();
}
