using Hx.Gate.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>
/// 007 T018 / SC-005: the no-Velopack source scan fails closed if a Velopack package reference, a <c>VelopackApp</c>
/// startup hook, or a <c>vpk</c> invocation reappears in the product/runtime/release path (<c>tools/</c> + <c>src/</c>).
/// Includes a positive scan of the live tree plus planted-violation and false-positive-guard cases.
/// </summary>
public sealed class NoVelopackScanTests
{
    [Fact]
    public void Product_runtime_path_is_velopack_free()
    {
        NoVelopackScanResult result = NoVelopackScanner.Scan(RepoRoot());

        Assert.Equal(StageOutcome.Pass, result.Outcome);
        Assert.Empty(result.Findings);
        Assert.True(result.ScannedFileCount > 0, "the scanner found no product files to scan — scope is wrong");
    }

    [Fact]
    public void Scanner_catches_a_planted_package_reference()
    {
        string repo = PlantedRepo("tools/Hx.Demo.Cli/Hx.Demo.Cli.csproj",
            "<Project><ItemGroup><PackageReference Include=\"Velopack\" /></ItemGroup></Project>");
        try
        {
            NoVelopackScanResult result = NoVelopackScanner.Scan(repo);

            Assert.Equal(StageOutcome.Fail, result.Outcome);
            Assert.Contains(result.Findings, f => f.Kind == "package-reference");
        }
        finally
        {
            Directory.Delete(repo, recursive: true);
        }
    }

    [Fact]
    public void Scanner_catches_a_planted_velopackapp_hook_and_vpk_invocation()
    {
        string repo = PlantedRepo("src/Hx.Demo.Core/Bad.cs",
            "class Bad\n{\n    void Hook() { VelopackApp.Build().Run(); }\n    void Pack() { VelopackTool.Prepare(); }\n}");
        try
        {
            NoVelopackScanResult result = NoVelopackScanner.Scan(repo);

            Assert.Equal(StageOutcome.Fail, result.Outcome);
            Assert.Contains(result.Findings, f => f.Kind == "velopackapp-hook");
            Assert.Contains(result.Findings, f => f.Kind == "vpk-invocation");
        }
        finally
        {
            Directory.Delete(repo, recursive: true);
        }
    }

    [Fact]
    public void Scanner_does_not_flag_frozen_contract_fields_or_message_strings()
    {
        // The kept Velopack* contract field names (T004) and "Velopack"-mentioning help/message strings
        // (owned by T028/T030) must NOT be reported — the scan targets the three concrete violations only.
        string repo = PlantedRepo("tools/Hx.Demo.Contracts/Demo.cs",
            "record R(string VelopackPackageId, string VelopackChannel, object VelopackArtifacts); " +
            "// copy Velopack artifacts when configured; re-run the Velopack installer; retargeted off vpk (T028)");
        try
        {
            NoVelopackScanResult result = NoVelopackScanner.Scan(repo);

            Assert.Equal(StageOutcome.Pass, result.Outcome);
            Assert.Empty(result.Findings);
        }
        finally
        {
            Directory.Delete(repo, recursive: true);
        }
    }

    private static string PlantedRepo(string relativeFile, string content)
    {
        string repo = Path.Combine(Path.GetTempPath(), "hx-novelopack-" + Guid.NewGuid().ToString("n"));
        string full = Path.Combine(repo, relativeFile.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return repo;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "scaffold-dotnet.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("scaffold-dotnet.slnx not found above the test output.");
    }
}
