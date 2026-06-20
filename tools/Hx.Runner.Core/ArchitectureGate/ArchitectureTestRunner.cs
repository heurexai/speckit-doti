using System.Xml.Linq;
using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Core.ArchitectureGate;

/// <summary>
/// Runs a generated repo's ArchUnitNET architecture families as a command-backed gate: subprocess
/// <c>dotnet test</c> filtered to the architecture tests (with build-server isolation), parse the TRX
/// into per-`[Fact]` results, and report them alongside the families declared in
/// <c>rules/architecture.json</c>. The runner stays ArchUnitNET-free — the rules live in the
/// template's test project; this orchestrates and reports.
/// </summary>
public static class ArchitectureTestRunner
{
    private static readonly XNamespace Trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
    public const string Filter = "FullyQualifiedName~Architecture.Tests";

    public static ArchitectureTestResult Run(string repositoryRoot)
    {
        IReadOnlyList<string> families = ArchitectureContract.LoadFamilyIds(repositoryRoot);
        string resultsDir = Path.Combine(repositoryRoot, "obj", "hx-arch-" + Guid.NewGuid().ToString("n"));

        try
        {
            var env = new Dictionary<string, string>
            {
                ["MSBUILDDISABLENODEREUSE"] = "1",
                ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0",
                ["NUGET_PACKAGES"] = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
                    ?? System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages"),
            };

            var command = new ToolCommand(
                "dotnet",
                [
                    "test", "--nologo", "--filter", Filter,
                    "--logger", "trx;LogFileName=architecture.trx",
                    "--results-directory", resultsDir,
                    "--disable-build-servers",
                ],
                repositoryRoot,
                env);

            ProcessRunResult run = ProcessRunner.Run(command);

            string? trx = Directory.Exists(resultsDir)
                ? Directory.GetFiles(resultsDir, "*.trx").FirstOrDefault()
                : null;

            if (trx is null)
            {
                return new ArchitectureTestResult(
                    JsonContractDefaults.SchemaVersion, StageOutcome.Fail, 0, 0, 0, [], families,
                    ["No TRX produced; `dotnet test` failed before reporting results.", Tail(run.StandardError + run.StandardOutput)]);
            }

            List<ArchitectureTestCase> tests = ParseTrx(trx);
            int passed = tests.Count(t => t.Outcome == StageOutcome.Pass);
            int failed = tests.Count(t => t.Outcome == StageOutcome.Fail);

            StageOutcome outcome =
                tests.Count == 0 ? StageOutcome.Skipped :
                (run.ExitCode != 0 || failed > 0) ? StageOutcome.Fail :
                StageOutcome.Pass;

            var notes = new List<string>();
            if (tests.Count == 0)
            {
                notes.Add("No architecture tests found (includeArchitectureTests=false or no arch project).");
            }

            return new ArchitectureTestResult(
                JsonContractDefaults.SchemaVersion, outcome, tests.Count, passed, failed, tests, families, notes);
        }
        finally
        {
            TryDelete(resultsDir);
        }
    }

    private static List<ArchitectureTestCase> ParseTrx(string trxPath)
    {
        XDocument doc = XDocument.Load(trxPath);
        return doc.Descendants(Trx + "UnitTestResult")
            .Select(e => new ArchitectureTestCase(
                ShortName(e.Attribute("testName")?.Value ?? string.Empty),
                e.Attribute("outcome")?.Value == "Passed" ? StageOutcome.Pass : StageOutcome.Fail))
            .ToList();
    }

    private static string ShortName(string fullyQualified)
    {
        int dot = fullyQualified.LastIndexOf('.');
        return dot >= 0 ? fullyQualified[(dot + 1)..] : fullyQualified;
    }

    private static string Tail(string text, int max = 800) =>
        text.Length <= max ? text : text[^max..];

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
            // best-effort cleanup of the temp results dir
        }
    }
}
