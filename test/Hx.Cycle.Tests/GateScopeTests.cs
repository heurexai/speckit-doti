using Hx.Cycle.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Cycle.Tests;

/// <summary>T020 (FR-028): the docs-only gate scope is the AND of "affected plan = no-tests-required" and "review
/// context = prose/docs-only", so a generated-code-template change (no test impact, but CODE) is NOT scope-skipped —
/// architecture + Sentrux still run (M-1). render/payload/skill-drift are never in the skippable set.</summary>
public sealed class GateScopeTests
{
    private static readonly LayerMap Layers = LayerMap.FromLayers([("contracts", ["tools/Hx.Tooling.Contracts"])]);

    private static AffectedPlan Plan(string outcome) => new(1, outcome, [], [], []);

    private static ReviewContext Review(params string[] paths) =>
        new ReviewContextProjector(Layers).Project(
            new ChangeSetContext(1, "b", "HEAD", "s", true, true, null,
                paths.Select(p => new ChangedFile(p, ChangeStatus.Modified, null)).ToList(), []));

    [Fact]
    public void DocsOnly_when_both_signals_agree()
    {
        Assert.True(GateScopeResolver.IsDocsOnly(Plan(AffectedOutcome.NoTestsRequired), Review("README.md", "docs/x.md")));
    }

    [Fact]
    public void Not_docs_only_when_a_generated_code_template_changed_even_with_no_tests_required() // M-1
    {
        // scaffold/templates/** has no test impact (planner = no-tests-required) but IS code — must NOT scope-skip.
        Assert.False(GateScopeResolver.IsDocsOnly(
            Plan(AffectedOutcome.NoTestsRequired), Review("scaffold/templates/dotnet-cli/src/App/Program.cs")));
    }

    [Fact]
    public void Not_docs_only_when_tests_are_required()
    {
        Assert.False(GateScopeResolver.IsDocsOnly(Plan(AffectedOutcome.Affected), Review("README.md")));
    }

    [Fact]
    public void Not_docs_only_when_runtime_code_changed()
    {
        Assert.False(GateScopeResolver.IsDocsOnly(Plan(AffectedOutcome.NoTestsRequired), Review("tools/Hx.Cycle.Core/X.cs")));
    }

    [Fact]
    public void Skippable_steps_are_architecture_and_sentrux_only()
    {
        // render / payload / skill-drift must never be in the scope-skippable set (SC-011).
        Assert.Contains("architecture-test", GateScopeResolver.ScopeSkippableSteps);
        Assert.Contains("sentrux-check", GateScopeResolver.ScopeSkippableSteps);
        Assert.DoesNotContain("doti-payload", GateScopeResolver.ScopeSkippableSteps);
        Assert.DoesNotContain("skill-drift", GateScopeResolver.ScopeSkippableSteps);
    }
}
