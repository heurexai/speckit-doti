using Hx.Doti.Core.Workflow;
using Hx.Cycle.Core;
using Xunit;

namespace Hx.Doti.Tests;

public sealed class DotiWorkflowRegistryTests
{
    [Fact]
    public void Registry_declares_ordered_numbered_normal_stages_without_commit()
    {
        string[] skillIds = DotiWorkflowRegistry.Stages.Select(s => s.SkillId).ToArray();

        Assert.Equal(
            [
                "01-doti-specify",
                "02-doti-clarify",
                "03-doti-plan",
                "04-doti-tasks",
                "05-doti-analyze",
                "06-doti-arch-review",
                "07-doti-implement",
                "08-doti-drift-review",
                "09-doti-release"
            ],
            skillIds);
        Assert.DoesNotContain(DotiWorkflowRegistry.Stages, s => s.CommandName == "doti-commit");
    }

    [Fact]
    public void Registry_declares_display_titles_and_branching_next_actions()
    {
        DotiWorkflowStage implement = DotiWorkflowRegistry.FindByStageId("implement");
        DotiWorkflowStage driftReview = DotiWorkflowRegistry.FindByStageId("drift-review");

        Assert.Equal("07-Implement", implement.DisplayTitle);
        Assert.Equal(["drift-review"], implement.NextStageIds);
        Assert.Contains("/08-doti-drift-review", implement.NextStep);

        Assert.Equal("08-Drift-Review", driftReview.DisplayTitle);
        Assert.Equal(["release", "specify"], driftReview.NextStageIds);
        Assert.Contains("/09-doti-release", driftReview.NextStep);
        Assert.Contains("/01-doti-specify", driftReview.NextStep);
        Assert.Contains(driftReview.AlternateActions, a => a.Optional && a.CommandName == "doti-specify");
    }

    [Fact]
    public void Workflow_assets_match_registry_without_commit_stage()
    {
        string repo = FindRepoRoot();
        AssertWorkflow(Path.Combine(repo, ".doti", "core", "workflows", "doti", "workflow.yml"));
        AssertWorkflow(Path.Combine(repo, ".doti", "workflows", "doti", "workflow.yml"));
    }

    private static void AssertWorkflow(string path)
    {
        StageModel model = StageModel.Load(path);

        Assert.Equal(
            DotiWorkflowRegistry.Stages.Select(s => s.StageId),
            model.Stages.Select(s => s.Id));
        Assert.Equal(
            DotiWorkflowRegistry.Stages.Select(s => s.SkillId),
            model.Stages.Select(s => s.Command));
        Assert.DoesNotContain(model.Stages, s => s.Id == "commit" || s.Command == "doti-commit");
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "scaffold-dotnet.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find repository root from test output directory.");
    }
}
