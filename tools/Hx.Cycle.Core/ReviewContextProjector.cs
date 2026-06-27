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

    // Ordered dispatch (first match wins). Each compound test is a named predicate so this method stays a flat, low-
    // complexity router — the boolean fan-out lives in the helpers, not here.
    private ReviewCategory Categorize(string path)
    {
        string normalized = path.Replace('\\', '/');
        string file = normalized.Split('/')[^1];

        if (IsGenerated(normalized))
        {
            return ReviewCategory.Generated;
        }

        if (IsGeneratedTemplate(normalized))
        {
            return ReviewCategory.GeneratedTemplate;
        }

        if (IsDependencyEdge(file))
        {
            return ReviewCategory.DependencyEdge;
        }

        if (IsContract(normalized))
        {
            return ReviewCategory.Contract;
        }

        if (IsDotiProse(normalized, file))
        {
            return ReviewCategory.DotiProse;
        }

        if (IsDocs(normalized, file))
        {
            return ReviewCategory.DocsOnly;
        }

        if (IsBroad(normalized, file))
        {
            return ReviewCategory.Broad;
        }

        if (IsRuntimeCode(normalized, file))
        {
            return ReviewCategory.RuntimeCode;
        }

        return ReviewCategory.Unknown;
    }

    // bin/obj output is generated, never reviewed.
    private static bool IsGenerated(string normalized) =>
        normalized.Split('/').Any(s => s is "bin" or "obj");

    // Generated-code templates are CODE — they become a real repo, so they run the code lenses.
    private static bool IsGeneratedTemplate(string normalized) =>
        normalized.StartsWith("scaffold/templates/", StringComparison.OrdinalIgnoreCase);

    // A project / solution change is a dependency-edge / layering surface (FR-027, SC-013).
    private static bool IsDependencyEdge(string file) =>
        file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
        || file.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
        || file.EndsWith(".sln", StringComparison.OrdinalIgnoreCase);

    // Contracts layer + the append-only error-code registry.
    private bool IsContract(string normalized) =>
        string.Equals(_layers.LayerOf(normalized), "contracts", StringComparison.OrdinalIgnoreCase)
        || normalized.StartsWith("errorcodes/", StringComparison.OrdinalIgnoreCase);

    // Doti prose: command templates, skills, agent-context, the .doti tree's non-code assets.
    private static bool IsDotiProse(string normalized, string file) =>
        normalized.StartsWith(".doti/", StringComparison.OrdinalIgnoreCase) && !IsCodeExtension(file);

    private static bool IsDocs(string normalized, string file) =>
        file.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
        || normalized.StartsWith("docs/", StringComparison.OrdinalIgnoreCase);

    private static bool IsRuntimeCode(string normalized, string file) =>
        IsCodeExtension(file)
        && (normalized.StartsWith("src/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("tools/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("test/", StringComparison.OrdinalIgnoreCase));

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
