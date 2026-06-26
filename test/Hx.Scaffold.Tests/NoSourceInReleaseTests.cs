using System.IO.Compression;
using Hx.Cli.Kernel;
using Hx.Runner.Core.Packaging;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Scaffold.Tests;

/// <summary>
/// 007 T019 / FR-005/FR-006/SC-004: release packaging fails closed if a staged artifact carries the tool's build tree
/// — including when the marker is planted INSIDE the Hx.Scaffold.Templates .nupkg (a zip) — while the legitimate
/// runtime payload (template-pack content, .doti, manifests/grammars, hx.config.json, payload.manifest.json, and the
/// compiled Hx.*.dll) passes. A blanket src/ or .csproj ban would false-positive on the required template pack.
/// </summary>
public sealed class NoSourceInReleaseTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "hx-nosource-" + Guid.NewGuid().ToString("n"));

    public NoSourceInReleaseTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Violation_code_is_bound_to_the_registry()
    {
        Assert.Equal(ErrorCodes.Integrity_ReleaseArtifactContainsSource, ReleaseSourceInspector.ViolationCode);
    }

    [Theory]
    [InlineData("scaffold-dotnet.slnx", "tool-solution")]
    [InlineData("src/Hx.Scaffold.Core/TemplateGenerator.cs", "tool-source-tree")]
    [InlineData("tools/Hx.Scaffold.Cli/Hx.Scaffold.Cli.csproj", "tool-project-file")]
    public void Staged_tool_build_tree_is_caught(string relativeEntry, string expectedMarker)
    {
        Write(relativeEntry, "x");

        ReleaseSourceScanResult result = ReleaseSourceInspector.Scan(_root);

        Assert.Equal(StageOutcome.Fail, result.Outcome);
        Assert.Contains(result.Findings, f => f.Marker == expectedMarker);
    }

    [Fact]
    public void Tool_source_planted_inside_a_nupkg_is_caught()
    {
        WriteNupkg("Hx.Scaffold.Templates.1.0.0.nupkg", new Dictionary<string, string>
        {
            ["content/HxScaffoldSample/Program.cs"] = "// legit template content",
            ["src/Hx.Scaffold.Core/Leaked.cs"] = "// planted tool source",
        });

        ReleaseSourceScanResult result = ReleaseSourceInspector.Scan(_root);

        Assert.Equal(StageOutcome.Fail, result.Outcome);
        Assert.Contains(result.Findings, f => f.Marker == "tool-source-tree" && f.Artifact.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Legitimate_release_payload_passes()
    {
        Write("hx.exe", "MZ");
        Write("Hx.Scaffold.Core.dll", "compiled");                 // compiled runtime payload — allowed
        Write("payload.manifest.json", "{}");
        Write("hx.config.json", "{}");
        Write(".doti/core/skills.json", "{}");
        Write("tools/sentrux/sentrux.version.json", "{}");
        Write("tools/sentrux/grammars/csharp.scm", "grammar");
        WriteNupkg("Hx.Scaffold.Templates.1.0.0.nupkg", new Dictionary<string, string>
        {
            ["content/src/HxScaffoldSample/Program.cs"] = "// template source — HxScaffoldSample, no Hx. dot",
            ["content/src/HxScaffoldSample.Cli/HxScaffoldSample.Cli.csproj"] = "<Project/>",
        });

        ReleaseSourceScanResult result = ReleaseSourceInspector.Scan(_root);

        Assert.Equal(StageOutcome.Pass, result.Outcome);
        Assert.Empty(result.Findings);
        Assert.True(result.ScannedEntryCount > 0, "the scanner walked nothing");
    }

    private void Write(string relative, string content)
    {
        string full = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private void WriteNupkg(string name, Dictionary<string, string> entries)
    {
        string full = Path.Combine(_root, name);
        using FileStream stream = File.Create(full);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach ((string entryName, string content) in entries)
        {
            using StreamWriter writer = new(zip.CreateEntry(entryName).Open());
            writer.Write(content);
        }
    }
}
