using System.Text.RegularExpressions;

namespace Hx.Cycle.Core;

/// <summary>
/// The architecture layer map (contracts → core → cli) read from <c>.sentrux/rules.toml</c>'s <c>[[layers]]</c>
/// blocks, used by the review-context projector and <c>ArchitectureRelevantSurface</c> to decide a path's layer and
/// whether a change touches dependency direction/layering (FR-027). FAIL-CLOSED (L-2): if the file is missing or
/// cannot be parsed, <see cref="IsResolved"/> is false and the consumer treats EVERY change as layering-relevant —
/// arch-review over-runs rather than silently skipping. Injected (constructor-set) so the projector stays a pure unit.
/// </summary>
public sealed partial class LayerMap
{
    private readonly IReadOnlyList<LayerEntry> _layers;

    private LayerMap(IReadOnlyList<LayerEntry> layers, bool resolved)
    {
        _layers = layers;
        IsResolved = resolved;
    }

    /// <summary>False when the layer config could not be read/parsed — consumers must then fail closed.</summary>
    public bool IsResolved { get; }

    public static LayerMap Load(string repositoryRoot)
    {
        string path = Path.Combine(repositoryRoot, ".sentrux", "rules.toml");
        if (!File.Exists(path))
        {
            return Unresolved();
        }

        try
        {
            IReadOnlyList<LayerEntry> layers = Parse(File.ReadAllText(path));
            return layers.Count > 0 ? new LayerMap(layers, resolved: true) : Unresolved();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Unresolved();
        }
    }

    /// <summary>An explicit map (for tests / fail-closed defaults).</summary>
    public static LayerMap FromLayers(IEnumerable<(string Name, IReadOnlyList<string> Prefixes)> layers) =>
        new(layers.Select(l => new LayerEntry(l.Name, Normalize(l.Prefixes))).ToList(), resolved: true);

    public static LayerMap Unresolved() => new([], resolved: false);

    /// <summary>The layer a repo-relative path belongs to (longest-prefix match), or null if none.</summary>
    public string? LayerOf(string path)
    {
        string normalized = path.Replace('\\', '/');
        string? best = null;
        int bestLength = -1;
        foreach (LayerEntry layer in _layers)
        {
            foreach (string prefix in layer.Prefixes)
            {
                if ((normalized.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                        || normalized.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
                    && prefix.Length > bestLength)
                {
                    best = layer.Name;
                    bestLength = prefix.Length;
                }
            }
        }

        return best;
    }

    // Minimal parse of the stable `[[layers]]` blocks: name = "..." and paths = ["a/*", "b/*"] (single- or
    // multi-line). A trailing `/*` is stripped to a directory prefix. Not a general TOML parser — just this section.
    private static IReadOnlyList<LayerEntry> Parse(string toml)
    {
        var layers = new List<LayerEntry>();
        string[] lines = toml.Replace("\r\n", "\n").Split('\n');
        string? name = null;
        var prefixes = new List<string>();
        bool inLayer = false;
        bool inPaths = false;

        void Flush()
        {
            if (inLayer && name is not null && prefixes.Count > 0)
            {
                layers.Add(new LayerEntry(name, Normalize(prefixes)));
            }
        }

        foreach (string raw in lines)
        {
            string line = StripComment(raw).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line is "[[layers]]")
            {
                Flush();
                name = null;
                prefixes = [];
                inLayer = true;
                inPaths = false;
                continue;
            }

            if (line.StartsWith('[') && line != "[[layers]]")
            {
                Flush();
                inLayer = false;
                inPaths = false;
                continue;
            }

            if (!inLayer)
            {
                continue;
            }

            if (NameRegex().Match(line) is { Success: true } nameMatch)
            {
                name = nameMatch.Groups["v"].Value;
            }
            else if (line.StartsWith("paths", StringComparison.Ordinal))
            {
                inPaths = !line.Contains(']'); // multi-line array stays open until the closing ]
                prefixes.AddRange(QuotedRegex().Matches(line).Select(m => m.Groups["v"].Value));
            }
            else if (inPaths)
            {
                prefixes.AddRange(QuotedRegex().Matches(line).Select(m => m.Groups["v"].Value));
                if (line.Contains(']'))
                {
                    inPaths = false;
                }
            }
        }

        Flush();
        return layers;
    }

    private static IReadOnlyList<string> Normalize(IEnumerable<string> prefixes) =>
        prefixes.Select(p => p.Replace('\\', '/').TrimEnd('/', '*').TrimEnd('/')).Where(p => p.Length > 0).ToList();

    private static string StripComment(string line)
    {
        int hash = line.IndexOf('#');
        return hash >= 0 ? line[..hash] : line;
    }

    [GeneratedRegex(@"^name\s*=\s*""(?<v>[^""]*)""")]
    private static partial Regex NameRegex();

    [GeneratedRegex(@"""(?<v>[^""]+)""")]
    private static partial Regex QuotedRegex();

    private sealed record LayerEntry(string Name, IReadOnlyList<string> Prefixes);
}
