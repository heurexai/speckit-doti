using System.Diagnostics;

namespace Hx.Scaffold.Core;

/// <summary>
/// Runs a child process with redirected output drained CONCURRENTLY (the round-trip hang fix:
/// reading one stream to end before the other deadlocks when the child fills the other's buffer).
/// Callers that spawn nested <c>dotnet</c> builds also pass the build-server-isolation env in
/// <see cref="NestedDotnetEnv"/> so persistent grandchildren cannot hold the pipe open.
/// </summary>
internal static class ProcessRunner
{
    public static (int ExitCode, string Output) Run(
        string fileName, string arguments, string workingDirectory, IReadOnlyDictionary<string, string>? env = null)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        if (env is not null)
        {
            foreach (KeyValuePair<string, string> kv in env)
            {
                psi.Environment[kv.Key] = kv.Value;
            }
        }

        using Process process = Process.Start(psi)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }

    /// <summary>Build-server isolation for nested <c>dotnet</c> builds/tests (hang prevention).</summary>
    public static Dictionary<string, string> NestedDotnetEnv()
    {
        string realCache = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        return new Dictionary<string, string>
        {
            ["NUGET_PACKAGES"] = realCache,
            ["MSBUILDDISABLENODEREUSE"] = "1",
            ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0",
        };
    }

    public static string Tail(string output, int chars = 600) =>
        output.Length <= chars ? output : output[^chars..];
}
