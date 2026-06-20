namespace Hx.Impact.Core.Domain;

/// <summary>
/// A node in the project-reference graph. Identity is the repo-relative, '/'-normalized `.csproj`
/// path (unique by construction and the form `ProjectReference Include` resolves to), so the planner
/// needs no hand-maintained project-ownership metadata — kind is auto-derived from the project file.
/// </summary>
public sealed record ProjectNode(
    string Path,
    string Name,
    bool IsTestProject,
    IReadOnlyList<string> ProjectReferences);

/// <summary>
/// The deterministic project-reference graph built from `.slnx` membership + each `.csproj`'s
/// `&lt;ProjectReference&gt;`. <see cref="DownstreamOf"/> (reverse closure) drives affected-test
/// selection; <see cref="DependencyClosureOf"/> (forward closure) drives the vendored-source closure.
/// All keys compare ordinal-ignore-case (Windows paths); all results are ordinally sorted for determinism.
/// </summary>
public sealed record ProjectGraph(
    IReadOnlyDictionary<string, ProjectNode> Nodes,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Edges,
    IReadOnlyDictionary<string, IReadOnlyList<string>> ReverseEdges,
    IReadOnlyList<string> Findings)
{
    /// <summary>The project plus every project that transitively depends on it (reverse closure).</summary>
    public IReadOnlyList<string> DownstreamOf(string projectPath) =>
        Closure(projectPath, ReverseEdges);

    /// <summary>The projects plus everything they transitively depend on (forward closure).</summary>
    public IReadOnlyList<string> DependencyClosureOf(IEnumerable<string> projectPaths)
    {
        var result = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        foreach (string start in projectPaths)
        {
            if (result.Add(start))
            {
                stack.Push(start);
            }
        }

        Walk(stack, result, Edges);
        return result.ToArray();
    }

    private IReadOnlyList<string> Closure(string start, IReadOnlyDictionary<string, IReadOnlyList<string>> adjacency)
    {
        var result = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { start };
        var stack = new Stack<string>();
        stack.Push(start);
        Walk(stack, result, adjacency);
        return result.ToArray();
    }

    private static void Walk(
        Stack<string> stack,
        SortedSet<string> result,
        IReadOnlyDictionary<string, IReadOnlyList<string>> adjacency)
    {
        while (stack.Count > 0)
        {
            string current = stack.Pop();
            if (!adjacency.TryGetValue(current, out var neighbours))
            {
                continue;
            }

            foreach (string next in neighbours)
            {
                if (result.Add(next))
                {
                    stack.Push(next);
                }
            }
        }
    }
}
