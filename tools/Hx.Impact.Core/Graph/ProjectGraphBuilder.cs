using System.Xml.Linq;
using Hx.Impact.Core.Domain;

namespace Hx.Impact.Core.Graph;

/// <summary>
/// Builds the project-reference graph from a repo's `.slnx` membership + each member `.csproj`'s
/// `&lt;ProjectReference&gt;`. Metadata-free and deterministic: identity is the repo-relative `.csproj`
/// path, kind is auto-derived (`&lt;IsTestProject&gt;` / `Microsoft.NET.Test.Sdk`). Unresolved references
/// (to a non-member project) and dependency cycles are recorded as findings (the planner escalates to a
/// full gate on any finding — it never silently mis-selects). Adapted from the proven polaris planner.
/// </summary>
public sealed class ProjectGraphBuilder
{
    public ProjectGraph Build(string repositoryRoot, string solutionFileName)
    {
        IReadOnlyList<string> memberPaths = ReadSolutionProjectPaths(repositoryRoot, solutionFileName);
        var members = new HashSet<string>(memberPaths, StringComparer.OrdinalIgnoreCase);

        var nodes = new SortedDictionary<string, ProjectNode>(StringComparer.OrdinalIgnoreCase);
        var edges = new SortedDictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var findings = new List<string>();

        foreach (string path in memberPaths)
        {
            string fullPath = Path.Combine(repositoryRoot, path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                findings.Add($"missing-project-file:{path}");
                nodes[path] = new ProjectNode(path, NameOf(path), false, []);
                edges[path] = [];
                continue;
            }

            (bool isTest, IReadOnlyList<string> rawRefs) = ReadProjectFile(repositoryRoot, fullPath);
            var resolved = new List<string>();
            foreach (string reference in rawRefs)
            {
                if (members.Contains(reference))
                {
                    resolved.Add(reference);
                }
                else
                {
                    findings.Add($"unresolved-reference:{path}->{reference}");
                }
            }

            resolved.Sort(StringComparer.OrdinalIgnoreCase);
            nodes[path] = new ProjectNode(path, NameOf(path), isTest, resolved);
            edges[path] = resolved;
        }

        findings.AddRange(DetectCycles(edges));
        IReadOnlyDictionary<string, IReadOnlyList<string>> reverse = BuildReverseEdges(nodes.Keys, edges);
        return new ProjectGraph(
            nodes,
            edges,
            reverse,
            findings.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(f => f, StringComparer.Ordinal).ToArray());
    }

    private static string NameOf(string projectPath) =>
        Path.GetFileNameWithoutExtension(projectPath);

    private static IReadOnlyList<string> ReadSolutionProjectPaths(string repositoryRoot, string solutionFileName)
    {
        string solutionPath = Path.Combine(repositoryRoot, solutionFileName);
        if (!File.Exists(solutionPath))
        {
            throw new FileNotFoundException($"Solution file not found: {solutionFileName}", solutionPath);
        }

        XDocument document = XDocument.Load(solutionPath);
        return document.Descendants()
            .Where(e => e.Name.LocalName == "Project")
            .Select(e => e.Attribute("Path")?.Value)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => Normalize(p!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static (bool IsTest, IReadOnlyList<string> References) ReadProjectFile(string repositoryRoot, string projectFullPath)
    {
        string projectDirectory = Path.GetDirectoryName(projectFullPath) ?? repositoryRoot;
        XDocument document = XDocument.Load(projectFullPath);

        bool isTest = document.Descendants()
            .Any(e => e.Name.LocalName == "IsTestProject"
                && string.Equals(e.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase))
            || document.Descendants()
                .Where(e => e.Name.LocalName == "PackageReference")
                .Any(e => string.Equals(e.Attribute("Include")?.Value, "Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase));

        IReadOnlyList<string> references = document.Descendants()
            .Where(e => e.Name.LocalName == "ProjectReference")
            .Select(e => e.Attribute("Include")?.Value)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => Path.GetFullPath(Path.Combine(projectDirectory, p!.Replace('\\', Path.DirectorySeparatorChar))))
            .Select(p => Normalize(Path.GetRelativePath(repositoryRoot, p)))
            .ToArray();

        return (isTest, references);
    }

    private static string Normalize(string path) =>
        path.Replace('\\', '/').TrimStart('.', '/').Trim();

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildReverseEdges(
        IEnumerable<string> nodeIds,
        IReadOnlyDictionary<string, IReadOnlyList<string>> edges)
    {
        var reverse = nodeIds.ToDictionary(id => id, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach ((string from, IReadOnlyList<string> targets) in edges)
        {
            foreach (string to in targets)
            {
                if (!reverse.TryGetValue(to, out var dependents))
                {
                    dependents = [];
                    reverse[to] = dependents;
                }

                dependents.Add(from);
            }
        }

        return reverse.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<string>)kvp.Value.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> DetectCycles(IReadOnlyDictionary<string, IReadOnlyList<string>> edges)
    {
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var findings = new List<string>();
        foreach (string node in edges.Keys)
        {
            Visit(node, edges, visiting, visited, findings);
        }

        return findings;
    }

    private static void Visit(
        string node,
        IReadOnlyDictionary<string, IReadOnlyList<string>> edges,
        ISet<string> visiting,
        ISet<string> visited,
        ICollection<string> findings)
    {
        if (visited.Contains(node)) { return; }
        if (!visiting.Add(node))
        {
            findings.Add($"cycle-detected:{node}");
            return;
        }

        if (edges.TryGetValue(node, out var references))
        {
            foreach (string reference in references)
            {
                Visit(reference, edges, visiting, visited, findings);
            }
        }

        visiting.Remove(node);
        visited.Add(node);
    }
}
