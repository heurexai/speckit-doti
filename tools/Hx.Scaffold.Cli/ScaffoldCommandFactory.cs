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
        AddRelease(rootCommand, meta);
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

    private static void AddRelease(RootCommand rootCommand, CliMeta meta)
    {
        Command command = new("release",
            "Build the manifest-declared target release, create/verify the local GitVersion tag, and copy Velopack artifacts when configured.");
        Option<string> repo = new("--repo") { Description = "Repository root to release.", DefaultValueFactory = _ => "." };
        Option<string> rid = new("--rid") { Description = "Runtime identifier to publish (defaults to the current host RID).", DefaultValueFactory = _ => "" };
        Option<string> releaseRoot = new("--release-root") { Description = "Explicit local release root. Overrides environment lookup.", DefaultValueFactory = _ => "" };
        Option<string> releaseRootEnv = new("--release-root-env") { Description = "Environment variable name for the local release root (overrides the target manifest default).", DefaultValueFactory = _ => "" };
        Option<bool> saveReleaseRoot = new("--save-release-root") { Description = "Persist --release-root into the manifest default release-root variable or --release-root-env for future runs.", DefaultValueFactory = _ => false };
        Option<bool> major = new("--major") { Description = "Require GitVersion to calculate a major release before tagging.", DefaultValueFactory = _ => false };
        Option<bool> minor = new("--minor") { Description = "Require GitVersion to calculate a minor release before tagging.", DefaultValueFactory = _ => false };
        Option<bool> patch = new("--patch") { Description = "Require GitVersion to calculate a patch release before tagging (default).", DefaultValueFactory = _ => false };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(rid);
        command.Options.Add(releaseRoot);
        command.Options.Add(releaseRootEnv);
        command.Options.Add(saveReleaseRoot);
        command.Options.Add(major);
        command.Options.Add(minor);
        command.Options.Add(patch);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "release",
            () => ScaffoldCommands.Release(
                meta,
                parseResult.GetValue(repo)!,
                parseResult.GetValue(rid)!,
                parseResult.GetValue(releaseRoot)!,
                parseResult.GetValue(releaseRootEnv)!,
                parseResult.GetValue(saveReleaseRoot),
                parseResult.GetValue(major),
                parseResult.GetValue(minor),
                parseResult.GetValue(patch)),
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
        Option<string> targetCommand = new("--for") { Description = "Command to check: new, version, or generated-validation.", DefaultValueFactory = _ => "new" };
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
