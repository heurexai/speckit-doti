using System.CommandLine;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace HxScaffoldSample.Cli;

// The agent-first CLI envelope — a self-contained starter you own and extend. Every command returns a CliResult and
// renders through Agent (JSON for machines, a readable line for a TTY); the JSON validates against the Heurex CLI
// envelope schema. Keep command bodies thin: do the work, return a CliResult, let Agent render + set the exit code.

/// <summary>The command outcome (Status ring) — serialized camelCase: success | partial | failed | blocked | skipped.</summary>
public enum CliOutcome { Success, Partial, Failed, Blocked, Skipped }

/// <summary>Diagnostic severity — serialized camelCase: error | warning | info.</summary>
public enum Severity { Error, Warning, Info }

/// <summary>The small fixed set of process exit classes (the int IS the process exit code).</summary>
public enum ExitClass { Success = 0, Usage = 2, Validation = 3, Integrity = 4, Internal = 70 }

/// <summary>Human help rendering mode.</summary>
public enum HelpMode { Auto, Rich, Plain }

/// <summary>A structured, pinpointed diagnostic (the Diagnostics ring).</summary>
public sealed record Diagnostic(string Code, Severity Severity, string Message, string? Target = null, string? Hint = null, bool Blocking = true);

/// <summary>A machine-readable next step an agent can take (the Direction ring).</summary>
public sealed record CliNextAction(string Label, string Why, string? Command = null);

/// <summary>The agent-first output envelope every command returns (Status / Identity / Diagnostics / Direction / Result).</summary>
public sealed record CliResult(
    int SchemaVersion,
    string Tool,
    string Version,
    string Command,
    CliOutcome Outcome,
    bool Ok,
    int ExitCode,
    string Summary,
    IReadOnlyList<Diagnostic> Errors,
    IReadOnlyList<Diagnostic> Warnings,
    IReadOnlyList<Diagnostic> Info,
    IReadOnlyList<CliNextAction> NextActions,
    bool RequiresOperator,
    long ElapsedMs,
    JsonNode? Data = null);

/// <summary>
/// Renders a <see cref="CliResult"/> and hosts thin commands. JSON is the agent contract: compact + LF-normalized
/// (one trailing <c>\n</c>, no <c>\r</c>) straight to the stream; on a TTY it prints a readable summary instead.
/// Output goes to an injected stream (stdout by default; tests pass a buffer), never bound to <c>Console</c> here.
/// </summary>
public static class Agent
{
    public const int Schema = 1;

