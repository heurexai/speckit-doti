using System.Diagnostics;

namespace Hx.Templates.Tests;

/// <summary>Locates the template assets in the repo and runs `dotnet` for the round-trip smoke.</summary>
internal static class TemplateRepo
{
    public static string Root { get; } = FindRoot();

    public static string TemplateDir => Path.Combine(Root, "scaffold", "templates", "dotnet-cli");
    public static string TemplateConfig => Path.Combine(TemplateDir, ".template.config", "template.json");
    public static string Slnx => Path.Combine(TemplateDir, "HxScaffoldSample.slnx");
    public static string PackProject => Path.Combine(Root, "scaffold", "Hx.Scaffold.Templates.csproj");
    public static string ArchTests => Path.Combine(
        TemplateDir, "test", "HxScaffoldSample.Architecture.Tests", "ArchitectureTests.cs");
    public static string ArchitectureJson => Path.Combine(TemplateDir, "rules", "architecture.json");
    public static string HygieneJson => Path.Combine(TemplateDir, "rules", "hygiene.json");
    public static string SentruxJson => Path.Combine(TemplateDir, "rules", "sentrux.json");
    public static string SentruxRulesToml => Path.Combine(TemplateDir, ".sentrux", "rules.toml");
    public static string SentruxIgnore => Path.Combine(TemplateDir, ".sentruxignore");

    private static string FindRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "scaffold-dotnet.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate the repo root (scaffold-dotnet.slnx) above the test output.");
    }

    public static (int ExitCode, string Output) RunDotnet(string args, string workingDirectory, IReadOnlyDictionary<string, string>? env = null)
    {
        var psi = new ProcessStartInfo("dotnet", args)
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

        // Drain stdout and stderr CONCURRENTLY. Reading one stream to the end before the other
        // deadlocks when the child fills the unread stream's pipe buffer (~4 KB) — the child blocks
        // on the write while we block on the read, and neither can proceed. The generated
        // solution's `dotnet test` is verbose enough to hit this. See the Process.StandardOutput
        // remarks. Kicking off both async reads before WaitForExit keeps both buffers drained.
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        string output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
        return (process.ExitCode, output);
    }
}
