using System.CommandLine;
using Hx.Cli.Kernel;

namespace Hx.Runner.Cli;

internal static partial class RunnerCommandFactory
{
    private static void AddDoti(RootCommand rootCommand, CliMeta meta)
    {
        Command dotiCommand = new("doti", "Doti self-hosting: render skills, cycle state, hooks, operator-question protocol.");
        AddDotiRenderSkills(dotiCommand, meta);
        AddDotiInstall(dotiCommand, meta);
        AddDotiCycle(dotiCommand, meta);
        AddDotiInstallHooks(dotiCommand, meta);
        AddDotiQuestion(dotiCommand, meta);
        rootCommand.Subcommands.Add(dotiCommand);
    }

    private static void AddDotiRenderSkills(Command dotiCommand, CliMeta meta)
    {
        Command command = new("render-skills",
            "Render installed Codex/Claude skills + shared agent context from one source; --check reports drift (fail closed).");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<string> agents = new("--agents") { Description = "Comma-separated agents (codex,claude).", DefaultValueFactory = _ => "codex,claude" };
        Option<bool> check = new("--check") { Description = "Check for drift instead of writing.", DefaultValueFactory = _ => false };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(agents);
        command.Options.Add(check);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti render-skills",
            () => RunnerCommands.DotiRenderSkills(meta, parseResult.GetValue(repo)!,
                parseResult.GetValue(agents)!, parseResult.GetValue(check)),
            forceJson: CliApp.ForceJson(parseResult, json)));
        dotiCommand.Subcommands.Add(command);
    }

    private static void AddDotiInstall(Command dotiCommand, CliMeta meta)
    {
        Command command = new("install", "Install Doti assets (doti/ source + rendered skills + repo metadata) into a target repo.");
        Option<string> repo = new("--repo") { Description = "Target repository root to install into.", DefaultValueFactory = _ => "." };
        Option<string> agents = new("--agents") { Description = "Comma-separated agents (codex,claude).", DefaultValueFactory = _ => "codex,claude" };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(agents);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti install",
            () => RunnerCommands.DotiInstall(meta, parseResult.GetValue(repo)!, parseResult.GetValue(agents)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        dotiCommand.Subcommands.Add(command);
    }

    private static void AddDotiInstallHooks(Command dotiCommand, CliMeta meta)
    {
        Command command = new("install-hooks", "Install the doti insurance pre-commit hook into this repo's (untracked) git hooks dir.");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti install-hooks",
            () => RunnerCommands.InstallHooks(meta, parseResult.GetValue(repo)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        dotiCommand.Subcommands.Add(command);
    }

    private static void AddDotiQuestion(Command dotiCommand, CliMeta meta)
    {
        Command questionCommand = new("question", "Operator-question protocol tooling.");
        Command checkCommand = new("check", "Fail-closed: validate an OperatorQuestion JSON file against the protocol.");
        Option<string> file = new("--file") { Description = "Path to the OperatorQuestion JSON file.", DefaultValueFactory = _ => "" };
        Option<bool> json = CliApp.JsonOption();
        checkCommand.Options.Add(file);
        checkCommand.Options.Add(json);
        checkCommand.SetAction(parseResult => CliHost.Run(meta, "doti question check",
            () => RunnerCommands.QuestionCheck(meta, parseResult.GetValue(file)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        questionCommand.Subcommands.Add(checkCommand);
        dotiCommand.Subcommands.Add(questionCommand);
    }
}
