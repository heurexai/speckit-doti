using Hx.Doti.Core.Workflow;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>
/// 028 FR-010: the workflow presentation/describe surfaces — the model-backed replacement for the deleted
/// <c>DotiWorkflowRegistry</c>. Asserts the SAME order + branching + no-commit invariants, now projected from the
/// engine stage model via <see cref="DotiWorkflowPresentation"/>/<see cref="DotiWorkflowDescribe"/> (a faithful
/// migration, never a weakening).
/// </summary>
public sealed class DotiWorkflowRegistryTests
{
    private static DotiWorkflowPresentation Workflow() => DotiWorkflowPresentation.Load(FindRepoRoot());

    [Fact]
    public void Runner_discovers_the_numeric_workflow_order()
    {
        Assert.Collection(Workflow().Stages,
            s => AssertStage(s, 1, "specify", "01-doti-specify"),
            s => AssertStage(s, 2, "clarify", "02-doti-clarify"),
            s => AssertStage(s, 3, "plan", "03-doti-plan"),
            s => AssertStage(s, 4, "arch-review", "04-doti-arch-review"),
            s => AssertStage(s, 5, "tasks", "05-doti-tasks"),
            s => AssertStage(s, 6, "analyze", "06-doti-analyze"),
            s => AssertStage(s, 7, "implement", "07-doti-implement"),
            s => AssertStage(s, 8, "drift-review", "08-doti-drift-review"),
            s => AssertStage(s, 9, "release", "09-doti-release"));
    }

    [Fact]
    public void Commit_is_not_a_normal_or_compatibility_workflow_stage()
    {
        Assert.DoesNotContain(Workflow().Stages, s => s.CommandName == "doti-commit");
    }

    [Fact]
    public void Describe_metadata_exposes_workflow_order_without_commit()
    {
        string repo = FindRepoRoot();
        CliDescribeWorkflow workflow = DotiWorkflowDescribe.Build(repo);

        Assert.NotNull(workflow);
        Assert.Equal("doti", workflow.Name);
        Assert.Equal(
            DotiWorkflowPresentation.Load(repo).Stages.Select(s => s.SkillId),
            workflow.Stages.Select(s => s.SkillId));
        Assert.DoesNotContain(workflow.Stages, s => s.StageId == "commit");
        Assert.DoesNotContain(workflow.Stages, s => s.CommandName == "doti-commit");

        CliDescribeWorkflowStage driftReview = Assert.Single(workflow.Stages, s => s.StageId == "drift-review");
        Assert.Equal(["release", "specify"], driftReview.NextStageIds);
        Assert.Contains(driftReview.AlternateActions, a => a.Id == "continue-release-train" && a.Optional);
    }

    [Fact]
    public void Drift_review_can_branch_to_release_or_new_specification()
    {
        DotiWorkflowStage driftReview = Workflow().Stages.Single(s => s.StageId == "drift-review");

        Assert.Equal(["release", "specify"], driftReview.NextStageIds);
        Assert.Contains(driftReview.AlternateActions, a => a.Id == "continue-release-train" && a.Optional);
    }

    private static void AssertStage(DotiWorkflowStage stage, int ordinal, string stageId, string skillId)
    {
        Assert.Equal(ordinal, stage.Ordinal);
        Assert.Equal(stageId, stage.StageId);
        Assert.Equal(skillId, stage.SkillId);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "scaffold-dotnet.slnx"))
                && File.Exists(Path.Combine(dir.FullName, ".doti", "workflows", "doti", "workflow.yml")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the speckit-doti repo root.");
    }
}
