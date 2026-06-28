using Hx.Cli.Kernel;
using Hx.Tooling.Contracts;
using Spectre.Console;
using Xunit;

namespace Hx.Cli.Kernel.Tests;

/// <summary>
/// 014 (T010, FR-004/006): the kernel renders the structural offenders UNDER each FAILING architecture-test/sentrux-*
/// ladder step as concise, deterministically ordered one-line summaries, capped with "+N more". A passing step shows
/// no offender lines (no false offenders). Rendered from the SAME <see cref="GateTrace"/> the JSON carries.
/// </summary>
public sealed class StructuralOffenderRenderTests
{
    private static string Render(GateTrace trace)
    {
        var buffer = new StringWriter();
        IAnsiConsole capture = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(buffer),
        });
        capture.Profile.Width = 200;
        CliRenderer.WriteGateSummary(capture, trace);
        return buffer.ToString();
    }

    private static GateStep Step(string name, StageOutcome outcome, string message) =>
        new(name, outcome, [new GateEvidence(name, message)], 5);

    private static GateTrace Trace(IReadOnlyList<GateStep> steps, params StructuralStepViolations[] structural) =>
        new(new GateScope(JsonContractDefaults.SchemaVersion, false, "code", []),
            new ChangeSummary(1, 0, 0, 0, 1, 0, ["src/a.cs"], [], false),
            null, steps, 50, GateEffectiveMode.Partial, structural);

    [Fact]
    public void Sentrux_offender_renders_function_file_line_and_measure_under_the_failing_step()
    {
        GateTrace trace = Trace(
            [Step("sentrux-check", StageOutcome.Fail, "regression fail")],
            new StructuralStepViolations("sentrux-check", [],
                [new SentruxViolation("max_cc", "tools/X/Bar.cs", "ProcessFoo", 42, "28", "25", "cc exceeded")]));

        string output = Render(trace);

        Assert.Contains("max_cc", output);
        Assert.Contains("ProcessFoo()", output);
        Assert.Contains("Bar.cs:42", output);
        Assert.Contains("(28 > 25)", output);
    }

    [Fact]
    public void Sentrux_unknown_location_renders_the_reason_not_a_fabricated_location()
    {
        GateTrace trace = Trace(
            [Step("sentrux-check", StageOutcome.Fail, "regression fail")],
            new StructuralStepViolations("sentrux-check", [],
                [new SentruxViolation("max_cc", null, null, null, null, null, "2 functions exceed",
                    "engine reported a summary-level violation without per-function location")]));

        string output = Render(trace);

        Assert.Contains("max_cc", output);
        Assert.Contains("location unknown", output);
        Assert.Contains("2 functions exceed", output);
    }

    [Fact]
    public void Architecture_offender_names_the_rule_and_the_violating_types()
    {
        GateTrace trace = Trace(
            [Step("architecture-test", StageOutcome.Fail, "11/12 passed; 2 families")],
            new StructuralStepViolations("architecture-test",
                [new ArchitectureViolation("cliSurfaceConfinement", "desc", ["Hx.X.Cli.FooService", "Hx.X.Cli.BarService"])],
                []));

        string output = Render(trace);

        Assert.Contains("cliSurfaceConfinement", output);
        Assert.Contains("FooService", output);
        Assert.Contains("BarService", output);
    }

    [Fact]
    public void Offender_lines_are_capped_with_an_overflow_marker_not_a_dump()
    {
        SentruxViolation[] many = Enumerable.Range(0, 9)
            .Select(i => new SentruxViolation("max_cc", $"F{i}.cs", $"Fn{i}", i, "30", "25", "m"))
            .ToArray();
        GateTrace trace = Trace(
            [Step("sentrux-check", StageOutcome.Fail, "regression fail")],
            new StructuralStepViolations("sentrux-check", [], many));

        string output = Render(trace);

        Assert.Contains("+4 more", output);     // 9 offenders, cap 5 → "+4 more"
        Assert.DoesNotContain("Fn8.cs", output); // nothing beyond the cap is rendered
    }

    [Fact]
    public void A_passing_structural_step_shows_no_offender_lines()
    {
        // The trace carries no structural violations for a passing run → no offender lines under the passing step.
        GateTrace trace = Trace([Step("architecture-test", StageOutcome.Pass, "12/12 passed; 2 families")]);

        string output = Render(trace);

        Assert.Contains("architecture test", output); // the ladder line is present
        Assert.DoesNotContain("↳", output);            // but no offender bullets
    }

    [Fact]
    public void Offenders_are_not_rendered_for_a_non_structural_step()
    {
        // A failing NON-structural step (e.g. hygiene) must not pick up structural offenders even if present in the
        // trace for a different step.
        GateTrace trace = Trace(
            [Step("hygiene", StageOutcome.Fail, "findings")],
            new StructuralStepViolations("sentrux-check", [],
                [new SentruxViolation("max_cc", "Bar.cs", "Fn", 1, "30", "25", "m")]));

        string output = Render(trace);

        Assert.DoesNotContain("max_cc", output);
    }
}
