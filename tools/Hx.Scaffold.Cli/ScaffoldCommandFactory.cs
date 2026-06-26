using System.CommandLine;
using Hx.Cli.Kernel;
using Hx.Scaffold.Core;
using Hx.Scaffold.Core.Configuration;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Cli;

public static class ScaffoldCommandFactory
{
    public static RootCommand Create(
        CliMeta meta,
        string? executableDirectory = null,
        DistributionChannelInfo? channel = null,
        CliDescribeTier? tier = null)
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
        ComposeWorkflowSurface(rootCommand, meta); // FR-045
        // 007 T045 (FR-022/FR-042): describe surfaces the active channel + tier/gate-ladder. Resolved by the caller
        // (Program.cs) so the same values feed the human help header; fall back to a fresh resolve for direct callers.
        CliApp.AddDescribe(rootCommand, meta, ErrorCodes.All,
            channel: channel ?? InstalledPayload.ResolveChannel(),
            tier: tier ?? InstalledPayload.ResolveTier(Directory.GetCurrentDirectory()));
        return rootCommand;
    }

    // FR-045: the installed hx exposes the runner + impact workflow command surface source-free. Build those
    // trees (with the hx meta so output stays consistent) and graft them into the hx root, reconciling the two
    // overlaps: the runner's `doti` subcommands merge into hx's `doti` group (hx's payload `install` wins), and
    // the runner's `version calculate` joins hx's `version` (which keeps its repo-report action). The runner's
    // own `describe` is dropped (hx provides its own). The impact planner becomes the `hx impact` group.
    private static void ComposeWorkflowSurface(RootCommand hxRoot, CliMeta meta)
    {
        RootCommand runner = Hx.Runner.Cli.RunnerCommandFactory.Create(meta);
        Command? hxDoti = hxRoot.Subcommands.FirstOrDefault(c => c.Name == "doti");
        Command? hxVersion = hxRoot.Subcommands.FirstOrDefault(c => c.Name == "version");

        foreach (Command sub in runner.Subcommands.ToList())
        {
            runner.Subcommands.Remove(sub);
            switch (sub.Name)
            {
                case "describe":
                    break;
                case "doti" when hxDoti is not null:
                    MergeSubcommands(sub, hxDoti);
                    break;
                case "version" when hxVersion is not null:
                    MergeSubcommands(sub, hxVersion);
                    break;
                default:
                    hxRoot.Subcommands.Add(sub);
                    break;
            }
        }

        Command impact = new("impact", "Deterministic affected-test planning.");
        Hx.Impact.Cli.ImpactCommandFactory.AddPlannerCommands(impact, meta);
        hxRoot.Subcommands.Add(impact);
    }

    private static void MergeSubcommands(Command source, Command target)
    {
        foreach (Command child in source.Subcommands.ToList())
        {
            source.Subcommands.Remove(child);
            if (target.Subcommands.All(c => !string.Equals(c.Name, child.Name, StringComparison.OrdinalIgnoreCase)))
            {
                target.Subcommands.Add(child);
            }
        }
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
            "Build the manifest-declared target release, create/verify the local GitVersion tag, and copy the channel package artifacts when configured.");
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
        Command group = new("doti", "Install, repair, migrate, or update Doti repo workflow assets from this installed hx payload.");
        AddDotiInstall(group, meta, configurationDirectory);
        AddDotiPayloadManifest(group, meta); // 007 T023 — pack-pipeline payload-descriptor generator
        rootCommand.Subcommands.Add(group);
    }

    private static void AddDotiPayloadManifest(Command group, CliMeta meta)
    {
        Command command = new("payload-manifest",
            "Generate payload.manifest.json (the source-free PayloadDescriptor) for a staged payload root — a pack/release build tool (007 T023).");
        Option<string> root = new("--root") { Description = "Staged payload root to hash + describe." };
        Option<string> payloadVersion = new("--payload-version") { Description = "Payload version stamped into the descriptor.", DefaultValueFactory = _ => "0.0.0" };
        Option<string> toolVersion = new("--tool-version") { Description = "Tool version stamped into the descriptor.", DefaultValueFactory = _ => "0.0.0" };
        Option<string> channel = new("--channel") { Description = "Distribution channel id.", DefaultValueFactory = _ => DistributionChannelId.GlobalTool };
        Option<string> mode = new("--mode") { Description = "Command mode.", DefaultValueFactory = _ => CommandMode.Installed };
        Option<string> digestOut = new("--digest-out") { Description = "Optional path to write the manifest's anti-substitution digest (FR-003 anchor) so the pack step can embed it in the executable.", DefaultValueFactory = _ => "" };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(root);
        command.Options.Add(payloadVersion);
        command.Options.Add(toolVersion);
        command.Options.Add(channel);
        command.Options.Add(mode);
        command.Options.Add(digestOut);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti payload-manifest",
            () => ScaffoldCommands.PayloadManifest(
                meta,
                parseResult.GetValue(root)!,
                parseResult.GetValue(payloadVersion)!,
                parseResult.GetValue(toolVersion)!,
                parseResult.GetValue(channel)!,
                parseResult.GetValue(mode)!,
                parseResult.GetValue(digestOut)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        group.Subcommands.Add(command);
    }

    private static void AddDotiInstall(Command group, CliMeta meta, string configurationDirectory)
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
