using System.Diagnostics;
using System.IO.Compression;
using Hx.Scaffold.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Scaffold.Tests;

/// <summary>
/// 007 T027: the per-channel source-free install smoke, as a test. Packs the framework-dependent global tool
/// (<c>Heurex.SpeckitDoti</c>), <c>dotnet tool install</c>s it into a NO-SOURCE sandbox, and runs the DOCUMENTED
/// operator command path from there — <c>hx --help</c>, <c>version --json</c>, <c>prereq check --json</c>,
/// <c>new</c> (scaffold from the bundled payload), and <c>doti install</c>. Each returns the <c>CliResult</c>
/// envelope + exit 0; the running tool reports the <c>global-tool</c> channel + <c>installed</c> mode (so it
/// knows it is source-free); and the scaffolded repo carries NO Velopack stub and routes its workflow through
/// installed <c>hx</c>, not <c>dotnet run --project tools/Hx.Runner.Cli</c>.
///
/// The two template-shape assertions are TEST-FIRST: they FAIL against today's template and pass only once T042
/// re-bases the template on the source-free rules (a still-Velopack / source-vendoring template must fail this
/// smoke). Heavy (pack + tool install + scaffold), so it is gated behind <c>HX_INSTALL_SMOKE=1</c> and skipped on
/// the inner loop — the release/store CI workflows run the equivalent smoke (FR-020/023/024/045, SC-001/008/019).
/// </summary>
public sealed class InstallLocationSmokeTests
{
    private static bool Enabled => Environment.GetEnvironmentVariable("HX_INSTALL_SMOKE") == "1";

    [Fact]
    public void Installed_global_tool_runs_the_documented_source_free_command_path()
    {
        Assert.SkipUnless(Enabled, "Set HX_INSTALL_SMOKE=1 to run the heavy global-tool install smoke (dotnet pack + tool install).");

        string repoRoot = FindRepoRoot();
        string sandbox = Path.Combine(Path.GetTempPath(), "hx-install-smoke-" + Guid.NewGuid().ToString("n"));
        string packOut = Path.Combine(sandbox, "nupkg");
        string toolPath = Path.Combine(sandbox, "tools");
        string gen = Path.Combine(sandbox, "gen");
        IReadOnlyDictionary<string, string> env = NestedDotnetEnv();
        try
        {
            Directory.CreateDirectory(packOut);
            string csproj = Path.Combine(repoRoot, "tools", "Hx.Scaffold.Cli", "Hx.Scaffold.Cli.csproj");

            // 1. Pack the framework-dependent global tool via the two-phase anchored pack (FR-003): stages the
            //    payload, computes its manifest digest, and re-emits hx with the digest embedded as the anchor.
            (int ExitCode, string Output) pack = Run("dotnet", $"build \"{csproj}\" -c Release -t:PackAnchoredTool -p:PackageOutputPath=\"{packOut}\" --nologo", repoRoot, env);
            Assert.True(pack.ExitCode == 0, $"PackAnchoredTool failed:\n{Tail(pack.Output)}");

            string nupkg = Directory.EnumerateFiles(packOut, "Heurex.SpeckitDoti.*.nupkg").Single();
            string version = Path.GetFileName(nupkg)["Heurex.SpeckitDoti.".Length..^".nupkg".Length];

            // FR-003/C2: the packed executable MUST carry the payload-manifest digest as its anti-substitution
            // anchor (never null), and that embedded value MUST equal the shipped descriptor's digest — otherwise
            // HX_PAYLOAD_ROOT could redirect resolution to a self-consistent but unanchored payload.
            AssertPackedToolIsAnchored(nupkg);

            // 2. Install into a NO-SOURCE sandbox from the local feed (offline; framework-dependent).
            (int ExitCode, string Output) install = Run("dotnet",
                $"tool install Heurex.SpeckitDoti --version {version} --add-source \"{packOut}\" --tool-path \"{toolPath}\"",
                sandbox, env);
            Assert.True(install.ExitCode == 0, $"dotnet tool install failed:\n{Tail(install.Output)}");

            string hx = Path.Combine(toolPath, OperatingSystem.IsWindows() ? "hx.exe" : "hx");
            Assert.True(File.Exists(hx), $"installed hx not found at '{hx}'.");

            // 3. The DOCUMENTED source-free command path, run from the sandbox (no source on cwd). Each exits 0.
            Assert.True(Run(hx, "--help", sandbox, env).ExitCode == 0, "hx --help did not exit 0.");

            string ver = AssertCommandOk(hx, "version --json", sandbox, env, "version");
            Assert.Contains(DistributionChannelId.GlobalTool, ver);   // the installed tool knows it is the global-tool channel
            Assert.Contains(CommandMode.Installed, ver);              // ...running source-free in installed mode

            AssertCommandOk(hx, "prereq check --json", sandbox, env, "prereq check");
            AssertCommandOk(hx, $"new --name HxInstallSmoke --company Smoke --output \"{gen}\" --agents codex,claude --json", sandbox, env, "new");
            AssertCommandOk(hx, $"doti install --repo \"{gen}\" --json", sandbox, env, "doti install");

            // 4. Template-shape rules (TEST-FIRST — these fail until T042 re-bases the template): the scaffolded repo
            //    must carry NO Velopack stub and route its workflow through installed `hx`, not the runner source.
            Assert.False(SourceTreeContains(gen, "VelopackApp"),
                "the scaffolded repo carries a Velopack stub (must be removed by T042).");
            Assert.False(SourceTreeContains(gen, "dotnet run --project tools/Hx.Runner.Cli"),
                "the scaffolded repo's workflow invokes the runner from source instead of installed hx (FR-045 / T042).");
        }
        finally
        {
            TryDelete(sandbox);
        }
    }

