using Hx.Tooling.Contracts;

namespace Hx.Cycle.Core;

/// <summary>
/// Projects a <see cref="ChangeSetContext"/> into a <see cref="ReviewContext"/> (FR-025): categorise each changed
/// file, then union the arch-review lenses each category activates — promoting the panel's <em>Applies-when</em>
/// triage from skill prose to a deterministic, unit-testable projection (SC-011/012/013). Pure: the
/// <see cref="LayerMap"/> is constructor-injected, and an unresolved map fails closed to ALL lenses.
/// </summary>
public sealed class ReviewContextProjector
{
    private readonly LayerMap _layers;

    public ReviewContextProjector(LayerMap layers) => _layers = layers;

    public ReviewContext Project(ChangeSetContext changeSet)
    {
        var categories = new SortedSet<ReviewCategory>();
        var applicable = new SortedSet<string>(StringComparer.Ordinal);
        var escalations = new SortedSet<string>(StringComparer.Ordinal);

        bool failClosed = !_layers.IsResolved;
        if (failClosed)
        {
            escalations.Add("layer map unresolved (.sentrux/rules.toml missing/malformed); all lenses (fail-closed)");
        }

        foreach (ChangedFile file in changeSet.Files)
        {
            ReviewCategory category = Categorize(file.Path);
            categories.Add(category);
            foreach (string lens in LensesFor(category))
            {
                applicable.Add(lens);
            }

            if (category is ReviewCategory.Broad or ReviewCategory.Unknown)
            {
                escalations.Add($"{Describe(category)}:{file.Path}");
            }
        }

        // Fail-closed: if we cannot prove the change does not touch layering, run every lens.
        if (failClosed && changeSet.Files.Count > 0)
        {
            foreach (string lens in ReviewLens.All)
            {
                applicable.Add(lens);
            }
        }

        IReadOnlyList<string> skipped = ReviewLens.All.Where(l => !applicable.Contains(l)).ToList();
        return new ReviewContext(
            JsonContractDefaults.SchemaVersion,
            changeSet.Files.Select(f => f.Path).ToList(),
            changeSet.AffectedSourceProjects,
            categories.ToList(),
            applicable.ToList(),
            skipped,
            escalations.ToList());
    }

    private ReviewCategory Categorize(string path)
    {
        string normalized = path.Replace('\\', '/');
        string[] segments = normalized.Split('/');
        string file = segments[^1];

        if (segments.Any(s => s is "bin" or "obj"))
        {
            return ReviewCategory.Generated;
        }

        // Generated-code templates are CODE — they become a real repo, so they run the code lenses.
        if (normalized.StartsWith("scaffold/templates/", StringComparison.OrdinalIgnoreCase))
        {
            return ReviewCategory.GeneratedTemplate;
        }

        // A project / solution change is a dependency-edge / layering surface (FR-027, SC-013).
        if (file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            return ReviewCategory.DependencyEdge;
        }

        // Contracts layer + the append-only error-code registry.
        if (string.Equals(_layers.LayerOf(normalized), "contracts", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("errorcodes/", StringComparison.OrdinalIgnoreCase))
        {
            return ReviewCategory.Contract;
        }

        // Doti prose: command templates, skills, agent-context, the .doti tree's non-code assets.
        if (normalized.StartsWith(".doti/", StringComparison.OrdinalIgnoreCase) && !IsCodeExtension(file))
        {
            return ReviewCategory.DotiProse;
        }

        if (file.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("docs/", StringComparison.OrdinalIgnoreCase))
        {
            return ReviewCategory.DocsOnly;
        }

        if (IsBroad(normalized, file))
        {
            return ReviewCategory.Broad;
        }

        if (IsCodeExtension(file)
            && (normalized.StartsWith("src/", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("tools/", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("test/", StringComparison.OrdinalIgnoreCase)))
        {
            return ReviewCategory.RuntimeCode;
        }

        return ReviewCategory.Unknown;
    }

    private static IReadOnlyList<string> LensesFor(ReviewCategory category) => category switch
    {
        ReviewCategory.Generated => [],
        ReviewCategory.DocsOnly or ReviewCategory.DotiProse => [ReviewLens.DesignSoundness],
        ReviewCategory.Contract =>
            [ReviewLens.DesignSoundness, ReviewLens.DataContractIntegrity, ReviewLens.BlastRadiusDependencies],
        ReviewCategory.DependencyEdge =>
            [ReviewLens.DesignSoundness, ReviewLens.BlastRadiusDependencies, ReviewLens.FitWithCurrentArchitecture],
        ReviewCategory.RuntimeCode or ReviewCategory.GeneratedTemplate => ReviewLens.Code,
        _ => ReviewLens.All, // Broad / Unknown escalate to every lens
    };

    private static string Describe(ReviewCategory category) =>
        category == ReviewCategory.Broad ? "broad-input" : "unattributed-path";

    private static bool IsCodeExtension(string file) =>
        file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
        || file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);

    private static bool IsBroad(string normalized, string file) =>
        file is "Directory.Build.props" or "Directory.Packages.props" or "global.json" or "nuget.config" or ".gitignore"
        || normalized.StartsWith("rules/", StringComparison.OrdinalIgnoreCase)
        || normalized.StartsWith(".sentrux", StringComparison.OrdinalIgnoreCase)
        || normalized.StartsWith(".config/", StringComparison.OrdinalIgnoreCase);
}
