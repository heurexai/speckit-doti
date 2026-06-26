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
public sealed partial class AffectedTestPlanner
{
    /// <summary>End-to-end: discover the solution, build the graph, collect the change set, resolve the plan.</summary>
    public AffectedPlan Plan(string repositoryRoot, string baseRef, string headRef, string configuration)
    {
        string solutionFileName = DiscoverSolution(repositoryRoot);
        ProjectGraph graph = new ProjectGraphBuilder().Build(repositoryRoot, solutionFileName);
        IReadOnlyList<string> changed = new ImpactChangeCollector().Collect(repositoryRoot, baseRef, headRef);
        return Resolve(graph, changed, configuration);
    }

    /// <summary>Pure resolution (no git/IO) — the testable core. Always carries the change set's non-generated
    /// changed files (007 T040, FR-043) so the same plan serves the test-scope and arch-review-context audiences.</summary>
    public static AffectedPlan Resolve(ProjectGraph graph, IReadOnlyList<string> changedPaths, string configuration) =>
        ResolveCore(graph, changedPaths, configuration) with { ChangedFiles = NonGeneratedChangedFiles(changedPaths) };

    private static AffectedPlan ResolveCore(ProjectGraph graph, IReadOnlyList<string> changedPaths, string configuration)
    {
        ChangeImpact impact = AnalyzeChangedPaths(graph, changedPaths);

        if (impact.Escalate)
        {
            return new AffectedPlan(JsonContractDefaults.SchemaVersion, AffectedOutcome.FullGateRequired, [], [], Dedup(impact.Reasons));
        }

        if (!impact.AnyImpactingPath)
        {
            return new AffectedPlan(JsonContractDefaults.SchemaVersion, AffectedOutcome.NoTestsRequired, [], [],
                impact.Reasons.Count == 0 ? ["No code or test changes (documentation/generated only)."] : Dedup(impact.Reasons));
        }

        AffectedSelection selection = SelectAffected(graph, impact);

        if (selection.TestProjects.Count == 0)
        {
            return new AffectedPlan(JsonContractDefaults.SchemaVersion, AffectedOutcome.NoTestsRequired,
                selection.SourceProjects.Select(p => graph.Nodes[p].Name).ToArray(), [], ["No test project covers the changed projects."]);
        }

        SelectedTest[] selected = selection.TestProjects
            .Select(t => new SelectedTest(graph.Nodes[t].Name, t, $"dotnet test {t} -c {configuration} --nologo"))
            .ToArray();
        return new AffectedPlan(JsonContractDefaults.SchemaVersion, AffectedOutcome.Affected,
            selection.SourceProjects.Select(p => graph.Nodes[p].Name).ToArray(), selected, Dedup(impact.Reasons));
    }

    private static IReadOnlyList<string> Dedup(IEnumerable<string> reasons) =>
        reasons.Distinct(StringComparer.Ordinal).OrderBy(r => r, StringComparer.Ordinal).ToArray();

    // Arch-review context = the human-edited footprint: every changed path EXCEPT generated build output (bin/obj).
    // Docs/templates/config are kept on purpose — the review triage needs them to decide which lenses apply
    // ("only templates changed -> skip the code lenses"). Deterministic order; git already omits gitignored bin/obj,
    // but filter defensively so a hand-built path list behaves identically.
    private static IReadOnlyList<string> NonGeneratedChangedFiles(IReadOnlyList<string> changedPaths) =>
        changedPaths
            .Where(p => !p.Replace('\\', '/').Split('/').Any(s => s is "bin" or "obj"))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

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