    /// <summary>Run an installed-hx command; assert exit 0 and that it returned the CliResult envelope for that
    /// command (the <c>"command":"&lt;name&gt;"</c> signature). Returns the raw output for further assertions.</summary>
    private static string AssertCommandOk(string hx, string arguments, string cwd, IReadOnlyDictionary<string, string> env, string command)
    {
        (int ExitCode, string Output) r = Run(hx, arguments, cwd, env);
        Assert.True(r.ExitCode == 0, $"hx {command} exited {r.ExitCode}:\n{Tail(r.Output)}");
        Assert.Contains($"\"command\":\"{command}\"", r.Output);   // the CliResult envelope was emitted
        return r.Output;
    }

    /// <summary>FR-003/C2: assert the packed tool assembly carries the payload-manifest digest as its embedded
    /// anti-substitution anchor, and that the embedded digest equals the shipped descriptor's digest.</summary>
    private static void AssertPackedToolIsAnchored(string nupkg)
    {
        using ZipArchive archive = ZipFile.OpenRead(nupkg);
        ZipArchiveEntry manifestEntry = archive.Entries.First(e => e.Name == "payload.manifest.json");
        ZipArchiveEntry dllEntry = archive.Entries.First(
            e => e.Name == "Hx.Scaffold.Cli.dll" && e.FullName.Replace('\\', '/').Contains("/any/"));

        using StreamReader manifestReader = new(manifestEntry.Open());
        string expectedAnchor = PayloadRoot.Sha256OfText(manifestReader.ReadToEnd());

        using MemoryStream dllBytes = new();
        using (Stream dllStream = dllEntry.Open())
        {
            dllStream.CopyTo(dllBytes);
        }

        string dllText = System.Text.Encoding.Latin1.GetString(dllBytes.ToArray());
        Assert.Contains(PayloadTrustAnchor.MetadataKey, dllText);   // the [AssemblyMetadata] key is embedded...
        Assert.Contains(expectedAnchor, dllText);                   // ...with the shipped descriptor's digest as its value
    }

    /// <summary>Recursive text search over a scaffolded repo's SOURCE (skips bin/obj/.git and binary files), so a
    /// build artifact that merely embeds the needle does not mask or fake a template-shape assertion.</summary>
    private static bool SourceTreeContains(string root, string needle)
    {
        if (!Directory.Exists(root))
        {
            return false;
        }

        string[] textExtensions = [".cs", ".csproj", ".props", ".targets", ".slnx", ".sln", ".json", ".yml", ".yaml", ".md", ".ps1", ".sh", ".xml", ".config"];
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(root, file);
            if (rel.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || rel.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || rel.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || rel.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || rel.StartsWith(".git" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                continue;
            }

            if (!textExtensions.Contains(Path.GetExtension(file)))
            {
                continue;
            }

            if (File.ReadAllText(file).Contains(needle, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static (int ExitCode, string Output) Run(string fileName, string arguments, string workingDirectory, IReadOnlyDictionary<string, string> env)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (KeyValuePair<string, string> kv in env)
        {
            psi.Environment[kv.Key] = kv.Value;
        }

        using Process process = Process.Start(psi)!;
        // Drain both streams CONCURRENTLY (reading one to end first deadlocks when the child fills the other).
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }

    /// <summary>Build-server isolation for the nested dotnet pack/install/build the smoke spawns (hang prevention).</summary>
    private static Dictionary<string, string> NestedDotnetEnv()
    {
        string cache = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        return new Dictionary<string, string>
        {
            ["NUGET_PACKAGES"] = cache,
            ["MSBUILDDISABLENODEREUSE"] = "1",
            ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0",
        };
    }

    private static string Tail(string output, int chars = 1200) =>
        output.Length <= chars ? output : output[^chars..];

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
            // best-effort sandbox cleanup
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "scaffold-dotnet.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("scaffold-dotnet.slnx not found above the test output.");
    }
}
