using System.CommandLine;
using Hx.Cli.Kernel;

namespace Hx.Runner.Cli;

public static partial class RunnerCommandFactory
{
    private static void AddDotiCycle(Command dotiCommand, CliMeta meta)
    {
        Command cycleCommand = new("cycle", "Doti cycle state: stamp diff-bound stage proofs, report freshness, and enforce the chokepoints.");
        AddCycleStamp(cycleCommand, meta);
        AddCycleStatus(cycleCommand, meta);
        AddCycleCheck(cycleCommand, meta);
        dotiCommand.Subcommands.Add(cycleCommand);
    }

    private static void AddCycleStamp(Command cycleCommand, CliMeta meta)
    {
        Command command = new("stamp", "Record a stage's diff-bound proof and advance the cycle state.");
        Option<string> stage = new("--stage") { Description = "Stage id (specify, clarify, plan, tasks, analyze, arch-review, implement, drift-review, release).", DefaultValueFactory = _ => "" };
        Option<string> feature = new("--feature") { Description = "Numbered feature slug (required on the first stamp; e.g. 001-doti-cycle-state).", DefaultValueFactory = _ => "" };
        Option<string> baseRef = new("--base") { Description = "Base ref for the change-set identity (default: dev if it resolves, else HEAD).", DefaultValueFactory = _ => "" };
        Option<string> releaseIntent = new("--release-intent") { Description = "Release intent for a release-stage transition: major, minor, or patch. Adds the matching GitVersion +semver signal to the automatic transition commit.", DefaultValueFactory = _ => "" };
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(stage);
        command.Options.Add(feature);
        command.Options.Add(baseRef);
        command.Options.Add(releaseIntent);
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti cycle stamp",
            () => RunnerCommands.CycleStamp(meta, parseResult.GetValue(repo)!, parseResult.GetValue(stage)!,
                parseResult.GetValue(feature)!, parseResult.GetValue(baseRef)!, parseResult.GetValue(releaseIntent)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        cycleCommand.Subcommands.Add(command);
    }

    private static void AddCycleStatus(Command cycleCommand, CliMeta meta)
    {
        Command command = new("status", "Report the cycle state with a freshness verdict per stamped stage (non-enforcing).");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti cycle status",
            () => RunnerCommands.CycleStatus(meta, parseResult.GetValue(repo)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        cycleCommand.Subcommands.Add(command);
    }

    private static void AddCycleCheck(Command cycleCommand, CliMeta meta)
    {
        Command command = new("check", "Fail-closed: verify a stage's prerequisites are all stamped + fresh + valid.");
        Option<string> stage = new("--stage") { Description = "Stage id whose prerequisites to verify.", DefaultValueFactory = _ => "" };
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(stage);
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti cycle check",
            () => RunnerCommands.CycleCheck(meta, parseResult.GetValue(repo)!, parseResult.GetValue(stage)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        cycleCommand.Subcommands.Add(command);
    }

}
