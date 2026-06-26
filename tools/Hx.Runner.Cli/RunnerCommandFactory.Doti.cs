using System.CommandLine;
using Hx.Cli.Kernel;

namespace Hx.Runner.Cli;

public static partial class RunnerCommandFactory
{
    private static void AddDoti(RootCommand rootCommand, CliMeta meta)
    {
        Command dotiCommand = new("doti", "Doti self-hosting: render skills, cycle state, hooks, operator-question protocol.");
        AddDotiRenderSkills(dotiCommand, meta);
        AddDotiInstall(dotiCommand, meta);
        AddDotiPayload(dotiCommand, meta);
        AddDotiCycle(dotiCommand, meta);
        AddDotiTaskHash(dotiCommand, meta);
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

    private static void AddDotiPayload(Command dotiCommand, CliMeta meta)
    {
        Command payloadCommand = new("payload", "Doti payload parity tooling.");
        Command checkCommand = new("check", "Check scaffold-installed Doti payload parity against this repo's source assets.");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        checkCommand.Options.Add(repo);
        checkCommand.Options.Add(json);
        checkCommand.SetAction(parseResult => CliHost.Run(meta, "doti payload check",
            () => RunnerCommands.DotiPayloadCheck(meta, parseResult.GetValue(repo)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        payloadCommand.Subcommands.Add(checkCommand);
        dotiCommand.Subcommands.Add(payloadCommand);
    }

    private static void AddDotiInstall(Command dotiCommand, CliMeta meta)
    {
        Command command = new("install", "Install, repair, migrate, or update Doti workflow assets in a --repo target (version-aware reconciliation; operator edits preserved).");
        Option<string?> repo = new("--repo") { Description = "Target repository root to install into. Required; the command never defaults to the current directory." };
        Option<string> agents = new("--agents") { Description = "Comma-separated agents (codex,claude).", DefaultValueFactory = _ => "codex,claude" };
        Option<bool> force = new("--force") { Description = "Replace modified/unknown legacy Doti-owned assets during migration." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(agents);
        command.Options.Add(force);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti install",
            () => RunnerCommands.DotiInstall(meta, parseResult.GetValue(repo), parseResult.GetValue(agents)!, parseResult.GetValue(force)),
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

    private static void AddDotiTaskHash(Command dotiCommand, CliMeta meta)
    {
        Command taskHashCommand = new("task-hash", "Task-completion hash tooling.");
        Command stampCommand = new("stamp", "Stamp canonical doti-task-hash markers for checked tasks; refuse unchecked tasks.");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<string> feature = new("--feature") { Description = "Numbered feature slug. Defaults to the active Doti cycle feature.", DefaultValueFactory = _ => "" };
        Option<bool> json = CliApp.JsonOption();
        stampCommand.Options.Add(repo);
        stampCommand.Options.Add(feature);
        stampCommand.Options.Add(json);
        stampCommand.SetAction(parseResult => CliHost.Run(meta, "doti task-hash stamp",
            () => RunnerCommands.TaskHashStamp(meta, parseResult.GetValue(repo)!, parseResult.GetValue(feature)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        taskHashCommand.Subcommands.Add(stampCommand);
        dotiCommand.Subcommands.Add(taskHashCommand);
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
