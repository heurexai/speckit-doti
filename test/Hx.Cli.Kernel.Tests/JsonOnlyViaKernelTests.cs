using Xunit;

namespace Hx.Cli.Kernel.Tests;

/// <summary>
/// Enforces "all output flows through the kernel": no migrated CLI project writes to
/// <c>Console</c> directly — every command renders its <see cref="Hx.Tooling.Contracts.CliResult"/> via
/// <c>CliHost</c>/<c>CliWriter</c>. A grep-style guard so a regression (a stray <c>Console.WriteLine</c>) fails the
/// gate's test step rather than silently re-introducing un-enveloped output.
/// </summary>
public sealed class JsonOnlyViaKernelTests
{
    [Theory]
    [InlineData("Hx.Impact.Cli")]
    [InlineData("Hx.Runner.Cli")]
    [InlineData("Hx.Scaffold.Cli")]
    public void Migrated_cli_project_never_writes_to_console_directly(string project)
    {
        string dir = Path.Combine(RepoRoot(), "tools", project);
        string[] sources = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToArray();

        Assert.NotEmpty(sources);
        List<string> offenders = [];
        foreach (string file in sources)
        {
            if (File.ReadAllText(file).Contains("Console.Write", StringComparison.Ordinal))
            {
                offenders.Add(Path.GetFileName(file));
            }
        }

        Assert.True(offenders.Count == 0,
            $"{project} writes to Console directly (output must flow through CliHost/CliWriter): {string.Join(", ", offenders)}");
    }

    private static string RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "scaffold-dotnet.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root not found");
    }
}
