using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;

namespace Hx.Impact.Core.ChangeDetection;

/// <summary>The git seam for per-file line counts: <c>git diff --numstat &lt;base&gt;..HEAD</c> (∪ the working tree).
/// Injected so <see cref="ChangeSummaryProjector"/> is unit-testable without a real repository.</summary>
public interface INumstatReader
{
    /// <summary>One entry per changed file: the added/removed line counts and the '/'-normalized repo-relative path.
    /// A binary file reports <c>added == removed == 0</c> (git prints "-" for binary, which we map to 0).</summary>
    IReadOnlyList<NumstatEntry> Read(string repositoryRoot, string baseRef, string headRef);
}

/// <summary>One <c>git diff --numstat</c> row: lines added/removed for a changed path.</summary>
public sealed record NumstatEntry(int Added, int Removed, string Path);

/// <summary>The production numstat seam: unions the committed <c>base..head</c> diff with the working tree so
/// uncommitted edits are counted, matching the change-set collector's union semantics.</summary>
public sealed class GitNumstatReader : INumstatReader
{
    public IReadOnlyList<NumstatEntry> Read(string repositoryRoot, string baseRef, string headRef)
    {
        // Committed diff base..head, then the working tree (staged + unstaged) unioned. First-seen path wins so a
        // file edited in both the history and the working tree is counted once (the committed total).
        string committed = Git(repositoryRoot, "diff", "--numstat", "-M", $"{baseRef}..{headRef}");
        string working = Git(repositoryRoot, "diff", "--numstat", "-M", "HEAD");
        return ChangeSummaryProjector.ParseNumstat(committed, working);
    }

    private static string Git(string repositoryRoot, params string[] arguments) =>
        ProcessRunner.Run(new ToolCommand("git", arguments, repositoryRoot)).StandardOutput;
}

/// <summary>
/// 012 (FR-020/021): projects a <see cref="ChangeSetContext"/> + per-file line counts into a bounded
/// <see cref="ChangeSummary"/> — file counts by category (source/test/docs/other), total lines ±, a capped
/// changed-file list, and (detailed tier only) the top-level class names touched in the changed <c>.cs</c>.
/// Deterministic Ordinal ordering; lists capped with an explicit "+N more" (FR-013/018). Telemetry only — never a
/// proof input (M1).
/// </summary>
public sealed class ChangeSummaryProjector
{
    /// <summary>The cap for the rendered file/class lists; the overflow becomes a single "+N more" marker.</summary>
    public const int ListCap = 12;

    private readonly INumstatReader _numstat;

    public ChangeSummaryProjector(INumstatReader? numstat = null) => _numstat = numstat ?? new GitNumstatReader();

    /// <summary>
    /// Build the basic summary (files + lines, no classes) for every gate. <paramref name="includeClasses"/> turns on
    /// the detailed tier: <see cref="ClassesTouched(string, IEnumerable{string})"/> scans the changed <c>.cs</c> for
    /// top-level type names. <paramref name="repositoryRoot"/> is used both for the numstat seam and to read the
    /// changed <c>.cs</c> files for the class scan.
    /// </summary>
    public ChangeSummary Project(
        string repositoryRoot,
        ChangeSetContext context,
        string baseRef,
        string headRef,
        bool includeClasses)
    {
        IReadOnlyList<NumstatEntry> numstat = _numstat.Read(repositoryRoot, baseRef, headRef);
        return Build(repositoryRoot, context, numstat, includeClasses);
    }

    /// <summary>Pure projection (no git/IO) — the testable core. <paramref name="numstat"/> supplies line counts;
    /// the class scan reads the changed <c>.cs</c> from disk only when <paramref name="includeClasses"/> is set.</summary>
    public static ChangeSummary Build(
        string repositoryRoot,
        ChangeSetContext context,
        IReadOnlyList<NumstatEntry> numstat,
        bool includeClasses)
    {
        var files = context.Files.Select(f => Normalize(f.Path)).ToArray();
        int source = files.Count(IsSource);
        int test = files.Count(IsTest);
        int docs = files.Count(IsDocs);
        int other = files.Length - source - test - docs;

        int added = numstat.Sum(n => n.Added);
        int removed = numstat.Sum(n => n.Removed);

        IReadOnlyList<string> fileList = Cap(files.OrderBy(p => p, StringComparer.Ordinal));
        IReadOnlyList<string> classes = includeClasses
            ? ClassesTouched(repositoryRoot, files.Where(IsCSharp))
            : [];

        return new ChangeSummary(source, test, docs, other, added, removed, fileList, classes, includeClasses);
    }

