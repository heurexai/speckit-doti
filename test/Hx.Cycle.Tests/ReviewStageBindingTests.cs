using Hx.Cycle.Core;
using Xunit;

namespace Hx.Cycle.Tests;

/// <summary>T014 (FR-003/FR-004): the review stages bind their durable record via <c>produces</c>, so a downstream
/// consumer (drift-review's spec↔code axis) depends on content-bound artifacts and <c>analyze</c> is safely
/// re-interpretable (SC-021). Asserts the shipped workflow declares the binding — locking it against regression.</summary>
public sealed class ReviewStageBindingTests
{
    [Theory]
    [InlineData("analyze", "docs/reviews/{feature}-analyze-report.md")]
    [InlineData("arch-review", "docs/reviews/{feature}-arch-review.md")]
    [InlineData("drift-review", "docs/reviews/{feature}-drift-review.md")]
    public void Review_stage_binds_its_record_via_produces(string stageId, string expectedProduces)
    {
        StageModel model = StageModel.Load(
            Path.Combine(FindRepoRoot(), ".doti", "workflows", "doti", "workflow.yml"));

        CycleStage stage = model.Find(stageId);

        Assert.Equal("review", stage.Kind);
        Assert.Equal(expectedProduces, stage.Produces);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "scaffold-dotnet.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root (scaffold-dotnet.slnx) not found.");
    }
}
