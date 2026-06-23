using System.CommandLine;
using Hx.Cli.Kernel;

namespace Hx.Runner.Cli;

internal static partial class RunnerCommandFactory
{
    private static void AddHygiene(RootCommand rootCommand, CliMeta meta)
    {
        Command hygieneCommand = new("hygiene", "Public-release hygiene scanning.");
        AddHygieneScan(hygieneCommand, meta);
        AddGitleaks(hygieneCommand, meta);
        rootCommand.Subcommands.Add(hygieneCommand);
    }

    private static void AddHygieneScan(Command hygieneCommand, CliMeta meta)
    {
        Command scanCommand = new("scan", "Run a public hygiene scan (changed-file by default).");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<string> scope = new("--scope") { Description = "changed | all", DefaultValueFactory = _ => "changed" };
        Option<string> source = new("--source") { Description = "staged | range", DefaultValueFactory = _ => "staged" };
        Option<string?> scanBase = new("--base") { Description = "Base ref for a range scan." };
        Option<string?> scanHead = new("--head") { Description = "Head ref for a range scan." };
        Option<bool> json = CliApp.JsonOption();
        scanCommand.Options.Add(repo);
        scanCommand.Options.Add(scope);
        scanCommand.Options.Add(source);
        scanCommand.Options.Add(scanBase);
        scanCommand.Options.Add(scanHead);
        scanCommand.Options.Add(json);
        scanCommand.SetAction(parseResult => CliHost.Run(meta, "hygiene scan",
            () => RunnerCommands.HygieneScan(meta, parseResult.GetValue(repo)!, parseResult.GetValue(scope)!,
                parseResult.GetValue(source)!, parseResult.GetValue(scanBase), parseResult.GetValue(scanHead)),
            forceJson: CliApp.ForceJson(parseResult, json)));
        hygieneCommand.Subcommands.Add(scanCommand);
    }

    private static void AddGitleaks(Command hygieneCommand, CliMeta meta)
    {
        Command gitleaksCommand = new("gitleaks", "Gitleaks tool localization checks.");
        AddGitleaksVerify(gitleaksCommand, meta);
        AddGitleaksUpdate(gitleaksCommand, meta);
        AddGitleaksRender(gitleaksCommand, meta);
        hygieneCommand.Subcommands.Add(gitleaksCommand);
    }

    private static void AddGitleaksVerify(Command gitleaksCommand, CliMeta meta)
    {
        Command command = new("verify", "Verify the vendored Gitleaks manifest, executable, license, and config.");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "hygiene gitleaks verify",
            () => RunnerCommands.GitleaksVerify(meta, parseResult.GetValue(repo)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        gitleaksCommand.Subcommands.Add(command);
    }

    private static void AddGitleaksUpdate(Command gitleaksCommand, CliMeta meta)
    {
        Command command = new("update-check", "Check the pinned Gitleaks release against upstream (explicit, network-enabled).");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "hygiene gitleaks update-check",
            () => RunnerCommands.GitleaksUpdateCheck(meta, parseResult.GetValue(repo)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        gitleaksCommand.Subcommands.Add(command);
    }

    private static void AddGitleaksRender(Command gitleaksCommand, CliMeta meta)
    {
        Command command = new("render-config", "Render tools/gitleaks/config/gitleaks.toml deterministically from rules/hygiene.json.");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "hygiene gitleaks render-config",
            () => RunnerCommands.GitleaksRenderConfig(meta, parseResult.GetValue(repo)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        gitleaksCommand.Subcommands.Add(command);
    }
}
