using System.Diagnostics;

namespace Hx.Runner.Core.Process;

public sealed record ToolCommand(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string>? Environment = null)
{
    public ProcessStartInfo ToStartInfo()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = FileName,
            WorkingDirectory = WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (string argument in Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (Environment is not null)
        {
            foreach (KeyValuePair<string, string> variable in Environment)
            {
                startInfo.Environment[variable.Key] = variable.Value;
            }
        }

        return startInfo;
    }

    public static ToolCommand DotNet(string workingDirectory, params string[] arguments)
    {
        return new ToolCommand("dotnet", arguments, workingDirectory);
    }
}
