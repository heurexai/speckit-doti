using System.Diagnostics;
using System.Text;
using Hx.Tooling.Contracts;

namespace Hx.Cli.Kernel;

/// <summary>
/// The thin-command host: times the command body, renders the resulting <see cref="CliResult"/> (JSON or human),
/// returns the process exit code, and fail-closes any unhandled exception to an Internal diagnostic. Output goes to
/// an <b>injected</b> stream (the CLI passes stdout; tests pass a buffer) — the kernel never binds to <c>Console</c>
/// for output. <see cref="RunStreaming"/> additionally hands the body an <c>emit</c> callback for NDJSON phases.
/// </summary>
public static class CliHost
{
    public static int Run(
        CliMeta meta, string command, Func<CliResult> body, Stream? output = null, bool? forceJson = null) =>
        Execute(meta, command, _ => body(), output, forceJson, streamEvents: false);

    /// <summary>
    /// Like <see cref="Run"/>, but the body receives an <c>emit</c> callback. When <paramref name="streamEvents"/> is
    /// set and the sink is JSON, each emitted <see cref="CliEvent"/> is written as an NDJSON line as it happens, then
    /// the final envelope follows. On a human TTY (or when not streaming) <c>emit</c> is a no-op.
    /// </summary>
    public static int RunStreaming(
        CliMeta meta, string command, Func<Action<CliEvent>, CliResult> body, Stream? output = null,
        bool? forceJson = null, bool streamEvents = false) =>
        Execute(meta, command, body, output, forceJson, streamEvents);

    /// <summary>
    /// A long-running command with a Heurex-branded live progress display in human mode: the body's emitted
    /// <see cref="CliEvent"/> steps drive a Spectre progress bar, then a summary panel is rendered. In JSON/piped
    /// mode this is byte-identical to <see cref="Run"/> — the emit callback is a no-op and only the final envelope
    /// is written — so the agent contract is unchanged.
    /// </summary>
    public static int RunWithProgress(
        CliMeta meta, string command, Func<Action<CliEvent>, CliResult> body, Stream? output = null,
        bool? forceJson = null) =>
        CliWriter.PreferHuman(forceJson)
            ? CliRenderer.RunWithLiveProgress(meta, command, body)
            : Execute(meta, command, body, output, forceJson, streamEvents: false);

    private static int Execute(
        CliMeta meta, string command, Func<Action<CliEvent>, CliResult> body, Stream? output, bool? forceJson,
        bool streamEvents)
    {
        Stream stream = output ?? Console.OpenStandardOutput();
        bool json = !CliWriter.PreferHuman(forceJson);
        Action<CliEvent> emit = streamEvents && json ? evt => CliWriter.WriteEvent(stream, evt) : static _ => { };

        var stopwatch = Stopwatch.StartNew();
        CliResult result;
        try
        {
            result = body(emit) with { ElapsedMs = stopwatch.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            result = CliResults.Fail(meta, command, ExitClass.Internal,
                [Diag.Of(ErrorCodes.Internal_Unhandled, $"{command} failed: {ex.Message}")])
                with { ElapsedMs = stopwatch.ElapsedMilliseconds };
        }

        if (json)
        {
            CliWriter.WriteJson(stream, result);
        }
        else
        {
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true) { NewLine = "\n" };
            CliWriter.WriteHuman(writer, result);
            writer.Flush();
        }

        return result.ExitCode;
    }
}
