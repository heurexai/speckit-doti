using System.CommandLine;
using System.Reflection;
using Hx.Cli.Kernel;
using Hx.Impact.Cli;

CliMeta meta = new("hx-impact", CliApp.ResolveVersion(Assembly.GetExecutingAssembly()));

RootCommand rootCommand = new("scaffold-dotnet affected-change planner");

// ---- bootstrap-plan ----
Command bootstrapCommand = new("bootstrap-plan", "Emit a placeholder full affected-change plan (smoke / bootstrap).");
Option<bool> bootstrapJson = CliApp.JsonOption();
bootstrapCommand.Options.Add(bootstrapJson);
bootstrapCommand.SetAction(parseResult => CliHost.Run(meta, "bootstrap-plan",
    () => ImpactCommands.BootstrapPlan(meta),
    forceJson: CliApp.ForceJson(parseResult, bootstrapJson)));
rootCommand.Subcommands.Add(bootstrapCommand);

// ---- plan ----
Command planCommand = new("plan", "Emit the deterministic affected-test plan for a change set.");
Option<string> planRepo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
Option<string> planBase = new("--base") { Description = "Base ref (default HEAD = working-tree changes only).", DefaultValueFactory = _ => "HEAD" };
Option<string> planHead = new("--head") { Description = "Head ref.", DefaultValueFactory = _ => "HEAD" };
Option<string> planConfiguration = new("--configuration") { Description = "Build configuration for the emitted test commands.", DefaultValueFactory = _ => "Release" };
Option<bool> planJson = CliApp.JsonOption();
planCommand.Options.Add(planRepo);
planCommand.Options.Add(planBase);
planCommand.Options.Add(planHead);
planCommand.Options.Add(planConfiguration);
planCommand.Options.Add(planJson);
planCommand.SetAction(parseResult => CliHost.Run(meta, "plan",
    () => ImpactCommands.Plan(
        meta,
        parseResult.GetValue(planRepo)!,
        parseResult.GetValue(planBase)!,
        parseResult.GetValue(planHead)!,
        parseResult.GetValue(planConfiguration)!),
    forceJson: CliApp.ForceJson(parseResult, planJson)));
rootCommand.Subcommands.Add(planCommand);

// ---- describe ----
CliApp.AddDescribe(rootCommand, meta, ErrorCodes.All);

return rootCommand.Parse(args).Invoke();
