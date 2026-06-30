using Hx.Cycle.Core;
using Hx.Doti.Core.Workflow;
using Xunit;

namespace Hx.Doti.Tests;

/// <summary>
/// 028 FR-010: the workflow presentation projection — the model-backed replacement for the deleted
/// <c>DotiWorkflowRegistry</c>. Asserts the SAME presentation behavior (ordered numbered stages without a commit
/// stage; display titles + branching next-actions; the rehomed prose), now projected from the engine
/// <see cref="StageModel"/> + <see cref="StagePresentation"/> rather than a hand-maintained registry list (a faithful
/// migration, never a weakening).
/// </summary>
public sealed class DotiWorkflowRegistryTests
{
    private static readonly DotiWorkflowPresentation Workflow = WorkflowPresentationFixture.Load();

    [Fact]
    public void Registry_declares_ordered_numbered_normal_stages_without_commit()
    {
        string[] skillIds = Workflow.Stages.Select(s => s.SkillId).ToArray();

        Assert.Equal(
            [
                "01-doti-specify",
                "02-doti-clarify",
                "03-doti-plan",
                "04-doti-arch-review",
                "05-doti-tasks",
                "06-doti-analyze",
                "07-doti-implement",
                "08-doti-drift-review",
                "09-doti-release"
            ],
            skillIds);
        Assert.DoesNotContain(Workflow.Stages, s => s.CommandName == "doti-commit");
    }

    [Fact]
    public void Registry_declares_display_titles_and_branching_next_actions()
    {
        DotiWorkflowStage implement = Workflow.Stages.Single(s => s.StageId == "implement");
        DotiWorkflowStage driftReview = Workflow.Stages.Single(s => s.StageId == "drift-review");

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
    public void Skill_identity_resolves_numbered_cycle_stages_and_unnumbered_utility_skills()
    {
        // A numbered cycle stage keeps its NN- ordinal id + the chained next-step.
        (string skillId, string commandName, string nextStep) = Workflow.ResolveSkillIdentity("doti-plan");
        Assert.Equal("03-doti-plan", skillId);
        Assert.Equal("doti-plan", commandName);
        Assert.Contains("/04-doti-arch-review", nextStep);

        // A utility skill renders UNNUMBERED with its rehomed next-step (formerly skills.json nextStage, B6).
        (string utilId, string utilCommand, string utilNext) = Workflow.ResolveSkillIdentity("doti-upgrade");
        Assert.Equal("doti-upgrade", utilId);
        Assert.Equal("doti-upgrade", utilCommand);
        Assert.Contains("/01-doti-specify", utilNext);
    }

    [Fact]
    public void Workflow_assets_match_presentation_without_commit_stage()
    {
        string repo = FindRepoRoot();
        AssertWorkflow(Path.Combine(repo, ".doti", "core", "workflows", "doti", "workflow.yml"));
        AssertWorkflow(Path.Combine(repo, ".doti", "workflows", "doti", "workflow.yml"));
    }

    private static void AssertWorkflow(string path)
    {
        StageModel model = StageModel.Load(path);

        Assert.Equal(
            Workflow.Stages.Select(s => s.StageId),
            model.Stages.Select(s => s.Id));
        Assert.Equal(
            Workflow.Stages.Select(s => s.SkillId),
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
