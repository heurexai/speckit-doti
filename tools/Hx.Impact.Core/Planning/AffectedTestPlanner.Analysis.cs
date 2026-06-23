using Hx.Impact.Core.Domain;
using Hx.Impact.Core.Graph;

namespace Hx.Impact.Core.Planning;

public sealed partial class AffectedTestPlanner
{
    private static ChangeImpact AnalyzeChangedPaths(ProjectGraph graph, IReadOnlyList<string> changedPaths)
    {
        var reasons = new List<string>(graph.Findings.Select(finding => "graph-finding:" + finding));
        bool escalate = graph.Findings.Count > 0;
        bool anyImpactingPath = false;
        var changedProduction = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var changedTestProjects = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string path in changedPaths)
        {
            PathImpact impact = AnalyzePath(path, graph);
            escalate |= impact.Escalates;
            anyImpactingPath |= impact.IsImpacting;
            if (impact.Reason is not null)
            {
                reasons.Add(impact.Reason);
            }

            if (impact.ProductionProject is not null)
            {
                changedProduction.Add(impact.ProductionProject);
            }

            if (impact.TestProject is not null)
            {
                changedTestProjects.Add(impact.TestProject);
            }
        }

        return new ChangeImpact(reasons, escalate, anyImpactingPath, changedProduction, changedTestProjects);
    }

    private static PathImpact AnalyzePath(string path, ProjectGraph graph)
    {
        Category category = Classify(path, graph, out string? owner);
        return category switch
        {
            Category.Generated or Category.Documentation => PathImpact.NonImpacting,
            Category.Broad => new PathImpact(true, true, "broad-input:" + path, null, null),
            Category.Owned when graph.Nodes[owner!].IsTestProject => new PathImpact(false, true, null, null, owner),
            Category.Owned => new PathImpact(false, true, null, owner, null),
            _ => new PathImpact(true, true, "unattributed-path:" + path, null, null),
        };
    }

    private static AffectedSelection SelectAffected(ProjectGraph graph, ChangeImpact impact)
    {
        var affectedSource = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var affectedTestProjects = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string production in impact.ChangedProduction)
        {
            AddDownstreamProjects(graph, production, affectedSource, affectedTestProjects);
        }

        foreach (string testProject in impact.ChangedTestProjects)
        {
            affectedTestProjects.Add(testProject);
        }

        return new AffectedSelection(affectedSource, affectedTestProjects);
    }

    private static void AddDownstreamProjects(
        ProjectGraph graph,
        string production,
        ISet<string> affectedSource,
        ISet<string> affectedTestProjects)
    {
        foreach (string dependent in graph.DownstreamOf(production))
        {
            if (graph.Nodes[dependent].IsTestProject) { affectedTestProjects.Add(dependent); }
            else { affectedSource.Add(dependent); }
        }
    }

    private enum Category { Generated, Documentation, Broad, Owned, Unknown }

    private static Category Classify(string path, ProjectGraph graph, out string? owner)
    {
        owner = null;
        string normalized = path.Replace('\\', '/');
        string[] segments = normalized.Split('/');
        if (segments.Any(s => s is "bin" or "obj"))
        {
            return Category.Generated;
        }

        string file = segments[^1];
        if (IsBroad(normalized, file))
        {
            return Category.Broad;
        }

        if (file.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || normalized.StartsWith("docs/", StringComparison.OrdinalIgnoreCase))
        {
            return Category.Documentation;
        }

        owner = OwningProject(normalized, graph);
        return owner is null ? Category.Unknown : Category.Owned;
    }

    private static bool IsBroad(string normalized, string file) =>
        file is "Directory.Build.props" or "Directory.Packages.props" or "global.json" or "nuget.config" or ".gitignore"
        || file.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
        || file.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
        || normalized.StartsWith("rules/", StringComparison.OrdinalIgnoreCase)
        || normalized.StartsWith(".sentrux", StringComparison.OrdinalIgnoreCase)
        || normalized.StartsWith("doti/", StringComparison.OrdinalIgnoreCase)
        || normalized.StartsWith(".config/", StringComparison.OrdinalIgnoreCase);

    // The changed file's owning project = the graph node whose directory is the longest path-prefix.
    private static string? OwningProject(string path, ProjectGraph graph)
    {
        string? best = null;
        int bestLength = -1;
        foreach (ProjectNode node in graph.Nodes.Values)
        {
            int slash = node.Path.LastIndexOf('/');
            string directory = slash >= 0 ? node.Path[..slash] : string.Empty;
            bool owned = string.Equals(path, node.Path, StringComparison.OrdinalIgnoreCase)
                || (directory.Length > 0 && path.StartsWith(directory + "/", StringComparison.OrdinalIgnoreCase));
            if (owned && directory.Length > bestLength)
            {
                best = node.Path;
                bestLength = directory.Length;
            }
        }

        return best;
    }

    private sealed record ChangeImpact(
        IReadOnlyList<string> Reasons,
        bool Escalate,
        bool AnyImpactingPath,
        IReadOnlyCollection<string> ChangedProduction,
        IReadOnlyCollection<string> ChangedTestProjects);

    private sealed record PathImpact(
        bool Escalates,
        bool IsImpacting,
        string? Reason,
        string? ProductionProject,
        string? TestProject)
    {
        public static readonly PathImpact NonImpacting = new(false, false, null, null, null);
    }

    private sealed record AffectedSelection(
        IReadOnlyCollection<string> SourceProjects,
        IReadOnlyCollection<string> TestProjects);
}
