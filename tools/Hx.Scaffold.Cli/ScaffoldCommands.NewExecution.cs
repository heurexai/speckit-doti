using Hx.Cli.Kernel;
using Hx.Scaffold.Core;
using Hx.Scaffold.Core.Prerequisites;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Cli;

public static partial class ScaffoldCommands
{
    /// <summary>029: run the validated <see cref="NewSetupPlan"/> — resolve the payload root, run the trusted
    /// prerequisite preflight, generate+finish+smoke via <see cref="ScaffoldNewRunner"/>, and render the proof with the
    /// operator-intent checklist. Behaviour is identical to the previous inline body; the command method stays thin.</summary>
    internal static CliResult ExecuteNew(
        CliMeta meta, NewSetupPlan plan, Action<CliEvent>? onEvent, PrerequisiteServices? prerequisiteServices)
    {
        ScaffoldRequest request = plan.Request!;
        string sourceRoot = InstalledPayload.ResolveAssetRoot(Directory.GetCurrentDirectory());
        CliResult? preflight = CheckPrerequisitesForCommand(
            meta,
            "new",
            new PrerequisiteCheckRequest(sourceRoot, PrerequisiteCommands.New, OutputPath: request.OutputPath),
            prerequisiteServices);
        if (preflight is not null)
        {
            return preflight;
        }

        ScaffoldProof proof = ScaffoldNewRunner.Run(request, sourceRoot, onEvent, meta.Version);
        string summary = $"Scaffold '{request.Name}' ({request.Profile}): {proof.Outcome}.";
        return proof.Outcome != StageOutcome.Pass
            ? CliResults.Fail(meta, "new", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, $"{summary} {FailureDetail(proof)}")], summary, proof)
            : CliResults.Ok(meta, "new", summary, proof,
                effects: [new CliEffect("create", Path.GetFullPath(request.OutputPath), "generated + finished + smoked repo")],
                nextActions: plan.Checklist);
    }
}
