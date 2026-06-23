using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;

namespace Hx.Sentrux.Core;

public static partial class SentruxChecker
{
    private static RegressionCheck RunRegression(string executable, string root, SentruxPolicy policy, int? qualitySignal)
    {
        ProcessRunResult gateRun = ProcessRunner.Run(SentruxProcessAdapter.GateCompare(executable, root));
        SentruxOutputParser.GateReport gate = SentruxOutputParser.ParseGate(gateRun.StandardOutput);
        int? after = gate.SignalAfter ?? qualitySignal;
        (StageOutcome outcome, int? delta) = SentruxRegression.Evaluate(gate.SignalBefore, after, policy.SignalToleranceBand);
        if (gate.SignalBefore is null)
        {
            outcome = StageOutcome.Blocked;
        }
        else if (gate.Degraded)
        {
            outcome = StageOutcome.Fail;
        }

        List<string> notes = RegressionNotes(gate, outcome, delta, policy.SignalToleranceBand);
        return new RegressionCheck(outcome, gate.SignalBefore, notes);
    }

    private static List<string> RegressionNotes(
        SentruxOutputParser.GateReport gate,
        StageOutcome outcome,
        int? delta,
        int toleranceBand)
    {
        List<string> notes = [];
        if (gate.SignalBefore is null)
        {
            notes.Add("Could not read the Sentrux gate baseline signal; failing closed.");
        }
        else if (delta is < 0 && -delta.Value > toleranceBand)
        {
            notes.Add($"Quality signal dropped {-delta!.Value} (> tolerance {toleranceBand}).");
        }

        if (gate.Degraded)
        {
            notes.Add("Sentrux gate reported structural degradation; absolute constraints are enforced by the rule check.");
        }

        return notes;
    }

    private sealed record RegressionCheck(
        StageOutcome Outcome,
        int? BaselineSignal,
        IReadOnlyList<string> Notes);
}
