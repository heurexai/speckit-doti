using Hx.Cycle.Core;
using Xunit;
using static Hx.Cycle.Tests.CycleTestFixtures;

namespace Hx.Cycle.Tests;

/// <summary>T005: <see cref="FeatureArtifactScope.OwnedPaths"/> resolves every stage's <c>produces</c> pattern for
/// a feature slug, deterministically, with exact-path (not prefix) membership.</summary>
public sealed class FeatureArtifactScopeTests
{
    [Fact]
    public void OwnedPaths_resolves_every_stage_produces_for_the_feature_in_deterministic_order()
    {
        string dir = NewTempDir();
        try
        {
            StageModel model = TwoStageModel(dir);

            IReadOnlyList<string> owned = FeatureArtifactScope.OwnedPaths(model, "008-foo");

            Assert.Equal(new[] { "docs/plans/008-foo-plan.md", "docs/specs/008-foo.md" }, owned);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void OwnedPaths_is_exact_path_membership_not_prefix()
    {
        string dir = NewTempDir();
        try
        {
            StageModel model = TwoStageModel(dir);

            IReadOnlyList<string> owned = FeatureArtifactScope.OwnedPaths(model, "008-foo");

            Assert.Contains("docs/specs/008-foo.md", owned);
            Assert.DoesNotContain("docs/specs/008-foo-stray.md", owned); // a stray sibling is NOT owned
        }
        finally { Directory.Delete(dir, true); }
    }
}
