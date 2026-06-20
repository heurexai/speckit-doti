using Hx.Impact.Core.Graph;
using Hx.Scaffold.Core;
using Xunit;

namespace Hx.Scaffold.Tests;

public sealed class SourceVendorClosureTests
{
    // The vendored-source set copied into generated repos MUST equal the forward project-reference
    // closure of the vendored CLIs. Otherwise a generated repo receives a CLI whose <ProjectReference>
    // points at a project that was never copied (an Hx.Gate.Core-style gap), and its self-hosting
    // `dotnet run --project tools/Hx.Runner.Cli` cannot build. This guard fails closed on any drift —
    // it is computed from the real project graph, not a hand-maintained list.
    [Fact]
    public void VendoredProjects_equal_the_forward_closure_of_the_vendored_clis()
    {
        string repoRoot = FindRepoRoot();
        var graph = new ProjectGraphBuilder().Build(repoRoot, "scaffold-dotnet.slnx");

        string[] cliPaths = graph.Nodes.Values
            .Where(n => n.Name is "Hx.Runner.Cli" or "Hx.Impact.Cli")
            .Select(n => n.Path)
            .ToArray();
        Assert.Equal(2, cliPaths.Length);

        HashSet<string> closure = graph.DependencyClosureOf(cliPaths)
            .Select(path => graph.Nodes[path].Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var vendored = SourceVendor.Projects.ToHashSet(StringComparer.OrdinalIgnoreCase);

        string[] missing = closure.Except(vendored, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        string[] extra = vendored.Except(closure, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        Assert.True(missing.Length == 0,
            "SourceVendor.Projects is MISSING projects the vendored CLIs reference (a generated repo would fail to build): " + string.Join(", ", missing));
        Assert.True(extra.Length == 0,
            "SourceVendor.Projects vendors projects outside the CLI closure (dead weight): " + string.Join(", ", extra));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "scaffold-dotnet.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new DirectoryNotFoundException("scaffold-dotnet.slnx not found above the test output directory.");
    }
}
