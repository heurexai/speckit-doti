using System.Text.Json;
using Xunit;

namespace Hx.Templates.Tests;

/// <summary>
/// Heavy end-to-end smoke (the hybrid-testing path). Gated behind the
/// <c>HX_TEMPLATE_ROUNDTRIP=1</c> environment variable so it does not run on the default
/// inner loop. It packs the template, installs it into a SANDBOXED template home
/// (<c>DOTNET_CLI_HOME</c> in a temp dir — never the real machine), instantiates it, and
/// builds + tests the generated solution, then tears everything down.
/// </summary>
public sealed class TemplateRoundTripTests
{
    private static bool Enabled =>
        Environment.GetEnvironmentVariable("HX_TEMPLATE_ROUNDTRIP") == "1";

    [Fact]
    public void Pack_install_instantiate_build_and_test_the_generated_solution()
    {
        Assert.SkipUnless(Enabled, "Set HX_TEMPLATE_ROUNDTRIP=1 to run the heavy template round-trip smoke.");

        string sandbox = Path.Combine(Path.GetTempPath(), "hx-tmpl-rt-" + Guid.NewGuid().ToString("n"));
        string cliHome = Path.Combine(sandbox, "clihome");
        string pkgOut = Path.Combine(sandbox, "pkg");
        string genOut = Path.Combine(sandbox, "gen", "Hx.Rt.Sample");
        Directory.CreateDirectory(cliHome);

        // DOTNET_CLI_HOME relocates the template-engine install root, so install/uninstall
        // never touch the real machine's templates. But it ALSO relocates the NuGet
        // global-packages cache to an empty dir under the sandbox; that would force restore to
        // hit configured package sources (and fail if any machine-global source is broken).
        // Pin NUGET_PACKAGES to the real cache so the generated solution restores from it.
        string realCache = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        var env = new Dictionary<string, string>
        {
            ["DOTNET_CLI_HOME"] = cliHome,
            ["NUGET_PACKAGES"] = realCache,
            // Nested dotnet must NOT spawn persistent build servers or reused MSBuild worker nodes.
            // Those grandchildren inherit our redirected stdout/stderr handles (UseShellExecute=false
            // propagates handles) and outlive the direct child, so the pipe never reaches EOF and the
            // output read blocks forever — an intermittent hang. Disabling node reuse and the MSBuild
            // server (plus --disable-build-servers on the building calls, which also stops the Roslyn
            // VBCSCompiler server) keeps every nested process a transient child of our call.
            ["MSBUILDDISABLENODEREUSE"] = "1",
            ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0",
        };

        try
        {
            (int code, string output) pack = TemplateRepo.RunDotnet(
                $"pack \"{TemplateRepo.PackProject}\" -c Release -o \"{pkgOut}\" --nologo --disable-build-servers", TemplateRepo.Root, env);
            Assert.True(pack.code == 0, "pack failed:\n" + pack.output);

            string nupkg = Directory.GetFiles(pkgOut, "*.nupkg").Single();

            (int code, string output) install = TemplateRepo.RunDotnet(
                $"new install \"{nupkg}\"", TemplateRepo.Root, env);
            Assert.True(install.code == 0, "install failed:\n" + install.output);

            try
            {
                (int code, string output) create = TemplateRepo.RunDotnet(
                    $"new hx-dotnet-cli -n Hx.Rt.Sample -o \"{genOut}\"", TemplateRepo.Root, env);
                Assert.True(create.code == 0, "instantiate failed:\n" + create.output);

                (int code, string output) test = TemplateRepo.RunDotnet(
                    "test Hx.Rt.Sample.slnx --nologo --disable-build-servers", genOut, env);
                Assert.True(test.code == 0, "generated solution build/test failed:\n" + test.output);

                // The generated solution must include and pass the ArchUnitNET architecture tests.
                Assert.Contains("Hx.Rt.Sample.Architecture.Tests", test.output);

                // The command-backed architecture gate over the generated repo. ArchitectureTestRunner
                // spawns a nested `dotnet test` with build-server isolation (no hang under the test host).
                Hx.Tooling.Contracts.ArchitectureTestResult arch =
                    Hx.Runner.Core.ArchitectureGate.ArchitectureTestRunner.Run(genOut);
                Assert.Equal(Hx.Tooling.Contracts.StageOutcome.Pass, arch.Outcome);
                Assert.Equal(8, arch.Families.Count);
                Assert.Equal(0, arch.FailedCount);
                Assert.True(arch.TestCount >= 8, $"expected the architecture families to run; got {arch.TestCount} tests");

                // The generated CLI is agent-first — greet emits a CliResult envelope; describe emits the
                // capability model. (Schema conformance of the envelope shape is proven in Hx.Cli.Kernel.Tests +
                // the always-on golden that checks Agent.cs declares every schema-required field.)
                (int code, string output) greet = TemplateRepo.RunDotnet(
                    "run --project src/Hx.Rt.Sample.Cli -- greet --name Rt --json", genOut, env);
                Assert.True(greet.code == 0, "generated greet failed:\n" + greet.output);
                JsonElement g = JsonDocument.Parse(ExtractJson(greet.output)).RootElement;
                Assert.Equal(1, g.GetProperty("schemaVersion").GetInt32());
                Assert.True(g.GetProperty("ok").GetBoolean());
                Assert.Equal("greet", g.GetProperty("command").GetString());
                Assert.Equal("success", g.GetProperty("outcome").GetString());
                Assert.True(g.GetProperty("data").TryGetProperty("greeting", out _));

                (int code, string output) describe = TemplateRepo.RunDotnet(
                    "run --project src/Hx.Rt.Sample.Cli -- describe --json", genOut, env);
                Assert.True(describe.code == 0, "generated describe failed:\n" + describe.output);
                JsonElement d = JsonDocument.Parse(ExtractJson(describe.output)).RootElement;
                Assert.True(d.GetProperty("ok").GetBoolean());
                Assert.True(d.GetProperty("data").TryGetProperty("commands", out _));
            }
            finally
            {
                TemplateRepo.RunDotnet("new uninstall Hx.Scaffold.Templates", TemplateRepo.Root, env);
            }
        }
        finally
        {
            TryDelete(sandbox);
        }
    }

    // `dotnet run` may print build/restore chatter before the program output; the envelope is the compact
    // single JSON line. Pick the first line that parses as a JSON object.
    private static string ExtractJson(string output) =>
        output.Split('\n').First(line => line.TrimStart().StartsWith('{'));

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // best-effort temp cleanup
        }
    }
}
