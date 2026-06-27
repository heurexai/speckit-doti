using System.CommandLine;
using Hx.Cli.Kernel;

namespace Hx.Runner.Cli;

public static partial class RunnerCommandFactory
{
    private static void AddSentrux(RootCommand rootCommand, CliMeta meta)
    {
        Command sentruxCommand = new("sentrux", "Sentrux structural-quality gate.");
        AddSentruxVerify(sentruxCommand, meta);
        AddSentruxBaseline(sentruxCommand, meta);
        AddSentruxCheck(sentruxCommand, meta);
        rootCommand.Subcommands.Add(sentruxCommand);
    }

    private static void AddSentruxVerify(Command sentruxCommand, CliMeta meta)
    {
        Command command = new("verify", "Verify the vendored Sentrux manifest, binary, grammars, and fork stamp.");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "sentrux verify",
            () => RunnerCommands.SentruxVerify(meta, parseResult.GetValue(repo)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        sentruxCommand.Subcommands.Add(command);
    }

    private static void AddSentruxBaseline(Command sentruxCommand, CliMeta meta)
    {
        Command command = new("baseline", "Raise the Sentrux baseline (explicit operator rebaseline). Refused without --authorize-rebaseline + a change-set-fresh arch-review classifying the growth as functionality-driven (FR-031).");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> authorize = new("--authorize-rebaseline") { Description = "Explicit operator intent to RAISE the baseline (the gate path never sets this; first-scaffold creation runs elsewhere).", DefaultValueFactory = _ => false };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(authorize);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "sentrux baseline",
            () => RunnerCommands.SentruxBaseline(meta, parseResult.GetValue(repo)!, parseResult.GetValue(authorize)),
            forceJson: CliApp.ForceJson(parseResult, json)));
        sentruxCommand.Subcommands.Add(command);
    }

    private static void AddSentruxCheck(Command sentruxCommand, CliMeta meta)
    {
        Command command = new("check", "Run the merged Sentrux rule check + regression gate.");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "sentrux check",
            () => RunnerCommands.SentruxCheck(meta, parseResult.GetValue(repo)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        sentruxCommand.Subcommands.Add(command);
    }
}
