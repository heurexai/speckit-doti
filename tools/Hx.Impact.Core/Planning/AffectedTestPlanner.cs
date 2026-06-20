using Hx.Impact.Core.ChangeDetection;
using Hx.Impact.Core.Domain;
using Hx.Impact.Core.Graph;
using Hx.Tooling.Contracts;

namespace Hx.Impact.Core.Planning;

/// <summary>
/// The deterministic, project-graph affected-test planner. Classifies each changed path, resolves the
/// reverse-dependency closure of the changed production projects to the test projects that could be
/// affected, and emits the exact `dotnet test` commands — or escalates to <c>full-gate-required</c>
/// whenever it cannot prove a safe narrowing (broad/unattributed input, graph drift). It never under-selects.
/// </summary>
public sealed class AffectedTestPlanner
{
    /// <summary>End-to-end: discover the solution, build the graph, collect the change set, resolve the plan.</summary>
    public AffectedPlan Plan(string repositoryRoot, string baseRef, string headRef, string configuration)
    {
        string solutionFileName = DiscoverSolution(repositoryRoot);
        ProjectGraph graph = new ProjectGraphBuilder().Build(repositoryRoot, solutionFileName);
        IReadOnlyList<string> changed = new ImpactChangeCollector().Collect(repositoryRoot, baseRef, headRef);
        return Resolve(graph, changed, configuration);
    }

    /// <summary>Pure resolution (no git/IO) — the testable core.</summary>
    public static AffectedPlan Resolve(ProjectGraph graph, IReadOnlyList<string> changedPaths, string configuration)
    {
        var reasons = new List<string>();
        foreach (string finding in graph.Findings)
        {
            reasons.Add("graph-finding:" + finding);
        }

        bool escalate = graph.Findings.Count > 0;
        bool anyImpactingPath = false;
        var changedProduction = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var changedTestProjects = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string path in changedPaths)
        {
            switch (Classify(path, graph, out string? owner))
            {
                case Category.Generated:
                case Category.Documentation:
                    break;
                case Category.Broad:
                    escalate = true;
                    anyImpactingPath = true;
                    reasons.Add("broad-input:" + path);
                    break;
                case Category.Owned:
                    anyImpactingPath = true;
                    if (graph.Nodes[owner!].IsTestProject) { changedTestProjects.Add(owner!); }
                    else { changedProduction.Add(owner!); }
                    break;
                default:
                    escalate = true;
                    anyImpactingPath = true;
                    reasons.Add("unattributed-path:" + path);
                    break;
            }
        }

        if (escalate)
        {
            return new AffectedPlan(JsonContractDefaults.SchemaVersion, AffectedOutcome.FullGateRequired, [], [], Dedup(reasons));
        }

        if (!anyImpactingPath)
        {
            return new AffectedPlan(JsonContractDefaults.SchemaVersion, AffectedOutcome.NoTestsRequired, [], [],
                reasons.Count == 0 ? ["No code or test changes (documentation/generated only)."] : Dedup(reasons));
        }

        var affectedSource = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var affectedTestProjects = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string production in changedProduction)
        {
            foreach (string dependent in graph.DownstreamOf(production))
            {
                if (graph.Nodes[dependent].IsTestProject) { affectedTestProjects.Add(dependent); }
                else { affectedSource.Add(dependent); }
            }
        }

        foreach (string testProject in changedTestProjects)
        {
            affectedTestProjects.Add(testProject);
        }

        if (affectedTestProjects.Count == 0)
        {
            return new AffectedPlan(JsonContractDefaults.SchemaVersion, AffectedOutcome.NoTestsRequired,
                affectedSource.Select(p => graph.Nodes[p].Name).ToArray(), [], ["No test project covers the changed projects."]);
        }

        SelectedTest[] selected = affectedTestProjects
            .Select(t => new SelectedTest(graph.Nodes[t].Name, t, $"dotnet test {t} -c {configuration} --nologo"))
            .ToArray();
        return new AffectedPlan(JsonContractDefaults.SchemaVersion, AffectedOutcome.Affected,
            affectedSource.Select(p => graph.Nodes[p].Name).ToArray(), selected, Dedup(reasons));
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

    private static IReadOnlyList<string> Dedup(IEnumerable<string> reasons) =>
        reasons.Distinct(StringComparer.Ordinal).OrderBy(r => r, StringComparer.Ordinal).ToArray();

    private static string DiscoverSolution(string repositoryRoot)
    {
        string[] solutions = Directory.GetFiles(repositoryRoot, "*.slnx");
        return solutions.Length switch
        {
            1 => Path.GetFileName(solutions[0]),
            0 => throw new FileNotFoundException($"No .slnx solution found in {repositoryRoot}."),
            _ => throw new InvalidOperationException($"Multiple .slnx solutions found in {repositoryRoot}; expected exactly one.")
        };
    }
}
