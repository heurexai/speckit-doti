using System.CommandLine;
using System.Reflection;
using Hx.Tooling.Contracts;

namespace Hx.Cli.Kernel;

/// <summary>
/// Wiring helpers shared by every migrated CLI so each tool's <c>Program.cs</c> stays thin: version
/// resolution from the entry assembly, the standard <c>--json</c> switch, and the kernel-generated <c>describe</c>
/// subcommand. The kernel never binds to <c>Console</c> for output — rendering flows through
/// <see cref="CliHost"/>.
/// </summary>
public static class CliApp
{
    /// <summary>The informational version stamped by the build (e.g. GitVersion), else the assembly version, else 0.0.0.</summary>
    public static string ResolveVersion(Assembly assembly)
    {
        string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(informational)
            ? assembly.GetName().Version?.ToString() ?? "0.0.0"
            : informational;
    }

    /// <summary>
    /// The standard machine-output switch. Absent ⇒ auto (human on a TTY, JSON when piped); present ⇒ force JSON.
    /// A fresh instance per command — System.CommandLine binds an option to a single owning command.
    /// </summary>
    public static Option<bool> JsonOption() =>
        new("--json") { Description = "Force the machine-readable JSON envelope (auto-selected when output is piped)." };

    /// <summary><c>true</c> to force JSON when <c>--json</c> is set; <c>null</c> to let <see cref="CliWriter"/> auto-detect.</summary>
    public static bool? ForceJson(ParseResult parseResult, Option<bool> jsonOption) =>
        parseResult.GetValue(jsonOption) ? true : null;

    /// <summary>
    /// The thin entry point each tool's <c>Program.cs</c> calls instead of <c>root.Parse(args).Invoke()</c>:
    /// renders the Heurex-branded banner + command table for root-level help (no args, or <c>-h/--help/-?/help</c>
    /// as the first token), and otherwise dispatches to System.CommandLine. Per-subcommand help (e.g. <c>new --help</c>)
    /// and <c>--version</c> fall through unchanged.
    /// </summary>
    public static int Invoke(RootCommand root, CliMeta meta, string[] args, string banner, string tagline)
    {
        if (WantsRootHelp(args))
        {
            CliRenderer.WriteHelp(root, meta, banner, tagline);
            return (int)ExitClass.Success;
        }

        return root.Parse(args).Invoke();
    }

    private static bool WantsRootHelp(string[] args) =>
        args.Length == 0 || args[0] is "-h" or "--help" or "-?" or "help";

    /// <summary>
    /// Adds a kernel-generated <c>describe</c> subcommand that emits the capability model for <paramref name="root"/>
    /// (command/option tree + exit classes + error-code catalog) so an agent learns the whole tool in one call.
    /// </summary>
    public static void AddDescribe(RootCommand root, CliMeta meta, IReadOnlyList<ErrorCodeEntry> errorCodes)
    {
        Option<bool> jsonOption = JsonOption();
        Command describe = new("describe",
            "Emit the machine-readable capability description: command/option tree, exit classes, and error-code catalog.");
        describe.Options.Add(jsonOption);
        describe.SetAction(parseResult => CliHost.Run(meta, "describe",
            () => CliResults.Ok(meta, "describe", $"{meta.Tool} capability description.",
                DescribeWalker.Describe(meta, root, errorCodes)),
            forceJson: ForceJson(parseResult, jsonOption)));
        root.Subcommands.Add(describe);
    }
}
