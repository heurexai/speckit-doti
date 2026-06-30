using Hx.Tooling.Contracts;

namespace Hx.Doti.Core.Workflow;

/// <summary>
/// 028 FR-010: the <c>hx describe</c> workflow capability model, projected from the model-backed
/// <see cref="DotiWorkflowPresentation"/> (the engine <see cref="Hx.Cycle.Core.StageModel"/> + the rehomed prose) —
/// no longer a hand-maintained stage list. Resolves the stage model from the repo root (defaulting to the current
/// directory, where <c>hx</c> runs).
/// </summary>
public static class DotiWorkflowDescribe
{
    public static CliDescribeWorkflow Build()
    {
        // The describe surface is built at CLI construction in ANY directory (the `hx` global tool runs anywhere).
        // Degrade gracefully to an empty cycle workflow when there is no installed workflow.yml (outside a doti repo,
        // a minimal test payload) — the deleted DotiWorkflowRegistry never required a file, and throwing here would
        // abort the entire `hx` command tree. The full stage detail is present whenever a workflow.yml is.
        string repoRoot = Directory.GetCurrentDirectory();
        string workflowYml = Path.Combine(repoRoot, ".doti", "workflows", "doti", "workflow.yml");
        return File.Exists(workflowYml) ? Build(repoRoot) : new CliDescribeWorkflow("doti", []);
    }

    public static CliDescribeWorkflow Build(string repoRoot) =>
        Build(DotiWorkflowPresentation.Load(repoRoot));

    public static CliDescribeWorkflow Build(DotiWorkflowPresentation workflow) =>
        new(
            "doti",
            workflow.Stages.Select(stage => new CliDescribeWorkflowStage(
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
