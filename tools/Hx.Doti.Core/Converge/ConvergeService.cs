using System.Text;
using System.Text.RegularExpressions;

namespace Hx.Doti.Core.Converge;

/// <summary>The deterministic coverage gap between a spec and its tasks: which requirements are unmapped.</summary>
public sealed record ConvergeAnalysis(
    IReadOnlyList<string> SpecRequirements,
    IReadOnlyList<string> CoveredRequirements,
    IReadOnlyList<string> UncoveredRequirements);

/// <summary>
/// 007 T038 (FR-039): the deterministic half of <c>converge</c> — brownfield/drift reconciliation of a spec against
/// its tasks. It computes the requirement coverage gap: every <c>FR-###</c>/<c>SC-###</c> the spec defines that NO
/// task claims to cover (via a <c>covers …</c> marker) is candidate unbuilt work. The agent then assesses each
/// against the codebase and appends the genuinely-missing ones as tasks (the converge command guides that), so the
/// ledger and the ordered-task gate stay in control — the command never blindly rewrites the tasks file.
/// </summary>
public static partial class ConvergeService
{
    [GeneratedRegex(@"\b(?:FR|SC)-\d+\b", RegexOptions.IgnoreCase)]
    private static partial Regex RequirementId();

    // The coverage signal is a task's `covers …` marker (e.g. `[covers FR-001, SC-002]`) — NOT a bare requirement
    // mention in a task's prose, which would over-count coverage.
    [GeneratedRegex(@"covers[^\]\r\n]*", RegexOptions.IgnoreCase)]
    private static partial Regex CoversMarker();

    public static ConvergeAnalysis Analyze(string specText, string tasksText)
    {
        IReadOnlyList<string> spec = RequirementIds(specText);
        IReadOnlyList<string> covered = RequirementIds(CoversText(tasksText));
        var coveredSet = new HashSet<string>(covered, StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<string> uncovered = spec.Where(id => !coveredSet.Contains(id)).ToArray();
        return new ConvergeAnalysis(spec, covered, uncovered);
    }

    private static IReadOnlyList<string> RequirementIds(string text) =>
        RequirementId().Matches(text)
            .Select(match => match.Value.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

    private static string CoversText(string tasksText)
    {
        var sb = new StringBuilder();
        foreach (Match match in CoversMarker().Matches(tasksText))
        {
            sb.AppendLine(match.Value);
        }

        return sb.ToString();
    }
}
