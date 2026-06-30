namespace Hx.Cycle.Core.Actions;

/// <summary>
/// 028 FR-010 / D6: the SINGLE code registry of workflow-affordance <see cref="CommandDescriptor"/>s the agent's valid
/// next-actions are GENERATED from (no hand-authored workflow command info). Stage-advance descriptors project from
/// <see cref="StageModel"/> (one per stage that has a forward successor — the registry static-invariant "one advance
/// per stage"); the recovery menu is the single <see cref="CycleRecoveryPlanner"/> projection, with each
/// <see cref="StageRecoveryStep"/> tagged by the matching <see cref="RecoveryDescriptorIdFor"/> (NEVER a second
/// evaluator — H6/the 027 hazard). The reviewed-no-impact verb, the publish-boundary STOP (<c>Command == null</c>),
/// the train-loop, the bug-phase steps, and the 7 <see cref="CommandKind.Utility"/> skills are static descriptors.
/// Payload-derived next-actions (affected-test hints, per-prereq install strings) are out of scope (B5) and stay
/// locally built.
/// </summary>
public sealed class DotiActionModel
{
    private readonly StageModel _stageModel;
    private readonly IReadOnlyList<CommandDescriptor> _descriptors;

    public DotiActionModel(StageModel stageModel)
    {
        _stageModel = stageModel;
        _descriptors = BuildDescriptors(stageModel);
    }

    /// <summary>All registered descriptors (stage-advance + the static workflow affordances + utility skills).</summary>
    public IReadOnlyList<CommandDescriptor> Descriptors => _descriptors;

    public StageModel StageModel => _stageModel;

    // ----- recovery descriptor ids (the tags the recovery menu wraps; never a second evaluator) -----

    public const string RecoverySafeRefreshId = "recovery.safe-refresh";
    public const string RecoveryReviewRebindId = "recovery.review-rebind";
    public const string RecoveryRerunId = "recovery.rerun";
    public const string RecoveryNotBoundId = "recovery.not-bound";
    public const string RecoveryInsertedStageId = "recovery.inserted-stage";
    public const string RecoveryUnresolvedId = "recovery.unresolved";

    // ----- static workflow-affordance ids -----

    public const string StartFeatureId = "start.specify";
    public const string ReviewRebindVerbId = "verb.review-rebind";
    public const string PublishBoundaryId = "boundary.publish";
    public const string TrainStartNextId = "train.start-next";

    /// <summary>
    /// 028 FR-010 / H6: the recovery descriptor id a <see cref="StageRecoveryStep"/> maps to, BY ITS ALREADY-COMPUTED
    /// <see cref="RestampSafety"/> + status — a pure tag over the single <see cref="CycleRecoveryPlanner"/> projection,
    /// never a re-classification. Keeps the recovery menu sourced from the one projection while still naming each step's
    /// descriptor (the ACID single-source property).
    /// </summary>
    public static string RecoveryDescriptorIdFor(StageRecoveryStep step) => step switch
    {
        { Status: CycleRecoveryPlanner.InsertedStageStatus } => RecoveryInsertedStageId,
        { Safety: RestampSafety.SafeReinterpret } => RecoverySafeRefreshId,
        { Safety: RestampSafety.ReBindContentEqual } => RecoverySafeRefreshId,
        { Safety: RestampSafety.ReviewedNoImpact } => RecoveryReviewRebindId,
        { Safety: RestampSafety.RerunRequired } => RecoveryRerunId,
        { Safety: RestampSafety.NotBound } => RecoveryNotBoundId,
        _ => RecoveryUnresolvedId,
    };

    private static IReadOnlyList<CommandDescriptor> BuildDescriptors(StageModel stageModel)
    {
        var descriptors = new List<CommandDescriptor>();
        descriptors.AddRange(StageAdvanceDescriptors(stageModel));
        descriptors.Add(StartFeatureDescriptor(stageModel));
        descriptors.Add(ReviewRebindDescriptor());
        descriptors.Add(PublishBoundaryDescriptor(stageModel));
        descriptors.Add(TrainStartNextDescriptor());
        descriptors.AddRange(BugPhaseDescriptors());
        descriptors.AddRange(UtilityDescriptors());
        return descriptors;
    }

    /// <summary>One <see cref="CommandKind.Advance"/> descriptor per stage that declares a forward successor — the
    /// FIRST <c>next</c> edge (the linear/release forward edge; the drift-review→specify alt is the train-loop). Applies
    /// when that stage is current AND its prerequisites are fresh. This is the "one advance per stage" invariant.</summary>
    private static IEnumerable<CommandDescriptor> StageAdvanceDescriptors(StageModel stageModel)
    {
        foreach (CycleStage stage in stageModel.Stages)
        {
            string? forward = ForwardSuccessor(stageModel, stage);
            if (forward is null)
            {
                continue; // terminal stage — no advance (the publish boundary applies there instead).
            }

            CycleStage nextStage = stageModel.Find(forward);
            yield return new CommandDescriptor(
                $"advance.{stage.Id}",
                CommandKind.Advance,
                $"Advance to '{forward}'",
                $"'{stage.Id}' is complete and fresh; run the next stage.",
                $"/{nextStage.Command}",
                Applicability.All(Applicability.StageCurrent(stage.Id), Applicability.CheckPassed()));
        }
    }

