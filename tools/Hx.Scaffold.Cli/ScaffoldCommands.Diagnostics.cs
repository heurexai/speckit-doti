using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Cli;

public static partial class ScaffoldCommands
{
    private static string FailureDetail(ScaffoldProof proof)
    {
        if (proof.Template.Outcome != StageOutcome.Pass)
        {
            return $"Template {proof.Template.Outcome}: {proof.Template.Source}";
        }

        GateStep? failedStep = proof.Smoke.Steps.FirstOrDefault(step =>
            step.Outcome is StageOutcome.Fail or StageOutcome.Blocked);
        if (failedStep is null)
        {
            return "No failed scaffold stage reported additional evidence.";
        }

        GateEvidence? evidence = failedStep.Evidence.FirstOrDefault();
        string message = evidence?.Message ?? "no evidence message";
        return $"Smoke {failedStep.Name} {failedStep.Outcome}: {message}";
    }
}
