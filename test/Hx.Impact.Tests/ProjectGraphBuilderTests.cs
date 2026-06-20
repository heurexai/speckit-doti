using System.Text;
using Hx.Impact.Core.Graph;
using Xunit;

namespace Hx.Impact.Tests;

public sealed class ProjectGraphBuilderTests
{
    [Fact]
    public void DownstreamOf_a_leaf_narrows_to_its_dependents_only()
    {
        using var repo = new GraphFixture();
        var graph = new ProjectGraphBuilder().Build(repo.Root, "test.slnx");

        string[] downstream = graph.DownstreamOf("src/C/C.csproj").Select(NameOf).OrderBy(Id, StringComparer.Ordinal).ToArray();
        Assert.Equal(["C", "C.Tests"], downstream);
    }

    [Fact]
    public void DownstreamOf_a_shared_leaf_reaches_everything()
    {
        using var repo = new GraphFixture();
        var graph = new ProjectGraphBuilder().Build(repo.Root, "test.slnx");

        string[] downstream = graph.DownstreamOf("src/A/A.csproj").Select(NameOf).OrderBy(Id, StringComparer.Ordinal).ToArray();
        Assert.Equal(["A", "A.Tests", "B", "C", "C.Tests"], downstream);
    }

    [Fact]
    public void DependencyClosureOf_is_the_forward_closure()
    {
        using var repo = new GraphFixture();
        var graph = new ProjectGraphBuilder().Build(repo.Root, "test.slnx");

        string[] closure = graph.DependencyClosureOf(["test/C.Tests/C.Tests.csproj"]).Select(NameOf).OrderBy(Id, StringComparer.Ordinal).ToArray();
        Assert.Equal(["A", "B", "C", "C.Tests"], closure);
    }

    [Fact]
    public void Test_projects_are_detected_and_a_clean_graph_has_no_findings()
    {
        using var repo = new GraphFixture();
        var graph = new ProjectGraphBuilder().Build(repo.Root, "test.slnx");

        Assert.True(graph.Nodes["test/C.Tests/C.Tests.csproj"].IsTestProject);
        Assert.False(graph.Nodes["src/C/C.csproj"].IsTestProject);
        Assert.Empty(graph.Findings);
    }

    [Fact]
    public void An_unresolved_reference_is_a_finding()
    {
        using var repo = new GraphFixture(extraRefForB: @"..\Ghost\Ghost.csproj");
        var graph = new ProjectGraphBuilder().Build(repo.Root, "test.slnx");

        Assert.Contains(graph.Findings, f => f.StartsWith("unresolved-reference:src/B/B.csproj->", StringComparison.Ordinal));
    }

    [Fact]
    public void A_dependency_cycle_is_a_finding()
    {
        using var repo = new GraphFixture(cycle: true);
        var graph = new ProjectGraphBuilder().Build(repo.Root, "test.slnx");

        Assert.Contains(graph.Findings, f => f.StartsWith("cycle-detected:", StringComparison.Ordinal));
    }

    private static string NameOf(string path) => Path.GetFileNameWithoutExtension(path);

    private static string Id(string name) => name;

    /// <summary>
    /// A throwaway repo: A &lt;- B &lt;- C (C depends on B depends on A); A.Tests covers A, C.Tests covers C.
    /// So C is a leaf (only C.Tests downstream) and A is shared (everything downstream).
    /// </summary>
    private sealed class GraphFixture : IDisposable
    {
        public string Root { get; }

        public GraphFixture(string? extraRefForB = null, bool cycle = false)
        {
            Root = Directory.CreateTempSubdirectory("hx-graph-").FullName;

            File.WriteAllText(Path.Combine(Root, "test.slnx"),
                """
                <Solution>
                  <Project Path="src/A/A.csproj" />
                  <Project Path="src/B/B.csproj" />
                  <Project Path="src/C/C.csproj" />
                  <Project Path="test/A.Tests/A.Tests.csproj" />
                  <Project Path="test/C.Tests/C.Tests.csproj" />
                </Solution>
                """);

            WriteProject("src/A/A.csproj", isTest: false, refs: cycle ? [@"..\C\C.csproj"] : []);
            WriteProject("src/B/B.csproj", isTest: false, refs: extraRefForB is null ? [@"..\A\A.csproj"] : [@"..\A\A.csproj", extraRefForB]);
            WriteProject("src/C/C.csproj", isTest: false, refs: [@"..\B\B.csproj"]);
            WriteProject("test/A.Tests/A.Tests.csproj", isTest: true, refs: [@"..\..\src\A\A.csproj"]);
            WriteProject("test/C.Tests/C.Tests.csproj", isTest: true, refs: [@"..\..\src\C\C.csproj"]);
        }

        private void WriteProject(string relativePath, bool isTest, string[] refs)
        {
            string full = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);

            var sb = new StringBuilder();
            sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
            if (isTest)
            {
                sb.AppendLine("  <PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup>");
            }

            if (refs.Length > 0)
            {
                sb.AppendLine("  <ItemGroup>");
                foreach (string reference in refs)
                {
                    sb.AppendLine($"    <ProjectReference Include=\"{reference}\" />");
                }

                sb.AppendLine("  </ItemGroup>");
            }

            sb.AppendLine("</Project>");
            File.WriteAllText(full, sb.ToString());
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); }
            catch (IOException) { /* best-effort temp cleanup */ }
        }
    }
}
