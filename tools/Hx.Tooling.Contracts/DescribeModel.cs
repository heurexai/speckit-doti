namespace Hx.Tooling.Contracts;

/// <summary>An option in the describe tree: name, type, whether required, description, default.</summary>
public sealed record CliDescribeOption(
    string Name,
    string Type,
    bool Required,
    string? Description = null,
    string? Default = null);

/// <summary>A command in the describe tree, with its options and subcommands.</summary>
public sealed record CliDescribeCommand(
    string Name,
    string? Summary,
    IReadOnlyList<CliDescribeOption> Options,
    IReadOnlyList<CliDescribeCommand> Subcommands);

/// <summary>
/// The machine-readable capability description emitted by <c>describe --json</c> — the full command/option tree plus
/// the catalogs (the <see cref="ExitClasses"/> set + the error-code registry in <see cref="ErrorCodeCatalog"/>) so an
/// agent learns the whole tool in one call. The catalog is the set of <em>possible</em> codes, distinct from the
/// per-call <c>errors</c> ring on <see cref="CliResult"/>.
/// </summary>
public sealed record CliDescribe(
    int SchemaVersion,
    string Tool,
    string Version,
    CliDescribeCommand Root,
    IReadOnlyList<string> ExitClasses,
    IReadOnlyList<ErrorCodeEntry> ErrorCodeCatalog);
