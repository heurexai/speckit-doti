using System.CommandLine;
using Hx.Cli.Kernel;

namespace Hx.Runner.Cli;

internal static partial class RunnerCommandFactory
{
    private static void AddTools(RootCommand rootCommand, CliMeta meta)
    {
        Command toolsCommand = new("tools", "Vendored-tool provisioning.");
        Command command = new("fetch", "Fetch + hash-verify the vendored tool binaries from their pinned manifests (fail-closed on mismatch).");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<string?> rid = new("--rid") { Description = "Target RID (default: host)." };
        Option<string> tool = new("--tool") { Description = "all | gitleaks | sentrux | gitversion", DefaultValueFactory = _ => "all" };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(rid);
        command.Options.Add(tool);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "tools fetch",
            () => RunnerCommands.ToolsFetch(meta, parseResult.GetValue(repo)!,
                parseResult.GetValue(rid), parseResult.GetValue(tool)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        toolsCommand.Subcommands.Add(command);
        rootCommand.Subcommands.Add(toolsCommand);
    }

    private static void AddErrorCodes(RootCommand rootCommand, CliMeta meta)
    {
        Command errorcodesCommand = new("errorcodes", "Error-code registry: render the generated constants and check stability.");
        AddErrorCodesRender(errorcodesCommand, meta);
        AddErrorCodesCheck(errorcodesCommand, meta);
        rootCommand.Subcommands.Add(errorcodesCommand);
    }

    private static void AddErrorCodesRender(Command errorcodesCommand, CliMeta meta)
    {
        Command command = new("render", "Regenerate tools/Hx.Cli.Kernel/ErrorCodes.g.cs from errorcodes/registry.json.");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "errorcodes render",
            () => RunnerCommands.ErrorCodesRender(meta, parseResult.GetValue(repo)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        errorcodesCommand.Subcommands.Add(command);
    }

    private static void AddErrorCodesCheck(Command errorcodesCommand, CliMeta meta)
    {
        Command command = new("check", "Fail-closed: every shipped code is still registered, unchanged, and the generated file is current.");
        Option<string> repo = new("--repo") { Description = "Repository root.", DefaultValueFactory = _ => "." };
        Option<bool> json = CliApp.JsonOption();
        command.Options.Add(repo);
        command.Options.Add(json);
        command.SetAction(parseResult => CliHost.Run(meta, "errorcodes check",
            () => RunnerCommands.ErrorCodesCheck(meta, parseResult.GetValue(repo)!),
            forceJson: CliApp.ForceJson(parseResult, json)));
        errorcodesCommand.Subcommands.Add(command);
    }
}
