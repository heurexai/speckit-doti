using Hx.Impact.Core.Domain;
using Hx.Impact.Core.Planning;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Impact.Tests;

public sealed class AffectedTestPlannerTests
{
    // src/A <- src/B <- src/C ; A.Tests covers A ; C.Tests covers C.
    private static ProjectGraph SampleGraph(params string[] findings)
    {
        (string Path, bool IsTest, string[] Refs)[] defs =
        [
            ("src/A/A.csproj", false, []),
            ("src/B/B.csproj", false, ["src/A/A.csproj"]),
            ("src/C/C.csproj", false, ["src/B/B.csproj"]),
            ("test/A.Tests/A.Tests.csproj", true, ["src/A/A.csproj"]),
            ("test/C.Tests/C.Tests.csproj", true, ["src/C/C.csproj"]),
        ];

        var nodes = defs.ToDictionary(
            d => d.Path,
            d => new ProjectNode(d.Path, Path.GetFileNameWithoutExtension(d.Path), d.IsTest, d.Refs),
            StringComparer.OrdinalIgnoreCase);
        var edges = defs.ToDictionary(d => d.Path, d => (IReadOnlyList<string>)d.Refs, StringComparer.OrdinalIgnoreCase);

        var reverse = defs.ToDictionary(d => d.Path, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach ((string path, _, string[] refs) in defs)
        {
            foreach (string reference in refs)
            {
                reverse[reference].Add(path);
            }
        }

        var reverseReadonly = reverse.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<string>)kvp.Value.OrderBy(v => v, StringComparer.Ordinal).ToArray(),
            StringComparer.OrdinalIgnoreCase);

        return new ProjectGraph(nodes, edges, reverseReadonly, findings);
    }

    private static string[] SelectedNames(AffectedPlan plan) =>
        plan.SelectedTests.Select(t => t.TestProject).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    [Fact]
    public void A_leaf_change_narrows_to_its_dependent_tests_only()
    {
        AffectedPlan plan = AffectedTestPlanner.Resolve(SampleGraph(), ["src/C/Widget.cs"], "Release");

        Assert.Equal(AffectedOutcome.Affected, plan.Outcome);
        Assert.Equal(["C.Tests"], SelectedNames(plan));
        Assert.Contains("dotnet test test/C.Tests/C.Tests.csproj -c Release --nologo", plan.SelectedTests.Select(t => t.Command));
    }

    [Fact]
    public void A_shared_change_reaches_all_dependent_tests()
    {
        AffectedPlan plan = AffectedTestPlanner.Resolve(SampleGraph(), ["src/A/Widget.cs"], "Release");

        Assert.Equal(AffectedOutcome.Affected, plan.Outcome);
        Assert.Equal(["A.Tests", "C.Tests"], SelectedNames(plan));
    }

    [Fact]
    public void A_changed_test_file_selects_its_own_test_project()
    {
        AffectedPlan plan = AffectedTestPlanner.Resolve(SampleGraph(), ["test/C.Tests/NewCase.cs"], "Release");

        Assert.Equal(AffectedOutcome.Affected, plan.Outcome);
        Assert.Equal(["C.Tests"], SelectedNames(plan));
    }

    [Theory]
    [InlineData("Directory.Packages.props")]
    [InlineData("global.json")]
    [InlineData("scaffold-dotnet.slnx")]
    [InlineData("rules/impact.json")]
    [InlineData("weird/unowned/file.cs")]
    public void Broad_or_unattributed_changes_escalate_to_full_gate(string changedPath)
    {
        AffectedPlan plan = AffectedTestPlanner.Resolve(SampleGraph(), [changedPath], "Release");

        Assert.Equal(AffectedOutcome.FullGateRequired, plan.Outcome);
        Assert.Empty(plan.SelectedTests);
        Assert.NotEmpty(plan.Reasons);
    }

    [Fact]
    public void Documentation_and_generated_only_changes_require_no_tests()
    {
        AffectedPlan plan = AffectedTestPlanner.Resolve(SampleGraph(), ["README.md", "src/C/obj/C.dll", "docs/notes.md"], "Release");

        Assert.Equal(AffectedOutcome.NoTestsRequired, plan.Outcome);
        Assert.Empty(plan.SelectedTests);
    }

    [Fact]
    public void A_graph_finding_escalates_to_full_gate()
    {
        AffectedPlan plan = AffectedTestPlanner.Resolve(SampleGraph("cycle-detected:src/A/A.csproj"), ["src/C/Widget.cs"], "Release");

        Assert.Equal(AffectedOutcome.FullGateRequired, plan.Outcome);
        Assert.Contains(plan.Reasons, r => r.Contains("graph-finding:cycle-detected", StringComparison.Ordinal));
    }

    // 007 T040 (FR-043): the plan always carries the arch-review changed-files context.
    [Fact]
    public void ChangedFiles_carries_the_change_set_for_an_affected_plan()
    {
        AffectedPlan plan = AffectedTestPlanner.Resolve(SampleGraph(), ["src/C/Widget.cs"], "Release");

        Assert.Equal(AffectedOutcome.Affected, plan.Outcome);
        Assert.Equal(["src/C/Widget.cs"], plan.ChangedFiles);
    }

    [Fact]
    public void ChangedFiles_excludes_generated_output_but_keeps_docs_for_triage()
    {
        // Arch-review triage needs to see docs/template edits ("only docs changed -> skip code lenses");
        // it must NOT see bin/obj build output. Deterministic Ordinal order.
        AffectedPlan plan = AffectedTestPlanner.Resolve(SampleGraph(), ["src/C/obj/C.dll", "docs/notes.md", "README.md"], "Release");

        Assert.Equal(["README.md", "docs/notes.md"], plan.ChangedFiles);
    }

    [Fact]
    public void ChangedFiles_is_present_even_when_the_plan_escalates_to_full_gate()
    {
        // A broad change escalates the TEST scope to full-gate, but the arch-review context must still arrive —
        // the reviewer triages from the changed files regardless of the test-selection outcome.
        AffectedPlan plan = AffectedTestPlanner.Resolve(SampleGraph(), ["Directory.Packages.props"], "Release");

        Assert.Equal(AffectedOutcome.FullGateRequired, plan.Outcome);
        Assert.Equal(["Directory.Packages.props"], plan.ChangedFiles);
    }
}
