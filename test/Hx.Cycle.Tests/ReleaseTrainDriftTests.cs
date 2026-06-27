using Hx.Cycle.Core;
using Hx.Tooling.Contracts;
using Xunit;
using static Hx.Cycle.Tests.CycleTestFixtures;

namespace Hx.Cycle.Tests;

/// <summary>T028 (FR-036) gate-proof status classification + T030 (FR-037/SC-019) cross-feature release-train drift.</summary>
public sealed class ReleaseTrainDriftTests
{
    // ---- T028: per-feature gate-proof status (the pure classifier) ----

    [Theory]
    [InlineData(false, "digest", true, true, 0, "not-required")]  // no implement stage → no proof needed
    [InlineData(true, null, true, true, 0, "missing")]            // implement stage but no recorded digest
    [InlineData(true, "digest", false, false, 0, "present")]      // earlier feature: attested by its digest
    [InlineData(true, "digest", true, false, 0, "present")]       // active but no live proof to re-validate
    [InlineData(true, "digest", true, true, 0, "present-valid")]  // active + live proof + clean
    [InlineData(true, "digest", true, true, 2, "present-stale")]  // active + live proof + validation issues
    public void ClassifyGateProofStatus_maps_each_case(
        bool hasImplement, string? digest, bool active, bool proofPresent, int issues, string expected) =>
        Assert.Equal(expected, CycleService.ClassifyGateProofStatus(hasImplement, digest, active, proofPresent, issues));

    // ---- T030: ReleaseTrainDriftDetector (pure, injected historical-diff collector) ----

    private static CycleReleaseTrainFeature Feature(string name, string commit) =>
        new(name, "drift-review", commit, null, "pass", "present", "included", []);

    [Fact]
    public void Detects_when_a_later_feature_changed_an_earlier_features_owned_path()
    {
        string dir = NewTempDir();
        try
        {
            StageModel model = TwoStageModel(dir); // 001-a owns docs/specs/001-a.md + docs/plans/001-a-plan.md
            var detector = new ReleaseTrainDriftDetector(
                (_, _, _) => ["docs/specs/001-a.md", "tools/Unrelated.cs"]);

            IReadOnlyList<ReleaseTrainDriftFinding> findings =
                detector.Detect(dir, model, [Feature("001-a", "sha-a"), Feature("002-b", "sha-b")]);

            ReleaseTrainDriftFinding finding = Assert.Single(findings);
            Assert.Equal("001-a", finding.EarlierFeature);
            Assert.Equal("002-b", finding.LaterFeature);
            Assert.Equal(["docs/specs/001-a.md"], finding.ConflictingPaths); // tools/Unrelated.cs is not owned by 001-a
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void No_drift_when_the_later_feature_touches_nothing_the_earlier_owns()
    {
        string dir = NewTempDir();
        try
        {
            var detector = new ReleaseTrainDriftDetector((_, _, _) => ["tools/Whatever.cs", "src/App/Program.cs"]);

            Assert.Empty(detector.Detect(dir, TwoStageModel(dir), [Feature("001-a", "sha-a"), Feature("002-b", "sha-b")]));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void A_single_feature_train_has_no_pairs_and_never_collects()
    {
        string dir = NewTempDir();
        try
        {
            var detector = new ReleaseTrainDriftDetector(
                (_, _, _) => throw new InvalidOperationException("a single-feature train must not run the pairwise diff"));

            Assert.Empty(detector.Detect(dir, TwoStageModel(dir), [Feature("001-a", "sha-a")]));
        }
        finally { Directory.Delete(dir, true); }
    }
}
