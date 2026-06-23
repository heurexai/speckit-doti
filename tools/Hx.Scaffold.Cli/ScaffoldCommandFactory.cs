using System.CommandLine;
using Hx.Cli.Kernel;

namespace Hx.Scaffold.Cli;

internal static class ScaffoldCommandFactory
{
    public static RootCommand Create(CliMeta meta)
    {
        RootCommand rootCommand = new("scaffold-dotnet generation CLI");
        AddProfile(rootCommand, meta);
        AddNew(rootCommand, meta);
        AddVersion(rootCommand, meta);
        AddUpdate(rootCommand, meta);
        AddPrereq(rootCommand, meta);
        CliApp.AddDescribe(rootCommand, meta, ErrorCodes.All);
        return rootCommand;
    }

    private static void AddProfile(RootCommand rootCommand, CliMeta meta)
    {
        Command command = new("profile", "Print the default scaffold profile.");
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "profile",
            () => ScaffoldCommands.Profile(meta), forceJson: CliApp.ForceJson(parseResult, json)));
        rootCommand.Subcommands.Add(command);
    }

    private static void AddNew(RootCommand rootCommand, CliMeta meta)
    {
        Command command = new(
            "new", "Generate a repo from the hx-dotnet-cli template, finish it (vendor tooling + Doti), and run the first smoke.");
        Option<string> name = new("--name") { Description = "Project/repo name.", DefaultValueFactory = _ => "" };
        Option<string> company = new("--company") { Description = "Company/owner.", DefaultValueFactory = _ => "Heurex" };
        Option<string> output = new("--output") { Description = "Output directory for the generated repo.", DefaultValueFactory = _ => "" };
        Option<string> profile = new("--profile") { Description = "Template profile.", DefaultValueFactory = _ => "dotnet-cli" };
        Option<string> agents = new("--agents") { Description = "Comma-separated agents (codex,claude).", DefaultValueFactory = _ => "codex,claude" };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(name);
        command.Options.Add(company);
        command.Options.Add(output);
        command.Options.Add(profile);
        command.Options.Add(agents);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.RunWithProgress(meta, "new",
            emit => ScaffoldCommands.New(
                meta,
                parseResult.GetValue(name)!,
                parseResult.GetValue(company)!,
                parseResult.GetValue(output)!,
                parseResult.GetValue(profile)!,
                parseResult.GetValue(agents)!,
                emit),
            forceJson: CliApp.ForceJson(parseResult, json)));
        rootCommand.Subcommands.Add(command);
    }

    private static void AddVersion(RootCommand rootCommand, CliMeta meta)
    {
        Command command = new("version", "Report the running hx version and target repo scaffold/Doti state.");
        Option<string> repo = new("--repo") { Description = "Repository root to inspect.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "version",
            () => ScaffoldCommands.Version(meta, parseResult.GetValue(repo)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        rootCommand.Subcommands.Add(command);
    }

    private static void AddUpdate(RootCommand rootCommand, CliMeta meta)
    {
        Command command = new("update", "Plan or run an existing-repo speckit-doti update.");
        Option<string> repo = new("--repo") { Description = "Repository root to update.", DefaultValueFactory = _ => "." };
        Option<bool> dryRun = new("--dry-run") { Description = "Report the update plan without mutating the target.", DefaultValueFactory = _ => false };
        Option<bool> force = new("--force") { Description = "Replace modified managed Doti assets after reporting them.", DefaultValueFactory = _ => false };
        Option<bool> noWorktree = new("--noworktree") { Description = "Disable the default backup worktree before replacement.", DefaultValueFactory = _ => false };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(dryRun);
        command.Options.Add(force);
        command.Options.Add(noWorktree);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "update",
            () => ScaffoldCommands.Update(
                meta,
                parseResult.GetValue(repo)!,
                parseResult.GetValue(dryRun),
                parseResult.GetValue(force),
                parseResult.GetValue(noWorktree),
                CliApp.ForceJson(parseResult, json) == true),
            forceJson: CliApp.ForceJson(parseResult, json)));
        rootCommand.Subcommands.Add(command);
    }

    private static void AddPrereq(RootCommand rootCommand, CliMeta meta)
    {
        Command group = new("prereq", "Check or install trusted system prerequisites.");
        AddPrereqCheck(group, meta);
        AddPrereqInstall(group, meta);
        rootCommand.Subcommands.Add(group);
    }

    private static void AddPrereqCheck(Command group, CliMeta meta)
    {
        Command command = new("check", "Check trusted prerequisites without installing anything.");
        Option<string> targetCommand = new("--for") { Description = "Command to check: new, update, version, or generated-validation.", DefaultValueFactory = _ => "new" };
        Option<string> repo = new("--repo") { Description = "Repository root for repo-aware checks.", DefaultValueFactory = _ => "." };
        Option<string> output = new("--output") { Description = "Output directory for new-solution checks.", DefaultValueFactory = _ => "" };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(targetCommand);
        command.Options.Add(repo);
        command.Options.Add(output);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "prereq check",
            () => ScaffoldCommands.PrereqCheck(
                meta,
                parseResult.GetValue(targetCommand)!,
                parseResult.GetValue(repo)!,
                parseResult.GetValue(output)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        group.Subcommands.Add(command);
    }

    private static void AddPrereqInstall(Command group, CliMeta meta)
    {
        Command command = new("install", "Install missing trusted prerequisites through an approved Windows winget plan.");
        Option<string> targetCommand = new("--for") { Description = "Command to unblock: new or update.", DefaultValueFactory = _ => "new" };
        Option<string> repo = new("--repo") { Description = "Repository root for repo-aware checks.", DefaultValueFactory = _ => "." };
        Option<string> output = new("--output") { Description = "Output directory for new-solution checks.", DefaultValueFactory = _ => "" };
        Option<string> confirmPlan = new("--confirm-plan") { Description = "Digest of the exact install plan approved by the operator.", DefaultValueFactory = _ => "" };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(targetCommand);
        command.Options.Add(repo);
        command.Options.Add(output);
        command.Options.Add(confirmPlan);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "prereq install",
            () => ScaffoldCommands.PrereqInstall(
                meta,
                parseResult.GetValue(targetCommand)!,
                parseResult.GetValue(repo)!,
                parseResult.GetValue(output)!,
                parseResult.GetValue(confirmPlan)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        group.Subcommands.Add(command);
    }
}
