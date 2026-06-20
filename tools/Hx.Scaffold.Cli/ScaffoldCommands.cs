using Hx.Cli.Kernel;
using Hx.Scaffold.Core;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Cli;

/// <summary>
/// The Scaffold CLI's command bodies: map generation onto the <see cref="CliResult"/> envelope. Kept out of
/// <c>Program.cs</c> wiring so the mapping is unit-testable in-process. A successful <c>new</c> carries the generated
/// repo as an Effect; a missing <c>--name</c>/<c>--output</c> is a Usage error; a generation/smoke failure is a
/// Validation failure with the <see cref="ScaffoldProof"/> preserved in <c>data</c>.
/// </summary>
public static class ScaffoldCommands
{
    public static CliResult Profile(CliMeta meta) =>
        CliResults.Ok(meta, "profile", $"Default scaffold profile: {ScaffoldBootstrap.DefaultProfile.Name}.",
            ScaffoldBootstrap.DefaultProfile);

    public static CliResult New(CliMeta meta, string name, string company, string output, string profile, string agentsCsv)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(output))
        {
            return CliResults.Fail(meta, "new", ExitClass.Usage,
                [Diag.Of(ErrorCodes.Usage_InvalidArguments, "Both --name and --output are required.")]);
        }

        string[] agents = agentsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var request = new ScaffoldRequest(name, company, output, profile, agents);
        string sourceRoot = ScaffoldRoot.Find(Directory.GetCurrentDirectory());

        ScaffoldProof proof = ScaffoldNewRunner.Run(request, sourceRoot);
        string summary = $"Scaffold '{name}' ({profile}): {proof.Outcome}.";
        return proof.Outcome == StageOutcome.Pass
            ? CliResults.Ok(meta, "new", summary, proof,
                effects: [new CliEffect("create", Path.GetFullPath(output), "generated + finished + smoked repo")])
            : CliResults.Fail(meta, "new", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, summary)], summary, proof);
    }
}
