using Hx.Cycle.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Cycle.Tests;

/// <summary>T016 (FR-025): the <see cref="ReviewContextProjector"/> turns a change set into the arch-review lens
/// applicability — docs-only skips the code lenses (SC-011), a CLI handler + tests run them (SC-012), a new
/// dependency edge runs blast-radius (SC-013) — deterministically, so triage is data, not prose.</summary>
public sealed class ReviewContextTests
{
    private static readonly LayerMap Layers = LayerMap.FromLayers(
    [
        ("contracts", ["tools/Hx.Tooling.Contracts"]),
        ("core", ["tools/Hx.Cycle.Core", "tools/Hx.Impact.Core"]),
        ("cli", ["tools/Hx.Runner.Cli"]),
    ]);

    private static ChangeSetContext ChangeSet(params string[] paths) =>
        new(1, "base", "HEAD", "sha", true, true, null,
            paths.Select(p => new ChangedFile(p, ChangeStatus.Modified, null)).ToList(),
            []);

    private static ReviewContext Project(params string[] paths) =>
        new ReviewContextProjector(Layers).Project(ChangeSet(paths));

    [Fact]
    public void DocsOnly_change_skips_the_code_lenses() // SC-011
    {
        ReviewContext ctx = Project("README.md", "docs/notes.md");

        Assert.True(ctx.IsDocsOnly);
        Assert.Equal([ReviewLens.DesignSoundness], ctx.ApplicableLenses);
        Assert.Contains(ReviewLens.BlastRadiusDependencies, ctx.SkippedLenses);
        Assert.Contains(ReviewLens.SecurityTrustBoundaries, ctx.SkippedLenses);
    }

    [Fact]
    public void DotiProse_change_is_docs_only()
    {
        ReviewContext ctx = Project(".doti/core/templates/commands/doti-analyze.md", ".doti/core/skills.json");

        Assert.True(ctx.IsDocsOnly);
        Assert.Contains(ReviewCategory.DotiProse, ctx.Categories);
        Assert.Equal([ReviewLens.DesignSoundness], ctx.ApplicableLenses);
    }

    [Fact]
    public void CliHandler_and_tests_apply_the_code_lenses() // SC-012
    {
        ReviewContext ctx = Project(
            "tools/Hx.Runner.Cli/RunnerCommands.DotiCycle.cs", "test/Hx.Cycle.Tests/RefreshTests.cs");

        Assert.False(ctx.IsDocsOnly);
        Assert.Contains(ReviewCategory.RuntimeCode, ctx.Categories);
        Assert.Contains(ReviewLens.FitWithCurrentArchitecture, ctx.ApplicableLenses);
        Assert.Contains(ReviewLens.TestabilityProof, ctx.ApplicableLenses);
        Assert.Contains(ReviewLens.EdgeCasesFailureModes, ctx.ApplicableLenses);
    }

    [Fact]
    public void New_dependency_edge_applies_blast_radius() // SC-013
    {
        ReviewContext ctx = Project("tools/Hx.Semantic.Core/Hx.Semantic.Core.csproj");

        Assert.Contains(ReviewCategory.DependencyEdge, ctx.Categories);
        Assert.Contains(ReviewLens.BlastRadiusDependencies, ctx.ApplicableLenses);
        Assert.Contains(ReviewLens.FitWithCurrentArchitecture, ctx.ApplicableLenses);
    }

    [Fact]
    public void Contract_change_applies_the_data_contract_lens()
    {
        ReviewContext ctx = Project("tools/Hx.Tooling.Contracts/CliResult.cs");

        Assert.Contains(ReviewCategory.Contract, ctx.Categories);
        Assert.Contains(ReviewLens.DataContractIntegrity, ctx.ApplicableLenses);
    }

    [Fact]
    public void GeneratedCodeTemplate_is_treated_as_code()
    {
        ReviewContext ctx = Project("scaffold/templates/dotnet-cli/src/App/Program.cs");

        Assert.Contains(ReviewCategory.GeneratedTemplate, ctx.Categories);
        Assert.False(ctx.IsDocsOnly);
        Assert.Contains(ReviewLens.ModularityDesignSmells, ctx.ApplicableLenses);
    }

    [Fact]
    public void Unresolved_layer_map_fails_closed_to_all_lenses() // L-2
    {
        ReviewContext ctx = new ReviewContextProjector(LayerMap.Unresolved()).Project(ChangeSet("README.md"));

        Assert.Equal(ReviewLens.All.OrderBy(x => x, StringComparer.Ordinal), ctx.ApplicableLenses);
        Assert.Empty(ctx.SkippedLenses);
        Assert.Contains(ctx.EscalationReasons, r => r.Contains("fail-closed", StringComparison.Ordinal));
    }

    [Fact]
    public void Broad_input_escalates_to_all_lenses()
    {
        ReviewContext ctx = Project("Directory.Packages.props");

        Assert.Contains(ReviewCategory.Broad, ctx.Categories);
        Assert.Empty(ctx.SkippedLenses);
    }

    // T018 (FR-027): ArchitectureRelevantSurface.IsTouched — does the change warrant re-running arch-review?
    [Fact]
    public void ArchitectureRelevantSurface_is_not_touched_by_docs_only_change()
    {
        Assert.False(new ArchitectureRelevantSurface(Layers).IsTouched(ChangeSet("README.md", "docs/x.md")));
    }

    [Fact]
    public void ArchitectureRelevantSurface_is_touched_by_a_code_change()
    {
        Assert.True(new ArchitectureRelevantSurface(Layers).IsTouched(ChangeSet("tools/Hx.Cycle.Core/X.cs")));
    }

    [Fact]
    public void ArchitectureRelevantSurface_fails_closed_when_the_layer_map_is_unresolved()
    {
        Assert.True(new ArchitectureRelevantSurface(LayerMap.Unresolved()).IsTouched(ChangeSet("README.md")));
    }
}
