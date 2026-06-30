using Hx.Cycle.Core;
using Hx.Cycle.Core.Actions;

namespace Hx.Doti.Core.Workflow;

/// <summary>
/// 028 FR-010 / H1 / H8: the workflow PRESENTATION projection — the rehomed home for the prose that the deleted
/// <c>DotiWorkflowRegistry</c> used to carry (each stage's display title, status, branching next-step prose, and
/// alternate actions, plus the 7 utility skills' next-step prose). The STRUCTURE (ordinal, command, skill id, the
/// <c>next</c> edges) projects from the engine's <see cref="StageModel"/> + <see cref="DotiActionModel"/> (the single
/// source of the stage chain); only the heavy presentation prose lives here in <see cref="Hx.Doti.Core"/>, never in
/// the dependency-leaf engine (H8). The renderer + <c>hx describe</c> read this projection so there is one source for
/// the workflow affordances and nothing hand-authored drifts.
/// </summary>
public sealed class DotiWorkflowPresentation
{
    private readonly StageModel _stageModel;
    private readonly IReadOnlyList<DotiWorkflowStage> _stages;
    private readonly IReadOnlyDictionary<string, string> _utilityNextSteps;

    private DotiWorkflowPresentation(StageModel stageModel)
    {
        _stageModel = stageModel;
        _stages = BuildStages(stageModel);
        _utilityNextSteps = StagePresentation.UtilityNextSteps;
    }

    /// <summary>Load the stage model from the installed <c>workflow.yml</c> and build the presentation projection.</summary>
    public static DotiWorkflowPresentation Load(string repoRoot)
    {
        string workflowYml = Path.GetFullPath(Path.Combine(
            repoRoot, ".doti", "workflows", "doti", "workflow.yml".Replace('/', Path.DirectorySeparatorChar)));
        return new DotiWorkflowPresentation(StageModel.Load(workflowYml));
    }

    /// <summary>The 9 numbered cycle stages, in declaration order, with their structure + rehomed prose.</summary>
    public IReadOnlyList<DotiWorkflowStage> Stages => _stages;

    /// <summary>The engine stage model this projection is built over (the single source of the stage chain) — used to
    /// build the <see cref="DotiActionModel"/> for the <c>{commandAvailability}</c> projection.</summary>
    public StageModel StageModel => _stageModel;

    /// <summary>
    /// 007 T032: resolve the rendered identity for a skill. A numbered cycle stage (specify..release) keeps its
    /// <c>NN-</c> ordinal SkillId, command name, and next-step from the stage chain. A UTILITY skill that is not a cycle
    /// stage (e.g. <c>doti-upgrade</c>) renders UNNUMBERED — its SkillId is the bare command name and its next-step
    /// comes from this presentation's utility table — so it never pollutes the cycle. (The <paramref name="_"/> legacy
    /// fallback parameter is retained for the renderer call-site but no longer sourced from <c>skills.json</c>.)
    /// </summary>
    public (string SkillId, string CommandName, string NextStep) ResolveSkillIdentity(string commandName, string? _ = null)
    {
        DotiWorkflowStage? stage = _stages.FirstOrDefault(
            s => string.Equals(s.CommandName, commandName, StringComparison.OrdinalIgnoreCase));
        if (stage is not null)
        {
            return (stage.SkillId, stage.CommandName, stage.NextStep);
        }

        // A utility skill renders UNNUMBERED with its rehomed next-step (formerly skills.json nextStage, B6). An unknown
        // skill (not a cycle stage, not a known utility — e.g. a minimal test payload) degrades gracefully to its bare
        // name + empty next-step, never throwing, exactly as the deleted registry's fallback did.
        string nextStep = _utilityNextSteps.TryGetValue(commandName, out string? prose) ? prose : string.Empty;
        return (commandName, commandName, nextStep);
    }

    private static IReadOnlyList<DotiWorkflowStage> BuildStages(StageModel stageModel)
    {
        var stages = new List<DotiWorkflowStage>();
        for (int i = 0; i < stageModel.Stages.Count; i++)
        {
            CycleStage stage = stageModel.Stages[i];
            if (!StagePresentation.TryFor(stage.Id, out StageProse? prose))
            {
                continue; // a non-canonical stage id (e.g. a minimal test workflow) carries no presentation prose.
            }

            int ordinal = i + 1;
            string commandName = CanonicalCommandName(stage.Id);
            stages.Add(new DotiWorkflowStage(
                ordinal,
                stage.Id,
                commandName,
                SkillIdOf(stage, ordinal, commandName),
                $"{ordinal:D2}-{prose!.Title}",
                prose.Status,
                stage.Next,
                prose.AlternateActions,
                prose.NextStep));
        }

        return stages;
    }

    /// <summary>The canonical bare skill/command name for a cycle stage — always <c>doti-{stageId}</c> (the skill name
    /// in <c>skills.json</c>). Derived from the stage id so resolution is independent of how the target repo's
    /// <c>workflow.yml</c> spells its <c>command</c> field (robust against a minimal/partial workflow).</summary>
    private static string CanonicalCommandName(string stageId) => $"doti-{stageId}";

    /// <summary>The numbered skill id (<c>01-doti-specify</c>): the workflow <c>command</c> when it already carries the
    /// <c>NN-doti-x</c> form (the real installed repo), else the canonical <c>{ordinal:D2}-{commandName}</c>.</summary>
    private static string SkillIdOf(CycleStage stage, int ordinal, string commandName)
    {
        string command = stage.Command;
        int dash = command.IndexOf('-');
        bool numbered = dash > 0 && command[..dash].All(char.IsDigit);
        return numbered ? command : $"{ordinal:D2}-{commandName}";
    }
}
