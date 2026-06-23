using Hx.Cli.Kernel;
using Hx.Scaffold.Core;
using Hx.Scaffold.Core.Prerequisites;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Cli;

public static partial class ScaffoldCommands
{
    public static CliResult New(
        CliMeta meta, string name, string company, string output, string profile, string agentsCsv,
        Action<CliEvent>? onEvent = null,
        PrerequisiteServices? prerequisiteServices = null)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(output))
        {
            return CliResults.Fail(meta, "new", ExitClass.Usage,
                [Diag.Of(ErrorCodes.Usage_InvalidArguments, "Both --name and --output are required.")]);
        }

        string[] agents = agentsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var request = new ScaffoldRequest(name, company, output, profile, agents);
        string sourceRoot = ScaffoldRoot.Resolve(Directory.GetCurrentDirectory());
        CliResult? preflight = CheckPrerequisitesForCommand(
            meta,
            "new",
            new PrerequisiteCheckRequest(sourceRoot, PrerequisiteCommands.New, OutputPath: output),
            prerequisiteServices);
        if (preflight is not null)
        {
            return preflight;
        }

        ScaffoldProof proof = ScaffoldNewRunner.Run(request, sourceRoot, onEvent, meta.Version);
        string summary = $"Scaffold '{name}' ({profile}): {proof.Outcome}.";
        return proof.Outcome == StageOutcome.Pass
            ? CliResults.Ok(meta, "new", summary, proof,
                effects: [new CliEffect("create", Path.GetFullPath(output), "generated + finished + smoked repo")])
            : CliResults.Fail(meta, "new", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, $"{summary} {FailureDetail(proof)}")], summary, proof);
    }
}
