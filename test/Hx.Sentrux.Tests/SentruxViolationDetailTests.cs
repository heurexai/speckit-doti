using Hx.Sentrux.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Sentrux.Tests;

/// <summary>
/// 014 (T006, FR-003/005): the Sentrux capture preserves a STRUCTURED offender (rule/file/function/line/value/limit)
/// alongside the legacy flattened <see cref="SentruxOutputParser.CheckReport.Violations"/> string list (unchanged). A
/// summary-style rule with no per-function attribution is surfaced as UnknownReason, never zero or a fabricated
/// location.
/// </summary>
public sealed class SentruxViolationDetailTests
{
    [Fact]
    public void Structures_a_per_object_violation_with_file_function_line_value_and_limit()
    {
        SentruxOutputParser.CheckReport report = SentruxOutputParser.ParseCheck(
            """
            {
              "passed": false,
              "qualitySignal": 0.61,
              "violations": [
                {
                  "rule": "max_cc",
                  "message": "function exceeds the cyclomatic-complexity limit",
                  "path": "tools/Hx.Runner.Cli/RunnerCommands.cs",
                  "function": "ProcessFoo",
                  "line": 42,
                  "value": 28,
                  "limit": 25
                }
              ]
            }
            """);

        SentruxViolation violation = Assert.Single(report.ViolationDetails);
        Assert.Equal("max_cc", violation.Rule);
        Assert.Equal("tools/Hx.Runner.Cli/RunnerCommands.cs", violation.File);
        Assert.Equal("ProcessFoo", violation.Function);
        Assert.Equal(42, violation.Line);
        Assert.Equal("28", violation.MeasuredValue);
        Assert.Equal("25", violation.Limit);
        Assert.Null(violation.UnknownReason);
    }

    [Fact]
    public void A_summary_style_violation_without_location_is_unknown_with_a_reason()
    {
        // The observed max_cc summary message: a count with no path AND no function → UnknownReason, never fabricated.
        SentruxOutputParser.CheckReport report = SentruxOutputParser.ParseCheck(
            """
            {
              "passed": false,
              "violations": [
                { "rule": "max_cc", "message": "2 function(s) exceed the complexity limit" }
              ]
            }
            """);

        SentruxViolation violation = Assert.Single(report.ViolationDetails);
        Assert.Equal("max_cc", violation.Rule);
        Assert.Null(violation.File);
        Assert.Null(violation.Function);
        Assert.Null(violation.Line);
        Assert.Equal("engine reported a summary-level violation without per-function location", violation.UnknownReason);
        Assert.Equal("2 function(s) exceed the complexity limit", violation.Message);
    }

    [Fact]
    public void A_string_only_violation_is_unstructured_with_a_reason()
    {
        SentruxOutputParser.CheckReport report = SentruxOutputParser.ParseCheck(
            """
            { "passed": false, "violations": [ "structural degradation detected" ] }
            """);

        SentruxViolation violation = Assert.Single(report.ViolationDetails);
        Assert.Equal("structural degradation detected", violation.Message);
        Assert.Equal("unstructured engine violation", violation.UnknownReason);
        Assert.Null(violation.File);
        Assert.Null(violation.Function);
    }

    [Fact]
    public void The_legacy_flattened_string_list_is_unchanged_and_parallel_to_the_structured_list()
    {
        SentruxOutputParser.CheckReport report = SentruxOutputParser.ParseCheck(
            """
            {
              "passed": false,
              "violations": [
                {
                  "rule": "god_file",
                  "message": "file has too many responsibilities",
                  "path": "tools/Hx.Runner.Cli/RunnerCommands.cs",
                  "line": 42,
                  "details": "split command orchestration from parsing",
                  "remediation": "move the core logic behind a service"
                }
              ]
            }
            """);

        // The string flatten is byte-for-byte the same as before 014.
        string flat = Assert.Single(report.Violations);
        Assert.Contains("god_file", flat, System.StringComparison.Ordinal);
        Assert.Contains("RunnerCommands.cs:42", flat, System.StringComparison.Ordinal);
        Assert.Contains("split command orchestration", flat, System.StringComparison.Ordinal);
        Assert.Contains("move the core logic", flat, System.StringComparison.Ordinal);

        // The structured list is parallel (one per parsed object) and carries the same file/line.
        SentruxViolation violation = Assert.Single(report.ViolationDetails);
        Assert.Equal("god_file", violation.Rule);
        Assert.Equal("tools/Hx.Runner.Cli/RunnerCommands.cs", violation.File);
        Assert.Equal(42, violation.Line);
    }

    [Fact]
    public void A_passing_check_yields_no_structured_violations()
    {
        SentruxOutputParser.CheckReport report = SentruxOutputParser.ParseCheck(
            """{ "passed": true, "qualitySignal": 0.73, "violations": [] }""");

        Assert.True(report.Passed);
        Assert.Empty(report.Violations);
        Assert.Empty(report.ViolationDetails);
    }
}