    /// <summary>Scan the changed <c>.cs</c> files for their top-level type names (lexer-aware so a <c>class</c> token in
    /// a string/comment is never counted). Missing/unreadable files are skipped. Deterministic, deduped, capped.</summary>
    public static IReadOnlyList<string> ClassesTouched(string repositoryRoot, IEnumerable<string> changedCsPaths)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string relative in changedCsPaths)
        {
            string full = Path.GetFullPath(Path.Combine(repositoryRoot, relative));
            string? source = TryRead(full);
            if (source is null)
            {
                continue;
            }

            foreach (string name in TopLevelTypeScanner.Scan(source))
            {
                names.Add(name);
            }
        }

        return Cap(names);
    }

    /// <summary>Parse a <c>git diff --numstat</c> stream (committed unioned with the working tree, first-seen wins).
    /// Public so the production reader and tests share one parse. A binary row ("-\t-\tpath") maps to 0/0.</summary>
    public static IReadOnlyList<NumstatEntry> ParseNumstat(string committed, string working)
    {
        var byPath = new Dictionary<string, NumstatEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (NumstatEntry entry in ParseNumstatStream(committed).Concat(ParseNumstatStream(working)))
        {
            byPath.TryAdd(entry.Path, entry);
        }

        return byPath.Values.OrderBy(e => e.Path, StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<NumstatEntry> ParseNumstatStream(string output)
    {
        if (string.IsNullOrEmpty(output))
        {
            yield break;
        }

        foreach (string raw in output.Split('\n'))
        {
            string line = raw.Trim('\r', ' ', '\t');
            if (line.Length == 0)
            {
                continue;
            }

            string[] parts = line.Split('\t');
            if (parts.Length < 3)
            {
                continue;
            }

            int added = ParseCount(parts[0]);
            int removed = ParseCount(parts[1]);
            // A rename row is "added\tremoved\told => new" or NUL-rename form; take the last path segment as the new path.
            string path = Normalize(RenameTarget(parts[2]));
            yield return new NumstatEntry(added, removed, path);
        }
    }

    // git prints "-" for binary files; treat as 0. Any non-numeric token is defensively 0.
    private static int ParseCount(string token) => int.TryParse(token.Trim(), out int value) ? value : 0;

    // A numstat rename can render as "old => new" or "dir/{old => new}/file". Take the new path conservatively.
    private static string RenameTarget(string token)
    {
        int arrow = token.IndexOf("=>", StringComparison.Ordinal);
        if (arrow < 0)
        {
            return token;
        }

        string after = token[(arrow + 2)..].Trim().TrimEnd('}');
        string before = token[..arrow];
        int brace = before.LastIndexOf('{');
        string prefix = brace >= 0 ? before[..brace] : string.Empty;
        return (prefix + after).Replace("//", "/");
    }

    private static IReadOnlyList<string> Cap(IEnumerable<string> ordered)
    {
        var list = ordered.ToList();
        if (list.Count <= ListCap)
        {
            return list;
        }

        var capped = list.Take(ListCap).ToList();
        capped.Add($"+{list.Count - ListCap} more");
        return capped;
    }

    private static string? TryRead(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsCSharp(string path) => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

    private static bool IsSource(string path)
    {
        bool codeExtension = path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);
        return codeExtension && (UnderTopLevel(path, "src") || UnderTopLevel(path, "tools")) && !IsTest(path);
    }

    private static bool IsTest(string path) => UnderTopLevel(path, "test");

    private static bool IsDocs(string path) =>
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || UnderTopLevel(path, "docs");

    private static bool UnderTopLevel(string path, string segment) =>
        path.StartsWith(segment + "/", StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) => path.Replace('\\', '/').Trim();
}
