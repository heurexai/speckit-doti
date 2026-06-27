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
        (SentruxRegressionVerdict verdict, int? delta) = SentruxRegression.Evaluate(
            gate.SignalBefore, after, policy.SignalToleranceBand, policy.EffectiveEscalationBandMultiplier);

        StageOutcome outcome;
        if (gate.SignalBefore is null)
        {
            outcome = StageOutcome.Blocked;
            verdict = SentruxRegressionVerdict.Fail;
        }
        else if (gate.Degraded)
        {
            // BL-1: an absolute structural-rule violation is an IMMEDIATE hard fail — never band-eligible (it IS the
            // "look at the architecture overall" outcome). Applied AFTER the deviation verdict so the band only ever
            // measures the quality-signal drop, never structural degradation.
            outcome = StageOutcome.Fail;
            verdict = SentruxRegressionVerdict.Fail;
        }
        else
        {
            // A within-band deviation exceeds tolerance and fails the gate closed; the two-optimization-try recovery
            // lives in the cycle (SentruxOptimizationLog), keyed off this EscalationBand verdict.
            outcome = verdict == SentruxRegressionVerdict.Pass ? StageOutcome.Pass : StageOutcome.Fail;
        }

        List<string> notes = RegressionNotes(gate, verdict, delta, policy.SignalToleranceBand, policy.EffectiveEscalationBandMultiplier);
        return new RegressionCheck(outcome, verdict, gate.SignalBefore, notes);
    }

    private static List<string> RegressionNotes(
        SentruxOutputParser.GateReport gate,
        SentruxRegressionVerdict verdict,
        int? delta,
        int toleranceBand,
        double escalationMultiplier)
    {
        List<string> notes = [];
        int limit = SentruxRegression.EscalationLimit(toleranceBand, escalationMultiplier);
        if (gate.SignalBefore is null)
        {
            notes.Add("Could not read the Sentrux gate baseline signal; failing closed.");
        }
        else if (verdict == SentruxRegressionVerdict.EscalationBand)
        {
            notes.Add($"Quality signal dropped {-delta!.Value} — within the escalation band (> tolerance {toleranceBand}, <= {limit}); two optimization attempts are allowed before a structural architecture review.");
        }
        else if (delta is < 0 && -delta.Value > limit)
        {
            notes.Add($"Quality signal dropped {-delta!.Value} (> escalation limit {limit}).");
        }

        if (gate.Degraded)
        {
            notes.Add("Sentrux gate reported structural degradation; absolute constraints are enforced by the rule check (a hard fail, NOT band-eligible).");
        }

        return notes;
    }

    private sealed record RegressionCheck(
        StageOutcome Outcome,
        SentruxRegressionVerdict Verdict,
        int? BaselineSignal,
        IReadOnlyList<string> Notes);
}
