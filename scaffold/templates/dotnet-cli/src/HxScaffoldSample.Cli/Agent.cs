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
        var commands = root.Subcommands.Select(c => new
        {
            name = c.Name,
            summary = c.Description,
            options = c.Options.Select(o => new { name = o.Name, description = o.Description }).ToList(),
        }).ToList();

        return Ok(command, $"{ToolName} capability description.",
            new { tool = ToolName, version = ToolVersion, exitClasses = Enum.GetNames<ExitClass>(), commands });
    }

    private static JsonNode? ToNode(object? data) => data is null ? null : JsonSerializer.SerializeToNode(data, Json);
}
