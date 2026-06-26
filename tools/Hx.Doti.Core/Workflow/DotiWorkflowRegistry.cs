namespace Hx.Doti.Core.Workflow;

public sealed record DotiWorkflowAlternateAction(
    string Id,
    string Label,
    string CommandName,
    bool Optional);

public sealed record DotiWorkflowStage(
    int Ordinal,
    string StageId,
    string CommandName,
    string SkillId,
    string DisplayTitle,
    string StageStatus,
    IReadOnlyList<string> NextStageIds,
    IReadOnlyList<DotiWorkflowAlternateAction> AlternateActions,
    string NextStep);

public static class DotiWorkflowRegistry
{
    public static IReadOnlyList<DotiWorkflowStage> Stages { get; } =
    [
        Stage(1, "specify", "doti-specify", "Specify", "required",
            ["clarify"],
            [new DotiWorkflowAlternateAction("skip-clarify", "Skip clarify when no ambiguity remains", "doti-plan", true)],
            "Run `/02-doti-clarify` to resolve ambiguities, or `/03-doti-plan` when no clarification is needed."),
        Stage(2, "clarify", "doti-clarify", "Clarify", "conditional",
            ["plan"], [],
            "Run `/03-doti-plan` to author the implementation plan."),
        Stage(3, "plan", "doti-plan", "Plan", "required",
            ["tasks"], [],
            "Run `/04-doti-tasks` to break the plan into executable tasks."),
        Stage(4, "tasks", "doti-tasks", "Tasks", "required",
            ["analyze"], [],
            "Run `/05-doti-analyze` for a cross-artifact consistency review."),
        Stage(5, "analyze", "doti-analyze", "Analyze", "required",
            ["arch-review"], [],
            // FR-028/SC-013: mark arch-review conditional/advisory when architecture impact is absent, required when present.
            "Run `/06-doti-arch-review` — required when the change has architecture impact (production code, contracts, CLI, dependencies, or scaffold-template code), advisory (a quick no-op review record) when it has none."),
        Stage(6, "arch-review", "doti-arch-review", "Arch-Review", "advisory-required",
            ["implement"], [],
            "Run `/07-doti-implement` to implement the tasks."),
        Stage(7, "implement", "doti-implement", "Implement", "required",
            ["drift-review"], [],
            "Run `/08-doti-drift-review` to check the diff against the approved design."),
        Stage(8, "drift-review", "doti-drift-review", "Drift-Review", "required",
            ["release", "specify"],
            [new DotiWorkflowAlternateAction("continue-release-train", "Start another specification before release", "doti-specify", true)],
            "Run `/09-doti-release` to release, or `/01-doti-specify` to add another feature to this release train."),
        Stage(9, "release", "doti-release", "Release", "terminal",
            [], [],
            "Cycle complete. Start the next feature with `/01-doti-specify`.")
    ];

    public static DotiWorkflowStage FindByStageId(string stageId) =>
        Stages.FirstOrDefault(s => string.Equals(s.StageId, stageId, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Unknown Doti workflow stage '{stageId}'.");

    public static DotiWorkflowStage FindByCommandName(string commandName) =>
        Stages.FirstOrDefault(s => string.Equals(s.CommandName, commandName, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Unknown Doti workflow command '{commandName}'.");

    public static bool IsNormalCommand(string commandName) =>
        Stages.Any(s => string.Equals(s.CommandName, commandName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 007 T032: resolve the rendered identity for a skill. A numbered cycle stage (specify..release) keeps its
    /// <c>NN-</c> ordinal SkillId, command name, and next-step from the registry. A UTILITY skill that is not a cycle
    /// stage (e.g. <c>doti-upgrade</c>, invoked anytime, never transitioned through) renders UNNUMBERED — its SkillId
    /// is the bare command name and its next-step comes from the skills.json entry — so it never pollutes the cycle.
    /// </summary>
    public static (string SkillId, string CommandName, string NextStep) ResolveSkillIdentity(
        string commandName, string fallbackNextStep)
    {
        DotiWorkflowStage? stage = Stages.FirstOrDefault(
            s => string.Equals(s.CommandName, commandName, StringComparison.OrdinalIgnoreCase));
        return stage is not null
            ? (stage.SkillId, stage.CommandName, stage.NextStep)
            : (commandName, commandName, fallbackNextStep);
    }

    private static DotiWorkflowStage Stage(
        int ordinal,
        string stageId,
        string commandName,
        string title,
        string status,
        IReadOnlyList<string> nextStageIds,
        IReadOnlyList<DotiWorkflowAlternateAction> alternateActions,
        string nextStep) =>
        new(
            ordinal,
            stageId,
            commandName,
            $"{ordinal:D2}-{commandName}",
            $"{ordinal:D2}-{title}",
            status,
            nextStageIds,
            alternateActions,
            nextStep);
}