    /// <summary>The forward successor of a stage: the first declared <c>next</c> that is NOT the train-loop back-edge to
    /// the first stage. drift-review's <c>next: [release, specify]</c> ⇒ forward = release. The first stage's loop-back
    /// is excluded. Null for a terminal stage (no next).</summary>
    private static string? ForwardSuccessor(StageModel stageModel, CycleStage stage)
    {
        string firstStageId = stageModel.Stages[0].Id;
        foreach (string next in stage.Next)
        {
            if (!string.Equals(next, firstStageId, StringComparison.OrdinalIgnoreCase))
            {
                return next;
            }
        }

        return null;
    }

    private static CommandDescriptor StartFeatureDescriptor(StageModel stageModel)
    {
        CycleStage first = stageModel.Stages[0];
        return new CommandDescriptor(
            StartFeatureId,
            CommandKind.Advance,
            "Start a new feature",
            "No active cycle; begin a numbered specification.",
            $"/{first.Command}",
            Applicability.OutsideCycle());
    }

    private static CommandDescriptor ReviewRebindDescriptor() =>
        new(
            ReviewRebindVerbId,
            CommandKind.ReviewRebind,
            "Review + record a no-impact rebind",
            "A stage is stale only on a prerequisite content change; read the surfaced diff, then attest no-impact (or re-author). Clearing the flag without assessing impact is forbidden.",
            "doti cycle review-rebind --target {stage} --attest no-impact",
            Applicability.RecoveryTier(RestampSafety.ReviewedNoImpact));

    /// <summary>The publish-boundary STOP — an operator-decision affordance with NO command (<c>Command == null</c>).
    /// Applies once the terminal stage is stamped + fresh: the next move is the remote push, which the agent never does
    /// unattended (the <c>/doti-auto</c> stop point).</summary>
    private static CommandDescriptor PublishBoundaryDescriptor(StageModel stageModel)
    {
        string terminal = stageModel.Stages[^1].Id;
        return new CommandDescriptor(
            PublishBoundaryId,
            CommandKind.PublishBoundary,
            "STOP: operator decision — publish boundary",
            "The local release is proven; pushing the tag + remote CI is an operator decision, never automated.",
            null,
            Applicability.All(Applicability.StageCurrent(terminal), Applicability.CheckPassed()));
    }

    /// <summary>The release train-loop: start the next feature directly from drift-review (the <c>next: [..., specify]</c>
    /// alt edge). Applies at drift-review when its prerequisites are fresh.</summary>
    private static CommandDescriptor TrainStartNextDescriptor() =>
        new(
            TrainStartNextId,
            CommandKind.TrainLoop,
            "Start the next feature (train loop)",
            "drift-review is complete; begin the next feature's specification on the same release train.",
            "/01-doti-specify",
            Applicability.All(Applicability.StageCurrent("drift-review"), Applicability.CheckPassed()));

    private static IEnumerable<CommandDescriptor> BugPhaseDescriptors()
    {
        yield return BugDescriptor("bug.assess", "Assess the bug (read-only)", "assess", "/doti-bug",
            "no bug assessment is recorded yet");
        yield return BugDescriptor("bug.fix", "Fix the bug (bound to the assessment)", "fix", "/doti-bug",
            "the bug assessment is recorded; implement the bound fix");
        yield return BugDescriptor("bug.test", "Test the fix (honest)", "test", "/doti-bug",
            "the fix is applied; verify it honestly");
    }

    private static CommandDescriptor BugDescriptor(string id, string label, string phase, string invocation, string why) =>
        new(id, CommandKind.BugPhase, label, why, invocation, Applicability.BugPhase(phase));

    private static IEnumerable<CommandDescriptor> UtilityDescriptors()
    {
        (string id, string skill, string why)[] utilities =
        [
            ("util.doti-bug", "doti-bug", "Run a bug fix as an enforced mini-cycle."),
            ("util.doti-amend", "doti-amend", "Amend an already-stamped stage after an approved artifact change."),
            ("util.doti-converge", "doti-converge", "Brownfield/drift reconciliation: append the remaining unbuilt work."),
            ("util.doti-drift-fix", "doti-drift-fix", "Patch a drift by correcting the code (never the spec)."),
            ("util.doti-constitution", "doti-constitution", "Author or amend the project constitution (§2)."),
            ("util.doti-auto", "doti-auto", "Drive the numbered cycle automatically to a target stage."),
            ("util.doti-upgrade", "doti-upgrade", "Upgrade the installed hx tool and reconcile this repo's assets."),
        ];

        foreach ((string id, string skill, string why) in utilities)
        {
            // Utility skills run by name OUTSIDE the numbered cycle — always available (Always), so deleting
            // skills.json nextStage never strands them (B6).
            yield return new CommandDescriptor(
                id, CommandKind.Utility, skill, why, $"/{skill}", Applicability.Always());
        }
    }
}
