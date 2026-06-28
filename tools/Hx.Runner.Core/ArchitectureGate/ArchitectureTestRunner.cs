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
            string[] projects = FindArchitectureTestProjects(repositoryRoot);
            if (projects.Length == 0)
            {
                return new ArchitectureTestResult(
                    JsonContractDefaults.SchemaVersion, StageOutcome.Skipped, 0, 0, 0, [], families,
                    ["No architecture test project found (includeArchitectureTests=false or no arch project)."]);
            }

            var tests = new List<ArchitectureTestCase>();
            var notes = new List<string>();
            bool commandFailed = false;
            foreach (string project in projects)
            {
                string logFileName = SafeLogFileName(project);
                var command = new ToolCommand(
                    "dotnet",
                    [
                        "test", project, "--nologo", "-c", "Release", "--no-build", "--no-restore", "--filter", Filter,
                        "--logger", $"trx;LogFileName={logFileName}",
                        "--results-directory", resultsDir,
                        "--disable-build-servers",
                    ],
                    repositoryRoot);

                ProcessRunResult run = ProcessRunner.Run(command, TestTimeout());
                commandFailed |= run.ExitCode != 0;

                string trx = Path.Combine(resultsDir, logFileName);
                if (!File.Exists(trx))
                {
                    commandFailed = true;
                    notes.Add("No TRX produced for " + RepositoryRelative(repositoryRoot, project)
                        + "; `dotnet test` failed before reporting results.");
                    notes.Add(Tail(run.StandardError + run.StandardOutput));
                    continue;
                }

                tests.AddRange(ParseTrx(trx));
            }

            int passed = tests.Count(t => t.Outcome == StageOutcome.Pass);
            int failed = tests.Count(t => t.Outcome == StageOutcome.Fail);

            StageOutcome outcome =
                tests.Count == 0 ? StageOutcome.Fail :
                (commandFailed || failed > 0) ? StageOutcome.Fail :
                StageOutcome.Pass;

            if (tests.Count == 0)
            {
                notes.Add("Architecture test project(s) were found, but no architecture tests were reported.");
            }

            return new ArchitectureTestResult(
                JsonContractDefaults.SchemaVersion, outcome, tests.Count, passed, failed, tests, families, notes);
        }
        finally
        {
            TryDelete(resultsDir);
        }
    }

    /// <summary>Parse a TRX file into per-`[Fact]` results, recovering each FAILING case's emitted offender detail
    /// (014, FR-001/005). Public for unit testing over a crafted TRX without shelling <c>dotnet test</c>.</summary>
    public static List<ArchitectureTestCase> ParseTrx(string trxPath)
    {
        XDocument doc = XDocument.Load(trxPath);
        return doc.Descendants(Trx + "UnitTestResult")
            .Select(ToTestCase)
            .ToList();
    }

    private static ArchitectureTestCase ToTestCase(XElement result)
    {
        string name = ShortName(result.Attribute("testName")?.Value ?? string.Empty);
        bool passed = result.Attribute("outcome")?.Value == "Passed";
        if (passed)
        {
            return new ArchitectureTestCase(name, StageOutcome.Pass);
        }

        // 014 (FR-001/005/006): recover the offender detail the test EMITTED into its failure message. Fail-closed —
        // a failing test whose message carries no parseable marker yields one UnknownReason violation, never an empty
        // "no violations".
        return new ArchitectureTestCase(name, StageOutcome.Fail, FailureViolations(name, result));
    }

    private static IReadOnlyList<ArchitectureViolation> FailureViolations(string testName, XElement result)
    {
        string message = result
            .Descendants(Trx + "ErrorInfo")
            .Elements(Trx + "Message")
            .Select(m => m.Value)
            .FirstOrDefault() ?? string.Empty;

        IReadOnlyList<ArchitectureViolation> parsed = ArchitectureViolationMarker.Parse(message);
        return parsed.Count > 0
            ? parsed
            : [new ArchitectureViolation(testName, "violation description not captured", [],
                "TRX failure message carried no structural detail")];
    }

    private static string ShortName(string fullyQualified)
    {
        int dot = fullyQualified.LastIndexOf('.');
        return dot >= 0 ? fullyQualified[(dot + 1)..] : fullyQualified;
    }

    private static string Tail(string text, int max = 800) =>
        text.Length <= max ? text : text[^max..];

    private static string[] FindArchitectureTestProjects(string repositoryRoot) =>
        Directory.GetFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(p => IsRootTestProject(repositoryRoot, p))
            .Where(p => Path.GetFileNameWithoutExtension(p).Contains("Architecture.Tests", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool IsRootTestProject(string repositoryRoot, string projectPath) =>
        RepositoryRelative(repositoryRoot, projectPath).StartsWith("test/", StringComparison.OrdinalIgnoreCase);

    private static string SafeLogFileName(string projectPath)
    {
        string name = Path.GetFileNameWithoutExtension(projectPath);
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '-');
        }

        return name + ".trx";
    }

    private static string RepositoryRelative(string repositoryRoot, string path) =>
        Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');

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

    private static TimeSpan TestTimeout()
    {
        string? configured = Environment.GetEnvironmentVariable("HX_ARCHITECTURE_TEST_TIMEOUT_SECONDS")
            ?? Environment.GetEnvironmentVariable("HX_GATE_TEST_TIMEOUT_SECONDS");
        return int.TryParse(configured, out int seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.FromSeconds(120);
    }
}
