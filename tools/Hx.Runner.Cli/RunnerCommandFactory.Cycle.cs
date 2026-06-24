using System.CommandLine;
using Hx.Cli.Kernel;

namespace Hx.Runner.Cli;

internal static partial class RunnerCommandFactory
{
    private static void AddDotiCycle(Command dotiCommand, CliMeta meta)
    {
        Command cycleCommand = new("cycle", "Doti cycle state: stamp diff-bound stage proofs, report freshness, and enforce the chokepoints.");
        AddCycleStamp(cycleCommand, meta);
        AddCycleStatus(cycleCommand, meta);
        AddCycleCheck(cycleCommand, meta);
        AddCycleCommit(cycleCommand, meta);
        AddPrecommitGuard(cycleCommand, meta);
        dotiCommand.Subcommands.Add(cycleCommand);
    }

    private static void AddCycleStamp(Command cycleCommand, CliMeta meta)
    {
        Command command = new("stamp", "Record a stage's diff-bound proof and advance the cycle state.");
        Option<string> stage = new("--stage") { Description = "Stage id (specify, clarify, plan, tasks, analyze, arch-review, implement, drift-review, commit, release).", DefaultValueFactory = _ => "" };
        Option<string> feature = new("--feature") { Description = "Numbered feature slug (required on the first stamp; e.g. 001-doti-cycle-state).", DefaultValueFactory = _ => "" };
        Option<string> baseRef = new("--base") { Description = "Base ref for the change-set identity (default: dev if it resolves, else HEAD).", DefaultValueFactory = _ => "" };
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(stage);
        command.Options.Add(feature);
        command.Options.Add(baseRef);
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti cycle stamp",
            () => RunnerCommands.CycleStamp(meta, parseResult.GetValue(repo)!, parseResult.GetValue(stage)!,
                parseResult.GetValue(feature)!, parseResult.GetValue(baseRef)!),
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

    private static void AddCycleCommit(Command cycleCommand, CliMeta meta)
    {
        Command command = new("commit", "The sanctioned commit path: verify prerequisites + a fresh passing gate proof + a clean staged scope, then commit; refuse otherwise.");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<string> message = new("--message") { Description = "The commit message (the operator authors intent).", DefaultValueFactory = _ => "" };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(message);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti cycle commit",
            () => RunnerCommands.CycleCommit(meta, parseResult.GetValue(repo)!, parseResult.GetValue(message)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        cycleCommand.Subcommands.Add(command);
    }

    private static void AddPrecommitGuard(Command cycleCommand, CliMeta meta)
    {
        Command command = new("precommit-guard", "Insurance-hook guard: exit 0 if the sanctioned-commit sentinel is set, else redirect to `doti cycle commit`.");
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti cycle precommit-guard",
            () => RunnerCommands.PrecommitGuard(meta), forceJson: CliApp.ForceJson(parseResult, json)));
        cycleCommand.Subcommands.Add(command);
    }
}
