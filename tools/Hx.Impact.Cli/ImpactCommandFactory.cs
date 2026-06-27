using System.CommandLine;
using Hx.Cli.Kernel;

namespace Hx.Impact.Cli;

/// <summary>
/// Builds the affected-test planner command tree (<c>bootstrap-plan</c>, <c>plan</c>) — extracted from the inline
/// <c>Program.cs</c> so it is composable: the standalone <c>hx-impact</c> CLI and the unified <c>hx</c> root
/// (007 FR-045) build the same tree.
/// </summary>
public static class ImpactCommandFactory
{
    /// <summary>Build a standalone root carrying the planner commands + a <c>describe</c>.</summary>
    public static RootCommand Create(CliMeta meta)
    {
        RootCommand rootCommand = new("scaffold-dotnet affected-change planner");
        AddPlannerCommands(rootCommand, meta);
        CliApp.AddDescribe(rootCommand, meta, ErrorCodes.All);
        return rootCommand;
    }

    /// <summary>Add the planner commands (no <c>describe</c>) to an existing command — used by the unified
    /// <c>hx impact</c> group (FR-045).</summary>
    public static void AddPlannerCommands(Command parent, CliMeta meta)
    {
        Command bootstrapCommand = new("bootstrap-plan", "Emit a placeholder full affected-change plan (smoke / bootstrap).");
        Option<bool> bootstrapJson = CliApp.JsonOption();
        bootstrapCommand.Options.Add(bootstrapJson);
        bootstrapCommand.SetAction(parseResult => CliHost.Run(meta, "bootstrap-plan",
            () => ImpactCommands.BootstrapPlan(meta),
            forceJson: CliApp.ForceJson(parseResult, bootstrapJson)));
        parent.Subcommands.Add(bootstrapCommand);

        Command planCommand = new("plan", "Emit the deterministic affected-change plan for a change set.");
        Option<string> planRepo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<string> planBase = new("--base") { Description = "Base ref (default HEAD = working-tree changes only).", DefaultValueFactory = _ => "HEAD" };
        Option<string> planHead = new("--head") { Description = "Head ref.", DefaultValueFactory = _ => "HEAD" };
        Option<string> planConfiguration = new("--configuration") { Description = "Build configuration for the emitted test commands.", DefaultValueFactory = _ => "Release" };
        Option<string> planFor = new("--for")
        {
            Description = "Audience: 'tests' (default — affected test scope), 'arch-review' (changed-files context for /06), or 'change-context' (status-rich change set for /08-drift-review).",
            DefaultValueFactory = _ => ImpactCommands.AudienceTests,
        };
        planFor.AcceptOnlyFromAmong(
            ImpactCommands.AudienceTests, ImpactCommands.AudienceArchReview, ImpactCommands.AudienceChangeContext);
        Option<bool> planJson = CliApp.JsonOption();
        planCommand.Options.Add(planRepo);
        planCommand.Options.Add(planBase);
        planCommand.Options.Add(planHead);
        planCommand.Options.Add(planConfiguration);
        planCommand.Options.Add(planFor);
        planCommand.Options.Add(planJson);
        planCommand.SetAction(parseResult => CliHost.Run(meta, "plan",
            () => ImpactCommands.Plan(
                meta,
                parseResult.GetValue(planRepo)!,
                parseResult.GetValue(planBase)!,
                parseResult.GetValue(planHead)!,
                parseResult.GetValue(planConfiguration)!,
                parseResult.GetValue(planFor)!),
            forceJson: CliApp.ForceJson(parseResult, planJson)));
        parent.Subcommands.Add(planCommand);
    }
}
