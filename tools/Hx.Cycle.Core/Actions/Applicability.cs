namespace Hx.Cycle.Core.Actions;

/// <summary>
/// 028 FR-010 / D6: declarative applicability over a CLOSED named-condition vocabulary — never an opaque predicate, so
/// the engine evaluates applicability AND renders the human "available when …" from the SAME data (the ACID no-drift
/// guarantee: <see cref="Describe"/> reads exactly what <see cref="Evaluate"/> tests). The vocabulary is
/// <c>StageCurrent</c> / <c>CheckPassed</c> / <c>RecoveryTier</c> / <c>GateFailed</c> / <c>TrainValid</c> /
/// <c>OutsideCycle</c> / <c>BugPhase</c>, composed by <see cref="All"/> / <see cref="Any"/>. Each is a function of the
/// <see cref="CommandContext"/> only — no new freshness logic, no second evaluator.
/// </summary>
public abstract class Applicability
{
    public abstract bool Evaluate(CommandContext context);

    /// <summary>The human "available when …" string — derived from the SAME named condition the evaluator tests.</summary>
    public abstract string Describe();

    public static Applicability StageCurrent(string stageId) => new StageCurrentCondition(stageId);

    public static Applicability CheckPassed() => new CheckPassedCondition();

    public static Applicability RecoveryTier(RestampSafety tier) => new RecoveryTierCondition(tier);

    public static Applicability GateFailed() => new GateFailedCondition();

    public static Applicability TrainValid() => new TrainValidCondition();

    public static Applicability OutsideCycle() => new OutsideCycleCondition();

    public static Applicability BugPhase(string phase) => new BugPhaseCondition(phase);

    public static Applicability Always() => new AllCondition([]);

    public static Applicability All(params Applicability[] conditions) => new AllCondition(conditions);

    public static Applicability Any(params Applicability[] conditions) => new AnyCondition(conditions);

    private sealed class StageCurrentCondition(string stageId) : Applicability
    {
        public override bool Evaluate(CommandContext context) =>
            string.Equals(context.CurrentStage, stageId, StringComparison.OrdinalIgnoreCase);

        public override string Describe() => $"the current stage is '{stageId}'";
    }

    private sealed class CheckPassedCondition : Applicability
    {
        public override bool Evaluate(CommandContext context) => context.CheckPassed;

        public override string Describe() => "the stage's prerequisites are all fresh";
    }

    private sealed class RecoveryTierCondition(RestampSafety tier) : Applicability
    {
        public override bool Evaluate(CommandContext context) => context.HasRecoveryTier(tier);

        public override string Describe() => $"the recovery plan has a {tier} step";
    }

    private sealed class GateFailedCondition : Applicability
    {
        public override bool Evaluate(CommandContext context) => context.GateFailed;

        public override string Describe() => "the gate proof failed";
    }

    private sealed class TrainValidCondition : Applicability
    {
        public override bool Evaluate(CommandContext context) => context.TrainValid;

        public override string Describe() => "the release train is valid";
    }

    private sealed class OutsideCycleCondition : Applicability
    {
        public override bool Evaluate(CommandContext context) => context.IsOutsideCycle;

        public override string Describe() => "there is no active cycle";
    }

    private sealed class BugPhaseCondition(string phase) : Applicability
    {
        public override bool Evaluate(CommandContext context) =>
            string.Equals(context.BugPhase, phase, StringComparison.OrdinalIgnoreCase);

        public override string Describe() => $"the bug cycle is in the '{phase}' phase";
    }

    private sealed class AllCondition(IReadOnlyList<Applicability> conditions) : Applicability
    {
        public override bool Evaluate(CommandContext context) => conditions.All(c => c.Evaluate(context));

        public override string Describe() =>
            conditions.Count == 0 ? "always" : string.Join(" and ", conditions.Select(c => c.Describe()));
    }

    private sealed class AnyCondition(IReadOnlyList<Applicability> conditions) : Applicability
    {
        public override bool Evaluate(CommandContext context) => conditions.Any(c => c.Evaluate(context));

        public override string Describe() =>
            conditions.Count == 0 ? "never" : string.Join(" or ", conditions.Select(c => c.Describe()));
    }
}
