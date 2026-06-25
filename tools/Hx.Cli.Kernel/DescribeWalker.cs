using System.CommandLine;
using Hx.Tooling.Contracts;

namespace Hx.Cli.Kernel;

/// <summary>
/// Builds the <see cref="CliDescribe"/> capability model by walking System.CommandLine's symbol tree, so
/// machine-readable help is generated from the live parser and cannot drift from the actual commands/options.
/// Includes the catalogs (the <c>ExitClass</c> set + the error-code registry) for one-call discovery.
/// </summary>
public static class DescribeWalker
{
    public static CliDescribe Describe(
        CliMeta meta,
        Command root,
        IReadOnlyList<ErrorCodeEntry> errorCodes,
        CliDescribeWorkflow? workflow = null) =>
        new(JsonContractDefaults.SchemaVersion, meta.Tool, meta.Version, Walk(root),
            Enum.GetNames<ExitClass>(), errorCodes, workflow);

    private static CliDescribeCommand Walk(Command command) =>
        new(command.Name,
            string.IsNullOrWhiteSpace(command.Description) ? null : command.Description,
            command.Options.Select(ToOption).ToList(),
            command.Subcommands.Select(Walk).ToList());

    private static CliDescribeOption ToOption(Option option) =>
        new(option.Name, option.ValueType.Name, option.Required,
            string.IsNullOrWhiteSpace(option.Description) ? null : option.Description);
}
