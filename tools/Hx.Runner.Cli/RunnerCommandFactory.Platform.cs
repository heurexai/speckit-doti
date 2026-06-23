using System.CommandLine;
using Hx.Cli.Kernel;

namespace Hx.Runner.Cli;

internal static partial class RunnerCommandFactory
{
    private static void AddBootstrap(RootCommand rootCommand, CliMeta meta)
    {
        Command proofCommand = new("bootstrap-proof", "Emit the bootstrap advisory proof.");
        Option<bool> proofJson = CliApp.JsonOption();
        proofCommand.Options.Add(proofJson);
        proofCommand.SetAction(parseResult => CliHost.Run(meta, "bootstrap-proof",
            () => RunnerCommands.BootstrapProof(meta), forceJson: CliApp.ForceJson(parseResult, proofJson)));
        rootCommand.Subcommands.Add(proofCommand);
    }

    private static void AddPlatform(RootCommand rootCommand, CliMeta meta)
    {
        Command platformCommand = new("platform", "Cross-platform diagnostics.");
        Command platformProbeCommand = new("probe", "Emit platform warning-mode diagnostics.");
        Option<bool> probeJson = CliApp.JsonOption();
        platformProbeCommand.Options.Add(probeJson);
        platformProbeCommand.SetAction(parseResult => CliHost.Run(meta, "platform probe",
            () => RunnerCommands.PlatformProbe(meta), forceJson: CliApp.ForceJson(parseResult, probeJson)));
        platformCommand.Subcommands.Add(platformProbeCommand);
        rootCommand.Subcommands.Add(platformCommand);
    }
}
