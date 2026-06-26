using System.CommandLine;
using Hx.Cli.Kernel;
using Hx.Scaffold.Core;
using Hx.Scaffold.Core.Configuration;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Cli;

public static class ScaffoldCommandFactory
{
    public static RootCommand Create(CliMeta meta, string? executableDirectory = null)
    {
        string configurationDirectory = string.IsNullOrWhiteSpace(executableDirectory)
            ? AppContext.BaseDirectory
            : executableDirectory;
        RootCommand rootCommand = new("scaffold-dotnet generation CLI");
        AddProfile(rootCommand, meta, configurationDirectory);
        AddNew(rootCommand, meta, configurationDirectory);
        AddVersion(rootCommand, meta, configurationDirectory);
        AddRelease(rootCommand, meta, configurationDirectory);
        AddPrereq(rootCommand, meta, configurationDirectory);
        AddDoti(rootCommand, meta, configurationDirectory);
        CliApp.AddDescribe(rootCommand, meta, ErrorCodes.All, channel: InstalledPayload.ResolveChannel());
        return rootCommand;
    }

    private static void AddProfile(RootCommand rootCommand, CliMeta meta, string configurationDirectory)
    {
        Command command = new("profile", "Print the default scaffold profile.");
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "profile",
            () => WithRequiredConfiguration(meta, "profile", configurationDirectory, _ => ScaffoldCommands.Profile(meta)),
            forceJson: CliApp.ForceJson(parseResult, json)));
        rootCommand.Subcommands.Add(command);
    }

    private static void AddNew(RootCommand rootCommand, CliMeta meta, string configurationDirectory)
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
            emit => WithRequiredConfiguration(meta, "new", configurationDirectory,
                _ => ScaffoldCommands.New(
                    meta,
                    parseResult.GetValue(name)!,
                    parseResult.GetValue(company)!,
                    parseResult.GetValue(output)!,
                    parseResult.GetValue(profile)!,
                    parseResult.GetValue(agents)!,
                    emit)),
            forceJson: CliApp.ForceJson(parseResult, json)));
        rootCommand.Subcommands.Add(command);
    }

    private static void AddVersion(RootCommand rootCommand, CliMeta meta, string configurationDirectory)
    {
        Command command = new("version", "Report the running hx version and target repo scaffold/Doti state.");
        Option<string> repo = new("--repo") { Description = "Repository root to inspect.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "version",
            () => WithRequiredConfiguration(meta, "version", configurationDirectory,
                _ => ScaffoldCommands.Version(meta, parseResult.GetValue(repo)!)),
            forceJson: CliApp.ForceJson(parseResult, json)));
        rootCommand.Subcommands.Add(command);
    }

    private static void AddRelease(RootCommand rootCommand, CliMeta meta, string configurationDirectory)
    {
        Command command = new("release",
            "Build the manifest-declared target release, create/verify the local GitVersion tag, and copy Velopack artifacts when configured.");
        Option<string> repo = new("--repo") { Description = "Repository root to release.", DefaultValueFactory = _ => "." };
        Option<string> rid = new("--rid") { Description = "Runtime identifier to publish (defaults to the current host RID).", DefaultValueFactory = _ => "" };
        Option<bool> major = new("--major") { Description = "Require GitVersion to calculate a major release before tagging.", DefaultValueFactory = _ => false };
        Option<bool> minor = new("--minor") { Description = "Require GitVersion to calculate a minor release before tagging.", DefaultValueFactory = _ => false };
        Option<bool> patch = new("--patch") { Description = "Require GitVersion to calculate a patch release before tagging (default).", DefaultValueFactory = _ => false };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(rid);
        command.Options.Add(major);
        command.Options.Add(minor);
        command.Options.Add(patch);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "release",
            () => WithRequiredConfiguration(meta, "release", configurationDirectory,
                configuration => ScaffoldCommands.Release(
                    meta,
                    parseResult.GetValue(repo)!,
                    parseResult.GetValue(rid)!,
                    configuration,
                    parseResult.GetValue(major),
                    parseResult.GetValue(minor),
                    parseResult.GetValue(patch))),
            forceJson: CliApp.ForceJson(parseResult, json)));
        rootCommand.Subcommands.Add(command);
    }

    private static void AddPrereq(RootCommand rootCommand, CliMeta meta, string configurationDirectory)
    {
        Command group = new("prereq", "Check or install trusted system prerequisites.");
        AddPrereqCheck(group, meta, configurationDirectory);
        AddPrereqInstall(group, meta, configurationDirectory);
        rootCommand.Subcommands.Add(group);
    }

    private static void AddDoti(RootCommand rootCommand, CliMeta meta, string configurationDirectory)
    {
        Command group = new("doti", "Install or repair Doti repo workflow assets from this installed hx payload.");
        AddDotiInstall(group, meta, configurationDirectory);
        rootCommand.Subcommands.Add(group);
    }

    private static void AddDotiInstall(Command group, CliMeta meta, string configurationDirectory)
    {
        Command command = new("install", "Install or repair .doti workflow assets into an explicit target repo.");
        Option<string?> repo = new("--repo") { Description = "Target repository root to install into. Required; the command never defaults to the current directory." };
        Option<string> agents = new("--agents") { Description = "Comma-separated agents (codex,claude).", DefaultValueFactory = _ => "codex,claude" };
        Option<bool> force = new("--force") { Description = "Replace modified/unknown legacy Doti-owned assets during migration." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(agents);
        command.Options.Add(force);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti install",
            () => WithRequiredConfiguration(meta, "doti install", configurationDirectory,
                _ => ScaffoldCommands.DotiInstall(
                    meta,
                    parseResult.GetValue(repo),
                    parseResult.GetValue(agents)!,
                    parseResult.GetValue(force),
                    configurationDirectory)),
            forceJson: CliApp.ForceJson(parseResult, json)));
        group.Subcommands.Add(command);
    }

    private static void AddPrereqCheck(Command group, CliMeta meta, string configurationDirectory)
    {
        Command command = new("check", "Check trusted prerequisites without installing anything.");
        Option<string> targetCommand = new("--for") { Description = "Command to check: new, version, or generated-validation.", DefaultValueFactory = _ => "new" };
        Option<string> repo = new("--repo") { Description = "Repository root for repo-aware checks.", DefaultValueFactory = _ => "." };
        Option<string> output = new("--output") { Description = "Output directory for new-solution checks.", DefaultValueFactory = _ => "" };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(targetCommand);
        command.Options.Add(repo);
        command.Options.Add(output);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "prereq check",
            () => WithRequiredConfiguration(meta, "prereq check", configurationDirectory,
                _ => ScaffoldCommands.PrereqCheck(
                    meta,
                    parseResult.GetValue(targetCommand)!,
                    parseResult.GetValue(repo)!,
                    parseResult.GetValue(output)!)),
            forceJson: CliApp.ForceJson(parseResult, json)));
        group.Subcommands.Add(command);
    }

    private static void AddPrereqInstall(Command group, CliMeta meta, string configurationDirectory)
    {
        Command command = new("install", "Install missing trusted prerequisites through an approved Windows winget plan.");
        Option<string> targetCommand = new("--for") { Description = "Command to unblock: new.", DefaultValueFactory = _ => "new" };
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
            () => WithRequiredConfiguration(meta, "prereq install", configurationDirectory,
                _ => ScaffoldCommands.PrereqInstall(
                    meta,
                    parseResult.GetValue(targetCommand)!,
                    parseResult.GetValue(repo)!,
                    parseResult.GetValue(output)!,
                    parseResult.GetValue(confirmPlan)!)),
            forceJson: CliApp.ForceJson(parseResult, json)));
        group.Subcommands.Add(command);
    }

    private static CliResult WithRequiredConfiguration(
        CliMeta meta,
        string command,
        string configurationDirectory,
        Func<HxLocalConfiguration, CliResult> action)
    {
        try
        {
            return action(HxLocalConfigurationLoader.LoadRequired(configurationDirectory));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return CliResults.Fail(meta, command, ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, ex.Message)]);
        }
    }
}