    private static readonly JsonSerializerOptions Json = CreateOptions();
    private const string Gold = "\u001b[38;2;201;169;97m";
    private const string Navy = "\u001b[38;2;26;31;77m";
    private const string Light = "\u001b[38;2;244;246;249m";
    private const string Muted = "\u001b[38;2;139;145;167m";
    private const string Reset = "\u001b[0m";
    private static readonly string ToolName =
        (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Name?.ToLowerInvariant() ?? "cli";
    private static readonly string ToolVersion =
        (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Version?.ToString() ?? "0.0.0";

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    /// <summary>Shared CLI entry point: intercept human help for root/group/leaf commands, then dispatch normally.</summary>
    public static int Invoke(RootCommand root, string[] args)
    {
        if (TryHelp(root, args, out Command? command, out List<string>? path, out HelpMode mode))
        {
            WriteHelp(command!, path!, mode);
            return (int)ExitClass.Success;
        }

        return root.Parse(args).Invoke();
    }

    /// <summary>A success: <paramref name="data"/> becomes the Result ring, exit 0.</summary>
    public static CliResult Ok(string command, string summary, object? data = null, IReadOnlyList<CliNextAction>? nextActions = null) =>
        new(Schema, ToolName, ToolVersion, command, CliOutcome.Success, true, (int)ExitClass.Success, summary,
            [], [], [], nextActions ?? [], false, 0, ToNode(data));

    /// <summary>A failure: the <paramref name="errors"/> block the command; the exit code is <paramref name="exitClass"/>.</summary>
    public static CliResult Fail(string command, ExitClass exitClass, IReadOnlyList<Diagnostic> errors, string? summary = null) =>
        new(Schema, ToolName, ToolVersion, command, CliOutcome.Failed, false, (int)exitClass,
            summary ?? (errors.Count > 0 ? errors[0].Message : "Command failed."), errors, [], [], [], false, 0, null);

    /// <summary>Time the body, render the result (JSON or human), fail-close any exception, and return the exit code.</summary>
    public static int Run(string command, Func<CliResult> body, Stream? output = null, bool? forceJson = null)
    {
        Stream stream = output ?? Console.OpenStandardOutput();
        var stopwatch = Stopwatch.StartNew();
        CliResult result;
        try
        {
            result = body() with { ElapsedMs = stopwatch.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            result = Fail(command, ExitClass.Internal, [new Diagnostic("INT0001", Severity.Error, $"{command} failed: {ex.Message}")])
                with { ElapsedMs = stopwatch.ElapsedMilliseconds };
        }

        if (forceJson != true && !Console.IsOutputRedirected)
        {
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true) { NewLine = "\n" };
            writer.WriteLine(result.Summary);
            foreach (Diagnostic d in result.Errors)
            {
                writer.WriteLine($"  {d.Severity.ToString().ToLowerInvariant()} {d.Code}: {d.Message}");
            }

            writer.Flush();
        }
        else
        {
            JsonSerializer.Serialize(stream, result, Json);
            stream.WriteByte((byte)'\n');
            stream.Flush();
        }

        return result.ExitCode;
    }

    /// <summary>Emit the machine-readable capability description (commands + options) so an agent learns the tool in one call.</summary>
    public static CliResult Describe(RootCommand root, string command)
    {
        return Ok(command, $"{ToolName} capability description.",
            new { tool = ToolName, version = ToolVersion, exitClasses = Enum.GetNames<ExitClass>(), root = DescribeCommand(root) });
    }

    private static object DescribeCommand(Command command) => new
    {
        name = command.Name,
        summary = command.Description,
        options = command.Options.Select(o => new { name = o.Name, description = o.Description }).ToList(),
        subcommands = command.Subcommands.Select(DescribeCommand).ToList(),
    };

    private static bool TryHelp(RootCommand root, string[] args, out Command? command, out List<string>? path, out HelpMode mode)
    {
        mode = HelpModeFromEnvironment();
        command = null;
        path = null;

        var remaining = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            string token = args[i];
            if (EqualsToken(token, "--plain-help") || EqualsToken(token, "--no-ansi-help"))
            {
                mode = HelpMode.Plain;
                continue;
            }

            if (token.StartsWith("--help-mode=", StringComparison.OrdinalIgnoreCase))
            {
                mode = ParseHelpMode(token["--help-mode=".Length..]);
                continue;
            }

            if (EqualsToken(token, "--help-mode") && i + 1 < args.Length)
            {
                mode = ParseHelpMode(args[++i]);
                continue;
            }

            remaining.Add(token);
        }

        if (remaining.Count == 0)
        {
            command = root;
            path = [];
            return true;
        }

        int helpIndex = remaining.FindIndex(t => (t is "-h" or "--help" or "-?") || EqualsToken(t, "help"));
        if (helpIndex < 0)
        {
            return false;
        }

        List<string> pathTokens = EqualsToken(remaining[helpIndex], "help")
            ? remaining.Skip(helpIndex + 1).Where(IsCommandToken).ToList()
            : remaining.Take(helpIndex).Where(IsCommandToken).ToList();
        command = Resolve(root, pathTokens);
        path = PathFrom(root, command);
        return true;
    }

    private static void WriteHelp(Command command, IReadOnlyList<string> path, HelpMode mode)
    {
        bool plain = mode == HelpMode.Plain || mode == HelpMode.Auto && (Console.IsOutputRedirected || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")));
        if (plain)
        {
            Console.Write(RenderPlainHelp(command, path));
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"{Gold}{ToolName}{Reset} {Muted}{ToolVersion}{Reset}");
        Console.WriteLine($"{Muted}{Description(command)}{Reset}");
        Console.WriteLine();
        Console.WriteLine($"{Muted}usage:{Reset} {Light}{Invocation(path)}{UsageSuffix(command)}{Reset}");
        if (command.Subcommands.Count > 0)
        {
            Console.WriteLine();
            WriteRows("Commands", command.Subcommands.Select(c => (c.Name, Description(c))));
        }

        Console.WriteLine();
        WriteRows("Options", HelpOptions(command));
        Console.WriteLine();
    }

    public static string RenderPlainHelp(Command command, IReadOnlyList<string> path)
    {
        var lines = new List<string>
        {
            $"{ToolName} {ToolVersion}",
            Description(command),
            string.Empty,
            "Usage:",
            $"  {Invocation(path)}{UsageSuffix(command)}",
            string.Empty,
        };
        if (command.Subcommands.Count > 0)
        {
            lines.Add("Commands:");
            AddRows(lines, command.Subcommands.Select(c => (c.Name, Description(c))));
            lines.Add(string.Empty);
        }

        lines.Add("Options:");
        AddRows(lines, HelpOptions(command));
        lines.Add(string.Empty);
        return string.Join(Environment.NewLine, lines);
    }

    private static void WriteRows(string title, IEnumerable<(string Name, string Description)> rows)
    {
        (string Name, string Description)[] materialized = rows.ToArray();
        int width = materialized.Length == 0 ? 0 : materialized.Max(r => r.Name.Length);
        Console.WriteLine($"{Gold}{title}{Reset}");
        Console.WriteLine($"{Navy}+{new string('-', width + 2)}+{new string('-', 54)}+{Reset}");
        foreach ((string name, string description) in materialized)
        {
            Console.WriteLine($"{Navy}|{Reset} {Light}{name.PadRight(width)}{Reset} {Navy}|{Reset} {description}");
        }

        Console.WriteLine($"{Navy}+{new string('-', width + 2)}+{new string('-', 54)}+{Reset}");
    }

    private static void AddRows(List<string> lines, IEnumerable<(string Name, string Description)> rows)
    {
        (string Name, string Description)[] materialized = rows.ToArray();
        int width = materialized.Length == 0 ? 0 : materialized.Max(r => r.Name.Length);
        foreach ((string name, string description) in materialized)
        {
            lines.Add($"  {name.PadRight(width)}  {description}");
        }
    }

    private static IReadOnlyList<(string Name, string Description)> HelpOptions(Command command)
    {
        var rows = command.Options
            .Select(o => (o.Name, string.IsNullOrWhiteSpace(o.Description) ? "No description." : o.Description!))
            .ToList();
        rows.Add(("--help-mode <auto|rich|plain>", "Select human help rendering. Also honors HX_HELP_MODE and NO_COLOR."));
        rows.Add(("--plain-help", "Shortcut for --help-mode plain."));
        rows.Add(("-h, --help, -?", "Show help for this command."));
        return rows;
    }

    private static Command Resolve(RootCommand root, IReadOnlyList<string> tokens)
    {
        Command current = root;
        foreach (string token in tokens)
        {
            Command? next = current.Subcommands.FirstOrDefault(c => EqualsToken(c.Name, token));
            if (next is null)
            {
                break;
            }

            current = next;
        }

        return current;
    }

    private static List<string> PathFrom(RootCommand root, Command command)
    {
        var path = new List<string>();
        return ReferenceEquals(root, command) || TryFind(root, command, path) ? path : [command.Name];
    }

    private static bool TryFind(Command current, Command target, List<string> path)
    {
        foreach (Command child in current.Subcommands)
        {
            path.Add(child.Name);
            if (ReferenceEquals(child, target) || TryFind(child, target, path))
            {
                return true;
            }

            path.RemoveAt(path.Count - 1);
        }

        return false;
    }

    private static HelpMode HelpModeFromEnvironment()
    {
        string? mode = Environment.GetEnvironmentVariable("HX_HELP_MODE");
        return string.IsNullOrWhiteSpace(mode) ? HelpMode.Auto : ParseHelpMode(mode);
    }

    private static HelpMode ParseHelpMode(string value) => value.Trim().ToLowerInvariant() switch
    {
        "plain" or "vanilla" or "text" => HelpMode.Plain,
        "rich" or "ansi" or "color" => HelpMode.Rich,
        _ => HelpMode.Auto,
    };

    private static string Invocation(IReadOnlyList<string> path) =>
        path.Count == 0 ? ToolName : ToolName + " " + string.Join(' ', path);

    private static string UsageSuffix(Command command) =>
        command.Subcommands.Count > 0 ? " [command] [options]" : " [options]";

    private static string Description(Command command) =>
        string.IsNullOrWhiteSpace(command.Description) ? "No description." : command.Description!;

    private static bool EqualsToken(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool IsCommandToken(string token) => !token.StartsWith("-", StringComparison.Ordinal);

    private static JsonNode? ToNode(object? data) => data is null ? null : JsonSerializer.SerializeToNode(data, Json);
}
