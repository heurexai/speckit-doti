using Hx.Tooling.Contracts;

namespace Hx.Doti.Core.Workflow;

public static class DotiWorkflowDescribe
{
    public static CliDescribeWorkflow Build() =>
        new(
            "doti",
            DotiWorkflowRegistry.Stages.Select(stage => new CliDescribeWorkflowStage(
                stage.Ordinal,
                stage.StageId,
                stage.CommandName,
                stage.SkillId,
                stage.DisplayTitle,
                stage.StageStatus,
                stage.NextStageIds,
                stage.AlternateActions.Select(action => new CliDescribeWorkflowAlternateAction(
                    action.Id,
                    action.Label,
                    action.CommandName,
                    action.Optional)).ToArray(),
                stage.NextStep)).ToArray());
}
