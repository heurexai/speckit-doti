namespace Hx.Doti.Core.Workflow;

/// <summary>An optional alternate next-action at a stage (e.g. skip-clarify, or the drift-review release-train branch).
/// A presentation DTO consumed by the renderer + <c>hx describe</c>.</summary>
public sealed record DotiWorkflowAlternateAction(
    string Id,
    string Label,
    string CommandName,
    bool Optional);

/// <summary>One projected workflow stage for the renderer + <c>hx describe</c>: its structural identity (ordinal,
/// stage id, command name, skill id, declared <c>next</c> edges) composed with its rehomed presentation prose (display
/// title, status, alternate actions, branching next-step). Built by <see cref="DotiWorkflowPresentation"/> from the
/// engine's <see cref="Hx.Cycle.Core.StageModel"/> + the prose table — no hand-maintained stage list.</summary>
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

/// <summary>The rehomed presentation prose for one numbered cycle stage (H1): its human title, its workflow status,
/// the branching next-step prose, and any optional alternate actions. Data, not logic — keyed by stage id in
/// <see cref="StagePresentation"/>.</summary>
public sealed record StageProse(
    string Title,
    string Status,
    string NextStep,
    IReadOnlyList<DotiWorkflowAlternateAction> AlternateActions);

/// <summary>
/// 028 FR-010 / H1: the single data table of rehomed workflow presentation prose — the per-cycle-stage title/status/
/// next-step/alternate-actions and the 7 utility skills' next-step prose that the deleted <c>DotiWorkflowRegistry</c>
/// (and <c>skills.json nextStage</c>) used to carry. Pure data so the projection in
/// <see cref="DotiWorkflowPresentation"/> composes it with the engine's structural <see cref="Hx.Cycle.Core.StageModel"/>
/// without a giant method (the registry-is-data discipline).
/// </summary>
public static class StagePresentation
{
    private static readonly IReadOnlyDictionary<string, StageProse> ByStageId =
        new Dictionary<string, StageProse>(StringComparer.OrdinalIgnoreCase)
        {
            ["specify"] = new(
                "Specify", "required",
                "Run `/02-doti-clarify` to resolve ambiguities, or `/03-doti-plan` when no clarification is needed.",
                [new DotiWorkflowAlternateAction("skip-clarify", "Skip clarify when no ambiguity remains", "doti-plan", true)]),
            ["clarify"] = new(
                "Clarify", "conditional",
                "Run `/03-doti-plan` to author the implementation plan.",
                []),
            ["plan"] = new(
                "Plan", "required",
                // FR-028/SC-013: mark arch-review conditional/advisory when architecture impact is absent, required when present.
                "Run `/04-doti-arch-review` — required when the change has architecture impact (production code, contracts, CLI, dependencies, or scaffold-template code), advisory (a quick no-op review record) when it has none.",
                []),
            ["arch-review"] = new(
                "Arch-Review", "advisory-required",
                "Run `/05-doti-tasks` to break the reviewed plan into executable tasks.",
                []),
            ["tasks"] = new(
                "Tasks", "required",
                "Run `/06-doti-analyze` for a cross-artifact consistency review.",
                []),
            ["analyze"] = new(
                "Analyze", "required",
                "Run `/07-doti-implement` to implement the tasks.",
                []),
            ["implement"] = new(
                "Implement", "required",
                "Run `/08-doti-drift-review` to check the diff against the approved design.",
                []),
            ["drift-review"] = new(
                "Drift-Review", "required",
                "Run `/09-doti-release` to release, or `/01-doti-specify` to add another feature to this release train.",
                [new DotiWorkflowAlternateAction("continue-release-train", "Start another specification before release", "doti-specify", true)]),
            ["release"] = new(
                "Release", "terminal",
                "Cycle complete. Start the next feature with `/01-doti-specify`.",
                []),
        };

    /// <summary>The 7 utility skills' next-step prose — formerly <c>skills.json nextStage</c> (B6: utility skills are
    /// not stage-chained, so their next-step prose lives here, keyed by bare skill name).</summary>
    public static readonly IReadOnlyDictionary<string, string> UtilityNextSteps =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["doti-upgrade"] = "Resume your active cycle stage, or start a feature with `/01-doti-specify`.",
            ["doti-bug"] = "A passing test closes the bug. Resume your active cycle stage, or start a feature with `/01-doti-specify`.",
            ["doti-converge"] = "Continue with `/07-doti-implement` to build the appended tasks, or `/08-doti-drift-review` to reconcile installed assets.",
            ["doti-amend"] = "Resume your active cycle stage; if the amendment touched code already under review, run `/08-doti-drift-review`.",
            ["doti-drift-fix"] = "Re-run `/08-doti-drift-review` to confirm the diff now matches the approved design, then resume your cycle.",
            ["doti-constitution"] = "Resume your active cycle stage; the next `/03-doti-plan` and `/04-doti-arch-review` evaluate against the fresh §2 automatically.",
            ["doti-auto"] = "When auto mode stops at a blocker, resolve it and re-invoke `/doti-auto` to resume from the current stage; when it reaches the target, the cycle sits at that stage (the local release, or your `--until` bound).",
        };

    public static StageProse For(string stageId) =>
        ByStageId.TryGetValue(stageId, out StageProse? prose)
            ? prose
            : throw new InvalidOperationException($"No stage presentation declared for cycle stage '{stageId}'.");

    /// <summary>Try to resolve a stage's prose; false for a non-canonical stage id (e.g. a minimal test workflow whose
    /// stages are not the canonical 9) so the projection skips it rather than throwing.</summary>
    public static bool TryFor(string stageId, out StageProse? prose) => ByStageId.TryGetValue(stageId, out prose);
}
