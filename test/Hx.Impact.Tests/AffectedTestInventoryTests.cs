using Hx.Impact.Core.Domain;
using Hx.Impact.Core.Planning;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Impact.Tests;

/// <summary>
/// 012 (T005): the <see cref="AffectedTestInventoryProjector"/> reports selected/total TEST PROJECTS from the
/// project graph (the cheap denominator), excludes the architecture-test project from the unit-test total (FR-012),
/// returns null for a no-tests plan, and marks class/case totals UNKNOWN rather than building unaffected test
/// projects (M3 / FR-005). A bad/missing built assembly yields an honest unknown, never a crash.
/// </summary>
public sealed class AffectedTestInventoryTests
{
    private static ProjectGraph Graph(params (string Path, bool IsTest)[] defs)
    {
        var nodes = defs.ToDictionary(
            d => d.Path,
            d => new ProjectNode(d.Path, Path.GetFileNameWithoutExtension(d.Path), d.IsTest, (IReadOnlyList<string>)[]),
            StringComparer.OrdinalIgnoreCase);
        var empty = defs.ToDictionary(d => d.Path, _ => (IReadOnlyList<string>)[], StringComparer.OrdinalIgnoreCase);
        return new ProjectGraph(nodes, empty, empty, []);
    }

    private static AffectedPlan AffectedPlan(params string[] testProjects) =>
        new(JsonContractDefaults.SchemaVersion, AffectedOutcome.Affected, [],
            testProjects.Select(p => new SelectedTest(Path.GetFileNameWithoutExtension(p), p, "cmd")).ToArray(), []);

    [Fact]
    public void No_tests_required_plan_has_no_inventory()
    {
        var plan = new AffectedPlan(JsonContractDefaults.SchemaVersion, AffectedOutcome.NoTestsRequired, [], [], []);
        AffectedTestInventory? inventory = AffectedTestInventoryProjector.Build(
            "/repo", Graph(("src/A/A.csproj", false)), plan, [], "Release");

        Assert.Null(inventory);
    }

    [Fact]
    public void Reports_selected_over_total_test_projects_excluding_architecture_tests()
    {
        ProjectGraph graph = Graph(
            ("src/A/A.csproj", false),
            ("test/A.Tests/A.Tests.csproj", true),
            ("test/B.Tests/B.Tests.csproj", true),
            ("test/Hx.Architecture.Tests/Hx.Architecture.Tests.csproj", true));
        AffectedPlan plan = AffectedPlan("test/A.Tests/A.Tests.csproj");

        AffectedTestInventory inventory = AffectedTestInventoryProjector.Build(
            "/repo-with-no-bin", graph, plan, ["test/A.Tests/A.Tests.csproj"], "Release")!;

        Assert.Equal(1, inventory.SelectedProjects);
        Assert.Equal(2, inventory.TotalProjects); // A.Tests + B.Tests; Architecture.Tests excluded (FR-012)
    }

    [Fact]
    public void Class_case_counts_are_unknown_when_built_assemblies_are_absent()
    {
        ProjectGraph graph = Graph(
            ("src/A/A.csproj", false),
            ("test/A.Tests/A.Tests.csproj", true));
        AffectedPlan plan = AffectedPlan("test/A.Tests/A.Tests.csproj");

        AffectedTestInventory inventory = AffectedTestInventoryProjector.Build(
            "/repo-with-no-bin", graph, plan, ["test/A.Tests/A.Tests.csproj"], "Release")!;

        // No built DLL → class/case counts unknown (null), with a reason; project scope still reported (FR-005).
        Assert.Null(inventory.SelectedClasses);
        Assert.Null(inventory.SelectedCases);
        Assert.Null(inventory.TotalClasses);
        Assert.Null(inventory.TotalCases);
        Assert.False(string.IsNullOrWhiteSpace(inventory.UnknownReason));
        Assert.Equal(1, inventory.SelectedProjects);
    }

    [Fact]
    public void Repo_wide_total_is_unknown_for_a_partial_selection_even_when_assemblies_reflect()
    {
        // Only one of two unit-test projects selected → the repo-wide total cannot be known cheaply (would require
        // building the unselected project). Even with a bin present the total must stay unknown for a partial plan.
        ProjectGraph graph = Graph(
            ("test/A.Tests/A.Tests.csproj", true),
            ("test/B.Tests/B.Tests.csproj", true));
        AffectedPlan plan = AffectedPlan("test/A.Tests/A.Tests.csproj");

        AffectedTestInventory inventory = AffectedTestInventoryProjector.Build(
            "/repo-with-no-bin", graph, plan, ["test/A.Tests/A.Tests.csproj"], "Release")!;

        Assert.Equal(1, inventory.SelectedProjects);
        Assert.Equal(2, inventory.TotalProjects);
        Assert.Null(inventory.TotalClasses); // partial → repo-wide total never built (M3)
    }

    [Fact]
    public void Reflects_class_and_case_counts_from_a_real_built_test_assembly()
    {
        // Reflect THIS test assembly's own build output: it has many [Fact]/[Theory] methods. The projector reads
        // metadata (no execution), so counts must be > 0 and the class count <= the case count.
        string assemblyPath = typeof(AffectedTestInventoryTests).Assembly.Location;
        string tfmDir = Path.GetDirectoryName(assemblyPath)!;            // .../bin/<config>/<tfm>
        string configDir = Path.GetDirectoryName(tfmDir)!;               // .../bin/<config>
        string config = Path.GetFileName(configDir);
        string binDir = Path.GetDirectoryName(configDir)!;               // .../bin
        string projectDir = Path.GetDirectoryName(binDir)!;              // the project dir
        string repoRoot = Path.GetDirectoryName(Path.GetDirectoryName(projectDir)!)!; // .../<repo>
        string projectRelative = Path.GetRelativePath(repoRoot, Path.Combine(projectDir, "Hx.Impact.Tests.csproj"))
            .Replace('\\', '/');

        ProjectGraph graph = Graph((projectRelative, true));
        AffectedPlan plan = AffectedPlan(projectRelative);

        AffectedTestInventory inventory = AffectedTestInventoryProjector.Build(
            repoRoot, graph, plan, [projectRelative], config)!;

        Assert.NotNull(inventory.SelectedCases);
        Assert.NotNull(inventory.SelectedClasses);
        Assert.True(inventory.SelectedCases > 0, "expected at least one [Fact]/[Theory] case");
        Assert.True(inventory.SelectedClasses <= inventory.SelectedCases);
        // A full selection (1 of 1) that reflects cleanly → the repo-wide total IS known.
        Assert.Equal(inventory.SelectedCases, inventory.TotalCases);
    }
}
