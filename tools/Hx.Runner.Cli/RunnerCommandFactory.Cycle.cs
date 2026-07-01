using System.CommandLine;
using Hx.Cli.Kernel;

namespace Hx.Runner.Cli;

public static partial class RunnerCommandFactory
{
    private static void AddDotiCycle(Command dotiCommand, CliMeta meta)
    {
        Command cycleCommand = new("cycle", "Doti cycle state: stamp diff-bound stage proofs, report freshness, and enforce the chokepoints.");
        AddCycleStamp(cycleCommand, meta);
        AddCycleStatus(cycleCommand, meta);
        AddCycleCheck(cycleCommand, meta);
        AddCycleRefreshPlan(cycleCommand, meta);
        AddCycleRefresh(cycleCommand, meta);
        AddCycleReviewRebind(cycleCommand, meta);
        AddCycleFinalizeRelease(cycleCommand, meta);
        dotiCommand.Subcommands.Add(cycleCommand);
    }

    // 039 WI4/FR-032: finalize a cycle wedged at the release stage so the next feature can start.
    private static void AddCycleFinalizeRelease(Command cycleCommand, CliMeta meta)
    {
        Command command = new("finalize-release",
            "Finalize a cycle wedged at the release stage (e.g. published via dev->main->CI): move the released feature into ReleasedCycles so the next specify can start. Idempotent; fail-closed unless a release tag exists.");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti cycle finalize-release",
            () => RunnerCommands.CycleFinalizeRelease(meta, parseResult.GetValue(repo)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        cycleCommand.Subcommands.Add(command);
    }

    private static void AddCycleReviewRebind(Command cycleCommand, CliMeta meta)
    {
        Command command = new("review-rebind",
            "Record an agent-gated reviewed-no-impact rebind: after reading the surfaced upstream diff, attest that an upstream content change does not affect the target stage; the engine rebinds ONLY that stage's prerequisite content. The decision is the agent's; clearing the flag without assessing impact is forbidden.");
        Option<string> target = new("--target") { Description = "Stage id stale solely on a prerequisite content change.", DefaultValueFactory = _ => "" };
        Option<string> attest = new("--attest") { Description = "The reviewed verdict (no-impact).", DefaultValueFactory = _ => "" };
        Option<string> reason = new("--reason") { Description = "Optional free-text reason recorded in the audit record.", DefaultValueFactory = _ => "" };
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(target);
        command.Options.Add(attest);
        command.Options.Add(reason);
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti cycle review-rebind",
            () => RunnerCommands.CycleReviewRebind(meta, parseResult.GetValue(repo)!, parseResult.GetValue(target)!,
                parseResult.GetValue(attest)!, parseResult.GetValue(reason)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        cycleCommand.Subcommands.Add(command);
    }

    private static void AddCycleRefreshPlan(Command cycleCommand, CliMeta meta)
    {
        Command command = new("refresh-plan", "Read-only: show how to recover a target stage's freshness (what is stale, why, and the next command).");
        Option<string> target = new("--target") { Description = "Stage id to recover (its stale prerequisites are planned).", DefaultValueFactory = _ => "" };
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(target);
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti cycle refresh-plan",
            () => RunnerCommands.CycleRefreshPlan(meta, parseResult.GetValue(repo)!, parseResult.GetValue(target)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        cycleCommand.Subcommands.Add(command);
    }

    private static void AddCycleRefresh(Command cycleCommand, CliMeta meta)
    {
        Command command = new("refresh", "Recover a target stage: with --apply-safe, re-stamp only the safe-to-reinterpret stale prerequisites (runner/binding migrations); a real input change still requires re-running the stage.");
        Option<string> target = new("--target") { Description = "Stage id to recover.", DefaultValueFactory = _ => "" };
        Option<bool> applySafe = new("--apply-safe") { Description = "Re-stamp the SafeReinterpret stale prerequisites (the only mutating refresh path). Without it, refresh is a dry run.", DefaultValueFactory = _ => false };
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(target);
        command.Options.Add(applySafe);
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti cycle refresh",
            () => RunnerCommands.CycleRefresh(meta, parseResult.GetValue(repo)!, parseResult.GetValue(target)!, parseResult.GetValue(applySafe)),
            forceJson: CliApp.ForceJson(parseResult, json)));
        cycleCommand.Subcommands.Add(command);
    }

    private static void AddCycleStamp(Command cycleCommand, CliMeta meta)
    {
        Command command = new("stamp", "Record a stage's diff-bound proof and advance the cycle state.");
        Option<string> stage = new("--stage") { Description = "Stage id (specify, clarify, plan, tasks, analyze, arch-review, implement, drift-review, release).", DefaultValueFactory = _ => "" };
        Option<string> feature = new("--feature") { Description = "Numbered feature slug (required on the first stamp; e.g. 001-doti-cycle-state).", DefaultValueFactory = _ => "" };
        Option<string> baseRef = new("--base") { Description = "Base ref for the change-set identity (default: dev if it resolves, else HEAD).", DefaultValueFactory = _ => "" };
        Option<string> releaseIntent = new("--release-intent") { Description = "Release intent for the release-stage transition: major, minor, or patch. Adds the matching GitVersion +semver signal to the automatic transition commit. Default (blank) for a feature cycle is minor (FR-044); an explicit value overrides.", DefaultValueFactory = _ => "" };
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(stage);
        command.Options.Add(feature);
        command.Options.Add(baseRef);
        command.Options.Add(releaseIntent);
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti cycle stamp",
            () => RunnerCommands.CycleStamp(meta, parseResult.GetValue(repo)!, parseResult.GetValue(stage)!,
                parseResult.GetValue(feature)!, parseResult.GetValue(baseRef)!, parseResult.GetValue(releaseIntent)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        cycleCommand.Subcommands.Add(command);
    }

    private static void AddCycleStatus(Command cycleCommand, CliMeta meta)
    {
        Command command = new("status", "Report the cycle state with a freshness verdict per stamped stage (non-enforcing).");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti cycle status",
            () => RunnerCommands.CycleStatus(meta, parseResult.GetValue(repo)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        cycleCommand.Subcommands.Add(command);
    }

    private static void AddCycleCheck(Command cycleCommand, CliMeta meta)
    {
        Command command = new("check", "Fail-closed: verify a stage's prerequisites are all stamped + fresh + valid.");
        Option<string> stage = new("--stage") { Description = "Stage id whose prerequisites to verify.", DefaultValueFactory = _ => "" };
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(stage);
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "doti cycle check",
            () => RunnerCommands.CycleCheck(meta, parseResult.GetValue(repo)!, parseResult.GetValue(stage)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        cycleCommand.Subcommands.Add(command);
    }

}
