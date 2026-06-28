using Hx.Runner.Core.ArchitectureGate;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

/// <summary>
/// 014 (T003, FR-001/005/006): the ArchUnit capture path. The architecture tests EMIT a deterministic
/// <see cref="ArchitectureViolationMarker"/> block into their failure message; <see cref="ArchitectureTestRunner"/>
/// PARSES it back out of the TRX. Proven here over crafted TRX fixtures (no real gate failure needed) and with the
/// shared marker round-trip, so the emit and parse sides cannot drift.
/// </summary>
public sealed class ArchitectureViolationCaptureTests
{
    [Fact]
    public void Marker_round_trips_through_format_and_parse()
    {
        string message = ArchitectureViolationMarker.Format(
            "cliSurfaceConfinement",
            "Classes that reside in \".Cli\" should not have name ending with \"Service\"",
            ["Hx.X.Cli.FooService", "Hx.X.Cli.BarService"]);

        ArchitectureViolation violation = Assert.Single(ArchitectureViolationMarker.Parse(message));
        Assert.Equal("cliSurfaceConfinement", violation.Rule);
        Assert.Equal("Classes that reside in \".Cli\" should not have name ending with \"Service\"", violation.Description);
        Assert.Equal(["Hx.X.Cli.FooService", "Hx.X.Cli.BarService"], violation.ViolatingObjects);
        Assert.Null(violation.UnknownReason);
    }

    [Fact]
    public void Marker_round_trips_payloads_that_contain_the_delimiters()
    {
        // A rule/object that itself contains the delimiter substrings ( ; = # " ||" ) must survive escaping.
        string message = ArchitectureViolationMarker.Format(
            "rule;with=specials # ||embedded",
            "desc ||with; delimiters = and #",
            ["A;B", "C=D ||E"]);

        ArchitectureViolation violation = Assert.Single(ArchitectureViolationMarker.Parse(message));
        Assert.Equal("rule;with=specials # ||embedded", violation.Rule);
        Assert.Equal("desc ||with; delimiters = and #", violation.Description);
        Assert.Equal(["A;B", "C=D ||E"], violation.ViolatingObjects);
    }

    [Fact]
    public void Parse_extracts_multiple_blocks_in_document_order()
    {
        string message =
            "preamble "
            + ArchitectureViolationMarker.Format("ruleA", "descA", ["one"])
            + " interleaved "
            + ArchitectureViolationMarker.Format("ruleB", "descB", ["two", "three"]);

        IReadOnlyList<ArchitectureViolation> violations = ArchitectureViolationMarker.Parse(message);

        Assert.Equal(2, violations.Count);
        Assert.Equal("ruleA", violations[0].Rule);
        Assert.Equal("ruleB", violations[1].Rule);
        Assert.Equal(["two", "three"], violations[1].ViolatingObjects);
    }

    [Fact]
    public void Trx_parse_populates_violations_for_a_failing_case_with_a_marker()
    {
        string marker = ArchitectureViolationMarker.Format(
            "cliSurfaceConfinement", "FooService should reside in core", ["Hx.X.Cli.FooService"]);
        string trx = WriteTrx(
            FailedResult("Hx.Architecture.Tests.ArchitectureTests.Cli_namespaces_carry_no_business_logic_types",
                $"Assert.True() Failure\nExpected: True\nActual:   False\n{marker}"));

        try
        {
            ArchitectureTestCase test = Assert.Single(ArchitectureTestRunner.ParseTrx(trx));
            Assert.Equal(StageOutcome.Fail, test.Outcome);
            ArchitectureViolation violation = Assert.Single(test.Violations!);
            Assert.Equal("cliSurfaceConfinement", violation.Rule);
            Assert.Equal(["Hx.X.Cli.FooService"], violation.ViolatingObjects);
            Assert.Null(violation.UnknownReason);
        }
        finally
        {
            File.Delete(trx);
        }
    }

    [Fact]
    public void Trx_parse_is_fail_closed_for_a_failing_case_with_no_marker()
    {
        // A failing test whose message carries NO parseable marker yields exactly one UnknownReason violation — never
        // an empty "no violations" (FR-005).
        string trx = WriteTrx(
            FailedResult("Hx.Architecture.Tests.ArchitectureTests.Something_failed",
                "Assert.True() Failure with no structural detail at all"));

        try
        {
            ArchitectureTestCase test = Assert.Single(ArchitectureTestRunner.ParseTrx(trx));
            Assert.Equal(StageOutcome.Fail, test.Outcome);
            ArchitectureViolation violation = Assert.Single(test.Violations!);
            Assert.Equal("Something_failed", violation.Rule);
            Assert.Empty(violation.ViolatingObjects);
            Assert.Equal("TRX failure message carried no structural detail", violation.UnknownReason);
        }
        finally
        {
            File.Delete(trx);
        }
    }

    [Fact]
    public void Trx_parse_carries_no_violations_for_a_passing_case()
    {
        string trx = WriteTrx(PassedResult("Hx.Architecture.Tests.ArchitectureTests.Cli_command_types_delegate"));

        try
        {
            ArchitectureTestCase test = Assert.Single(ArchitectureTestRunner.ParseTrx(trx));
            Assert.Equal(StageOutcome.Pass, test.Outcome);
            Assert.Null(test.Violations);
        }
        finally
        {
            File.Delete(trx);
        }
    }

    private const string TrxNs = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    private static string PassedResult(string testName) =>
        $"""<UnitTestResult testName="{testName}" outcome="Passed" />""";

    private static string FailedResult(string testName, string message) =>
        $"""
        <UnitTestResult testName="{testName}" outcome="Failed">
          <Output><ErrorInfo><Message>{System.Security.SecurityElement.Escape(message)}</Message></ErrorInfo></Output>
        </UnitTestResult>
        """;

    private static string WriteTrx(string resultXml)
    {
        string path = Path.Combine(Path.GetTempPath(), "hx-arch-violation-" + Guid.NewGuid().ToString("n") + ".trx");
        File.WriteAllText(path,
            $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <TestRun xmlns="{TrxNs}">
              <Results>
                {resultXml}
              </Results>
            </TestRun>
            """);
        return path;
    }
}
