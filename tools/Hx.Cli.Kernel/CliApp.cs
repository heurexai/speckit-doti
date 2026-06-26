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
    /// The hardened entry point each tool's <c>Program.cs</c> calls instead of <c>root.Parse(args).Invoke()</c>
    /// (FR-017/FR-018): the kernel owns ALL help, <c>--version</c>, and parse-error rendering through the shared
    /// branded renderer / <c>CliResult</c> envelope, so NO command surface falls through to System.CommandLine's
    /// default help/version/error output. The interception happens before <c>Invoke()</c> (parsing renders
    /// nothing; only <c>Invoke()</c> would run SCL's default help/version/error actions), so by branding those
    /// cases and only invoking a real command action, the SCL defaults are never reached.
    /// </summary>
    public static int Harden(
        RootCommand root, CliMeta meta, string[] args, string banner, string tagline, Stream? output = null,
        string? helpContext = null)
    {
        // 1. Help (any form, any level) → branded help. The optional helpContext (007 T045, FR-042) is the active
        //    tier + channel one-liner, surfaced in the human help header alongside the machine-readable describe.
        if (CliHelpRequestParser.TryParse(root, args) is { } help)
        {
            CliRenderer.WriteHelp(root, help.Command, help.Path, meta, banner, tagline, help.Mode, helpContext: helpContext);
            return (int)ExitClass.Success;
        }

        ParseResult parse = root.Parse(args);
        bool? forceJson = args.Contains("--json", StringComparer.Ordinal) ? true : null;

        // 2. Root --version → branded CliResult (never SCL's default version line).
        if (IsRootVersionRequest(root, parse, args))
        {
            return CliHost.Run(meta, "version",
                () => CliResults.Ok(meta, "version", $"{meta.Tool} {meta.Version}",
                    new { tool = meta.Tool, version = meta.Version }),
                output, forceJson: forceJson);
        }

        // 3. Parse errors (unknown command/option, unknown subcommand, missing required arg) → branded Usage
        //    CliResult (never SCL's default unbranded error text).
        if (parse.Errors.Count > 0)
        {
            string detail = string.Join("; ", parse.Errors.Select(e => e.Message));
            return CliHost.Run(meta, "usage",
                () => CliResults.Fail(meta, "usage", ExitClass.Usage,
                    [Diag.Of(ErrorCodes.Usage_InvalidArguments, detail)]),
                output, forceJson: forceJson);
        }

        // 4. A real command → execute; its own action renders its CliResult.
        return parse.Invoke();
    }

    /// <summary>Back-compat alias — delegates to <see cref="Harden"/> so existing roots harden without churn.</summary>
    public static int Invoke(RootCommand root, CliMeta meta, string[] args, string banner, string tagline) =>
        Harden(root, meta, args, banner, tagline);

    private static bool IsRootVersionRequest(RootCommand root, ParseResult parse, string[] args) =>
        args.Any(a => string.Equals(a, "--version", StringComparison.OrdinalIgnoreCase))
        && ReferenceEquals(parse.CommandResult.Command, root);

    /// <summary>
    /// Adds a kernel-generated <c>describe</c> subcommand that emits the capability model for <paramref name="root"/>
    /// (command/option tree + exit classes + error-code catalog) so an agent learns the whole tool in one call.
    /// </summary>
    public static void AddDescribe(
        RootCommand root,
        CliMeta meta,
        IReadOnlyList<ErrorCodeEntry> errorCodes,
        CliDescribeWorkflow? workflow = null,
        DistributionChannelInfo? channel = null,
        CliDescribeTier? tier = null)
    {
        Option<bool> jsonOption = JsonOption();
        Command describe = new("describe",
            "Emit the machine-readable capability description: command/option tree, exit classes, and error-code catalog.");
        describe.Options.Add(jsonOption);
        describe.SetAction(parseResult => CliHost.Run(meta, "describe",
            () => CliResults.Ok(meta, "describe", $"{meta.Tool} capability description.",
                DescribeWalker.Describe(meta, root, errorCodes, workflow, channel, tier)),
            forceJson: ForceJson(parseResult, jsonOption)));
        root.Subcommands.Add(describe);
    }
}
