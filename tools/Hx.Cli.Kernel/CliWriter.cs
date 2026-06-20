using System.Text.Json;
using Hx.Tooling.Contracts;

namespace Hx.Cli.Kernel;

/// <summary>
/// Renders a <see cref="CliResult"/> to an output stream. JSON is the agent contract: compact + LF-normalized —
/// UTF-8 bytes straight to the stream via <see cref="JsonSerializer"/>'s internal <c>Utf8JsonWriter</c> (no
/// intermediate string), with a single explicit <c>\n</c> (never the platform newline). Human mode
/// is a readable summary + pinpointed diagnostics. The kernel never binds to <c>Console</c> for output — callers
/// pass the stream.
/// </summary>
public static class CliWriter
{
    private static readonly JsonSerializerOptions JsonOptions = JsonContractSerializerOptions.Create();

    /// <summary>Human at a TTY; JSON when piped/redirected. <paramref name="forceJson"/> (the --output flag) overrides.</summary>
    public static bool PreferHuman(bool? forceJson) => forceJson != true && !Console.IsOutputRedirected;

    /// <summary>Compact, LF-normalized JSON straight to the stream (no intermediate string), one trailing <c>\n</c>.</summary>
    public static void WriteJson(Stream output, CliResult result)
    {
        JsonSerializer.Serialize(output, result, JsonOptions);
        output.WriteByte((byte)'\n');
        output.Flush();
    }

    /// <summary>
    /// One NDJSON streaming event: compact JSON + a single <c>\n</c>, flushed immediately so a consuming
    /// agent sees each phase live. The final <see cref="CliResult"/> envelope follows as the last line of the stream.
    /// </summary>
    public static void WriteEvent(Stream output, CliEvent evt)
    {
        JsonSerializer.Serialize(output, evt, JsonOptions);
        output.WriteByte((byte)'\n');
        output.Flush();
    }

    /// <summary>A readable summary, pinpointed diagnostics, the operator decision (if any), and next actions.</summary>
    public static void WriteHuman(TextWriter writer, CliResult result)
    {
        writer.WriteLine(result.Summary);
        foreach (Diagnostic d in result.Errors)
        {
            WriteDiagnostic(writer, d);
        }

        foreach (Diagnostic d in result.Warnings)
        {
            WriteDiagnostic(writer, d);
        }

        if (result.RequiresOperator && result.Decision is { } q)
        {
            writer.WriteLine($"  operator decision needed: {q.Question}");
        }

        foreach (CliNextAction a in result.NextActions)
        {
            writer.WriteLine(a.Command is { } c ? $"  next: {a.Label}  ({c})" : $"  next: {a.Label}");
        }
    }

    private static void WriteDiagnostic(TextWriter writer, Diagnostic d)
    {
        string loc = string.Empty;
        if (d.Location is { Path: { } p } l)
        {
            loc = $" {p}";
            if (l.Line is { } line)
            {
                loc += $":{line}";
            }

            if (l.Column is { } col)
            {
                loc += $":{col}";
            }
        }

        string target = d.Target is { } t ? $" [{t}]" : string.Empty;
        writer.WriteLine($"  {d.Severity.ToString().ToLowerInvariant()} {d.Code}{loc}{target}: {d.Message}");
        if (d.Hint is { } h)
        {
            writer.WriteLine($"      hint: {h}");
        }
    }
}
