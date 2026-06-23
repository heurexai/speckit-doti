using System.Diagnostics;
using System.Text;

namespace Hx.Runner.Core.Process;

public sealed record ProcessRunResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Runs a <see cref="ToolCommand"/> with redirected output. All arguments are
/// passed via <see cref="ProcessStartInfo.ArgumentList"/>; no shell strings.
/// </summary>
public static class ProcessRunner
{
    public static ProcessRunResult Run(ToolCommand command)
    {
        using var process = new System.Diagnostics.Process { StartInfo = command.ToStartInfo() };
        process.Start();

        // Drain stdout and stderr CONCURRENTLY. Reading one stream to the end before the other
        // deadlocks when the child fills the unread stream's pipe buffer (~4 KB). This is latent for
        // quiet native tools (gitleaks/sentrux) but real for verbose children like `dotnet test`.
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return new ProcessRunResult(process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }

    public static ProcessRunResult Run(ToolCommand command, TimeSpan timeout)
    {
        using var process = CreateProcess(command);
        var output = new ProcessOutputBuffer(process);
        process.Start();
        output.BeginRead();

        int timeoutMs = timeout.TotalMilliseconds >= int.MaxValue ? int.MaxValue : (int)timeout.TotalMilliseconds;
        if (process.WaitForExit(timeoutMs))
        {
            int exitCode = process.ExitCode;
            if (!process.WaitForExit(milliseconds: 5000))
            {
                output.AppendError("Output streams did not close within 5 second(s) after process exit.");
            }

            return new ProcessRunResult(exitCode, output.StandardOutput, output.StandardError);
        }

        try { process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { /* already exited */ }

        process.WaitForExit(milliseconds: 5000);
        string timeoutMessage = $"Timed out after {timeout.TotalSeconds:n0} second(s).";
        output.AppendError(timeoutMessage);
        return new ProcessRunResult(-1, output.StandardOutput, output.StandardError);
    }

    private static System.Diagnostics.Process CreateProcess(ToolCommand command) =>
        new() { StartInfo = command.ToStartInfo() };

    private sealed class ProcessOutputBuffer
    {
        private readonly System.Diagnostics.Process _process;
        private readonly StringBuilder _standardOutput = new();
        private readonly StringBuilder _standardError = new();
        private readonly object _gate = new();

        public ProcessOutputBuffer(System.Diagnostics.Process process)
        {
            _process = process;
            _process.OutputDataReceived += (_, e) => Append(_standardOutput, e.Data);
            _process.ErrorDataReceived += (_, e) => Append(_standardError, e.Data);
        }

        public string StandardOutput
        {
            get { lock (_gate) return _standardOutput.ToString(); }
        }

        public string StandardError
        {
            get { lock (_gate) return _standardError.ToString(); }
        }

        public void BeginRead()
        {
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        public void AppendError(string message) => Append(_standardError, message);

        private void Append(StringBuilder builder, string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (_gate)
            {
                builder.AppendLine(line);
            }
        }
    }
}
