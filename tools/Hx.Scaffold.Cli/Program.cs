using System.CommandLine;
using System.Reflection;
using Hx.Cli.Kernel;
using Hx.Scaffold.Cli;

CliMeta meta = new("hx-scaffold", CliApp.ResolveVersion(Assembly.GetExecutingAssembly()));

RootCommand rootCommand = new("scaffold-dotnet generation CLI");

// ---- profile ----
Command profileCommand = new("profile", "Print the default scaffold profile.");
Option<bool> profileJson = CliApp.JsonOption();
profileCommand.Options.Add(profileJson);
profileCommand.SetAction(parseResult => CliHost.Run(meta, "profile",
    () => ScaffoldCommands.Profile(meta), forceJson: CliApp.ForceJson(parseResult, profileJson)));
rootCommand.Subcommands.Add(profileCommand);

// ---- new ----
Command newCommand = new(
    "new", "Generate a repo from the hx-dotnet-cli template, finish it (vendor tooling + Doti), and run the first smoke.");
Option<string> nameOption = new("--name") { Description = "Project/repo name.", DefaultValueFactory = _ => "" };
Option<string> companyOption = new("--company") { Description = "Company/owner.", DefaultValueFactory = _ => "Heurex" };
Option<string> outputOption = new("--output") { Description = "Output directory for the generated repo.", DefaultValueFactory = _ => "" };
Option<string> profileOption = new("--profile") { Description = "Template profile.", DefaultValueFactory = _ => "dotnet-cli" };
Option<string> agentsOption = new("--agents") { Description = "Comma-separated agents (codex,claude).", DefaultValueFactory = _ => "codex,claude" };
Option<bool> newJson = CliApp.JsonOption();
newCommand.Options.Add(nameOption);
newCommand.Options.Add(companyOption);
newCommand.Options.Add(outputOption);
newCommand.Options.Add(profileOption);
newCommand.Options.Add(agentsOption);
newCommand.Options.Add(newJson);
newCommand.SetAction(parseResult => CliHost.RunWithProgress(meta, "new",
    emit => ScaffoldCommands.New(
        meta,
        parseResult.GetValue(nameOption)!,
        parseResult.GetValue(companyOption)!,
        parseResult.GetValue(outputOption)!,
        parseResult.GetValue(profileOption)!,
        parseResult.GetValue(agentsOption)!,
        emit),
    forceJson: CliApp.ForceJson(parseResult, newJson)));
rootCommand.Subcommands.Add(newCommand);

// ---- describe ----
CliApp.AddDescribe(rootCommand, meta, ErrorCodes.All);

return CliApp.Invoke(rootCommand, meta, args, "speckit-doti",
    "Agentic .NET spec-driven development starter kit");
