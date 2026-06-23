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
}
