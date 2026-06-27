using System.CommandLine;
using Hx.Cli.Kernel;

namespace Hx.Runner.Cli;

public static partial class RunnerCommandFactory
{
    private static void AddArchitecture(RootCommand rootCommand, CliMeta meta)
    {
        Command architectureCommand = new("architecture", "Architecture gate: run the rule families.");
        Command command = new("test", "Run the repo's ArchUnitNET architecture families and emit a per-test proof.");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "architecture test",
            () => RunnerCommands.ArchitectureTest(meta, parseResult.GetValue(repo)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        architectureCommand.Subcommands.Add(command);
        rootCommand.Subcommands.Add(architectureCommand);
    }

    private static void AddGate(RootCommand rootCommand, CliMeta meta)
    {
        Command gateCommand = new("gate", "Run the deterministic gate ladder and emit one aggregated proof.");
        Command runCommand = new("run", "Run the gate ladder for a lane and emit a fail-closed GateProof.");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<string> profile = new("--profile") { Description = "Lane profile: auto|advisory|normal|release.", DefaultValueFactory = _ => "auto" };
        Option<bool> stream = new("--stream") { Description = "Stream NDJSON phase events as the ladder runs (JSON sink); the final envelope carries the gate trace.", DefaultValueFactory = _ => false };
        Option<bool> json = CliApp.JsonOption();
        runCommand.Options.Add(repo);
        runCommand.Options.Add(profile);
        runCommand.Options.Add(stream);
        runCommand.Options.Add(json);
        // 012 (WI-3 + WI-5): the human TTY gets the outcome-aware live-progress bars + the gate-trace summary panel;
        // JSON (incl. --stream) gets the NDJSON phases + the final envelope with GateRunResult.Trace.
        runCommand.SetAction(parseResult => CliHost.RunStreamingWithProgress(meta, "gate run",
            emit => RunnerCommands.GateRun(meta, parseResult.GetValue(repo)!,
                parseResult.GetValue(profile)!, emit),
            forceJson: CliApp.ForceJson(parseResult, json),
            streamEvents: parseResult.GetValue(stream)));
        gateCommand.Subcommands.Add(runCommand);
        rootCommand.Subcommands.Add(gateCommand);
    }

    private static void AddVersion(RootCommand rootCommand, CliMeta meta)
    {
        Command versionCommand = new("version", "GitVersion-backed version calculation.");
        AddVersionCalculate(versionCommand, meta);
        rootCommand.Subcommands.Add(versionCommand);
    }

    private static void AddVersionCalculate(Command versionCommand, CliMeta meta)
    {
        Command command = new("calculate", "Compute the version via the vendored GitVersion CLI (fail closed if unvendored).");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "version calculate",
            () => RunnerCommands.VersionCalculate(meta, parseResult.GetValue(repo)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        versionCommand.Subcommands.Add(command);
    }

    private static void AddSecurity(RootCommand rootCommand, CliMeta meta)
    {
        Command securityCommand = new("security", "Security gate: package-vulnerability scan + analyzer-enforced SAST status.");
        Command command = new("scan", "Run the package-vulnerability SCA + report SAST enforcement; fail closed on findings >= the policy floor.");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "security scan",
            () => RunnerCommands.SecurityScan(meta, parseResult.GetValue(repo)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        securityCommand.Subcommands.Add(command);
        securityCommand.Subcommands.Add(UrlCheckCommand(meta));
        rootCommand.Subcommands.Add(securityCommand);
    }

    // 007 T034 (FR-035 / SC-014): SSRF-resistant validation of a URL a command intends to ingest.
    private static Command UrlCheckCommand(CliMeta meta)
    {
        Command command = new("url-check",
            "Validate a URL for SSRF-resistant ingestion (https-only + host allowlist + no private/reserved resolution); fail closed.");
        Option<string> url = new("--url") { Description = "The URL to validate.", DefaultValueFactory = _ => "" };
        Option<string> allow = new("--allow") { Description = "Comma-separated host allowlist.", DefaultValueFactory = _ => "" };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(url);
        command.Options.Add(allow);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "security url-check",
            () => RunnerCommands.SecurityUrlCheck(meta, parseResult.GetValue(url)!, parseResult.GetValue(allow)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        return command;
    }
}
