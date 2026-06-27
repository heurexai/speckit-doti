using System.Text;

namespace Hx.Doti.Core;

public sealed record DotiGitIgnorePlan(
    string RelativePath,
    bool FileExists,
    IReadOnlyList<string> MissingEntries)
{
    public bool ShouldWrite => MissingEntries.Count > 0;
}

public static class DotiGitIgnore
{
    public const string RelativePath = ".gitignore";

    public static readonly IReadOnlyList<string> RuntimeStateEntries =
    [
        ".doti/cycle-state.json",
        ".doti/gate-proof.json",
        ".doti/sentrux-optimization-log.json",
        // FR-014: .doti/templates is materialized from .doti/core/templates at install — generated, never committed.
        ".doti/templates/",
    ];

    public static DotiGitIgnorePlan Plan(string repositoryRoot)
    {
        string path = Path.Combine(repositoryRoot, RelativePath);
        if (!File.Exists(path))
        {
            return new DotiGitIgnorePlan(RelativePath, FileExists: false, RuntimeStateEntries);
        }

        string[] existing = File.ReadAllLines(path);
        HashSet<string> normalized = existing
            .Select(NormalizeEntry)
            .Where(entry => entry.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string[] missing = RuntimeStateEntries
            .Where(entry => !normalized.Contains(NormalizeEntry(entry)))
            .ToArray();
        return new DotiGitIgnorePlan(RelativePath, FileExists: true, missing);
    }

    public static IReadOnlyList<string> Ensure(string repositoryRoot)
    {
        DotiGitIgnorePlan plan = Plan(repositoryRoot);
        if (!plan.ShouldWrite)
        {
            return [];
        }

        string path = Path.Combine(repositoryRoot, RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        string existing = File.Exists(path) ? File.ReadAllText(path) : "";
        var builder = new StringBuilder(existing);
        if (builder.Length > 0 && !EndsWithNewLine(existing))
        {
            builder.AppendLine();
        }

        if (builder.Length > 0 && !IsBlankLastLine(existing))
        {
            builder.AppendLine();
        }

        builder.AppendLine("# Doti cycle working state (local; durable proof is in commits)");
        foreach (string entry in plan.MissingEntries)
        {
            builder.AppendLine(entry);
        }

        File.WriteAllText(path, builder.ToString());
        return [RelativePath];
    }

    private static string NormalizeEntry(string entry)
    {
        string trimmed = entry.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
        {
            return "";
        }

        return trimmed.Replace('\\', '/').TrimStart('/');
    }

    private static bool EndsWithNewLine(string value) =>
        value.EndsWith('\n') || value.EndsWith('\r');

    private static bool IsBlankLastLine(string value)
    {
        if (value.Length == 0)
        {
            return true;
        }

        string normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
        int lastBreak = normalized.LastIndexOf('\n');
        string lastLine = lastBreak >= 0 ? normalized[(lastBreak + 1)..] : normalized;
        return string.IsNullOrWhiteSpace(lastLine);
    }
}
