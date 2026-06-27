using Hx.Tooling.Contracts;

namespace Hx.Impact.Core.ChangeDetection;

/// <summary>
/// Pure parsing of git's <c>diff --name-status -M -z</c> + <c>status --porcelain=v1 -z</c> output into the
/// deduped changed-file set. BL-3 (load-bearing): the dedup is by path with <see cref="StringComparer.OrdinalIgnoreCase"/>,
/// a rename/copy collapses to the NEW path only (the old path is metadata, never a separate entry), and the diff
/// set is unioned before the working-tree set (first-seen casing wins) — exactly the set
/// <see cref="ImpactChangeCollector"/> produced before generalisation, so <c>ChangeSetIdentity.Compute</c> over
/// <c>Files.Select(f =&gt; f.Path)</c> stays byte-identical and no stamped proof is silently invalidated.
/// </summary>
public static class ChangeSetParser
{
    public static IReadOnlyList<ChangedFile> Parse(string diffNameStatusZ, string statusPorcelainZ)
    {
        // First-seen wins per OrdinalIgnoreCase path: the committed diff is unioned before the working tree,
        // matching the prior SortedSet(OrdinalIgnoreCase) Add order. Final order is OrdinalIgnoreCase-sorted so
        // Files.Select(Path) equals the prior sorted set regardless of Dictionary enumeration order.
        var byPath = new Dictionary<string, ChangedFile>(StringComparer.OrdinalIgnoreCase);
        foreach (ChangedFile file in ParseNameStatusZ(diffNameStatusZ))
        {
            byPath.TryAdd(file.Path, file);
        }

        foreach (ChangedFile file in ParsePorcelainZ(statusPorcelainZ))
        {
            byPath.TryAdd(file.Path, file);
        }

        return byPath.Values.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    // `diff --name-status -z`: NUL-separated [status, path]; rename/copy is [status, old, new].
    private static IEnumerable<ChangedFile> ParseNameStatusZ(string output)
    {
        if (string.IsNullOrEmpty(output)) { yield break; }

        string[] tokens = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length;)
        {
            string status = tokens[i++];
            ChangeStatus kind = MapDiffStatus(status);
            if ((status.StartsWith('R') || status.StartsWith('C')) && i + 1 < tokens.Length)
            {
                string oldPath = Normalize(tokens[i++]);
                yield return new ChangedFile(Normalize(tokens[i++]), kind, oldPath);
            }
            else if (i < tokens.Length)
            {
                yield return new ChangedFile(Normalize(tokens[i++]), kind, null);
            }
        }
    }

    // `status --porcelain=v1 -z`: each record is "XY <path>"; a rename record is followed by its old-path token.
    private static IEnumerable<ChangedFile> ParsePorcelainZ(string output)
    {
        if (string.IsNullOrEmpty(output)) { yield break; }

        string[] tokens = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (token.Length < 4) { continue; }

            string xy = token[..2];
            string path = Normalize(token[3..]); // the new path; the old path (renames) follows and is captured below
            string? oldPath = null;
            if (xy.Contains('R') && i + 1 < tokens.Length)
            {
                oldPath = Normalize(tokens[++i]);
            }

            yield return new ChangedFile(path, MapPorcelainStatus(xy), oldPath);
        }
    }

    private static ChangeStatus MapDiffStatus(string status) =>
        status.Length == 0 ? ChangeStatus.Unknown : MapStatusLetter(status[0]);

    // Prefer the index (X) status; fall back to the worktree (Y) for unstaged/untracked changes.
    private static ChangeStatus MapPorcelainStatus(string xy)
    {
        if (xy == "??") { return ChangeStatus.Untracked; }
        if (xy.Contains('U')) { return ChangeStatus.Unmerged; }
        char index = xy[0];
        return index is not (' ' or '?') ? MapStatusLetter(index) : MapStatusLetter(xy[1]);
    }

    private static ChangeStatus MapStatusLetter(char letter) => letter switch
    {
        'A' => ChangeStatus.Added,
        'M' => ChangeStatus.Modified,
        'D' => ChangeStatus.Deleted,
        'R' => ChangeStatus.Renamed,
        'C' => ChangeStatus.Copied,
        'T' => ChangeStatus.TypeChanged,
        'U' => ChangeStatus.Unmerged,
        '?' => ChangeStatus.Untracked,
        _ => ChangeStatus.Unknown,
    };

    private static string Normalize(string path) => path.Replace('\\', '/').Trim();
}
