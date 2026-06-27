using Hx.Cli.Kernel;
using Hx.Tooling.Contracts;
using Spectre.Console;
using Xunit;

namespace Hx.Cli.Kernel.Tests;

/// <summary>
/// 012 (T010): the kernel renders the gate trace (scope line, two-tier change summary, per-step ladder with
/// durations + terse reasons, total elapsed) from the SAME <see cref="GateTrace"/> the JSON carries (FR-014/015/
/// 017/018/019). All bounded — no full file/test/violation dump (FR-018).
/// </summary>
public sealed class GateTraceRenderTests
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

    private static GateStep Step(string name, StageOutcome outcome, string message, long ms) =>
        new(name, outcome, [new GateEvidence(name, message)], ms);

    [Fact]
    public void Docs_only_trace_shows_scope_and_no_tests_required()
    {
        var trace = new GateTrace(
            new GateScope(JsonContractDefaults.SchemaVersion, true, "docs-only", ["architecture-test", "sentrux-check"]),
            new ChangeSummary(0, 0, 2, 0, 8, 1, ["docs/a.md", "README.md"], [], false),
            null,
            [
                Step("affected-change", StageOutcome.Pass, "no test-affecting changes", 3),
                Step("architecture-test", StageOutcome.Skipped, "scope: docs-only change", 0),
            ],
            42,
            GateEffectiveMode.None);

        string output = Render(trace);

        Assert.Contains("docs-only", output);
        Assert.Contains("no tests required", output);
        Assert.Contains("architecture test", output);   // ladder shows the skipped step
        Assert.Contains("skipped", output);
        Assert.Contains("total:", output);
        Assert.Contains("42 ms", output);
        Assert.DoesNotContain("classes:", output);       // no detailed tier for docs-only
    }

    [Fact]
    public void Code_trace_shows_scope_mode_change_summary_and_ladder()
    {
        var trace = new GateTrace(
            new GateScope(JsonContractDefaults.SchemaVersion, false, "code", []),
            new ChangeSummary(3, 1, 0, 0, 40, 5, ["src/a.cs", "test/b.cs"], ["Alpha", "Beta"], true),
            new AffectedTestInventory(2, 7, 18, null, 4, null, "repo-wide total not enumerated"),
            [
                Step("hygiene", StageOutcome.Pass, "ok", 12),
                Step("restore-build-test", StageOutcome.Pass, "2 prebuilt affected test project(s) passed", 900),
                Step("sentrux-check", StageOutcome.Fail, "regression band", 30),
            ],
            980,
            GateEffectiveMode.Partial);

        string output = Render(trace);

        Assert.Contains("code", output);
        Assert.Contains("partial", output);              // effective mode
        Assert.Contains("src 3", output);                // basic change counts
        Assert.Contains("+40/-5", output);               // lines
        Assert.Contains("Alpha", output);                // classes touched (detailed tier)
        Assert.Contains("2/7 project(s)", output);       // selected/total test projects
        Assert.Contains("restore build test", output);   // ladder label
        Assert.Contains("900 ms", output);               // per-step duration
        Assert.Contains("regression band", output);      // failed step reason surfaced
        Assert.Contains("980 ms", output);               // total
    }

    [Fact]
    public void File_and_class_lists_are_bounded_with_overflow_marker_not_a_dump()
    {
        // The projector caps lists; the renderer must render the capped list incl. the "+N more" marker verbatim.
        var files = Enumerable.Range(0, 12).Select(i => $"src/F{i}.cs").Append("+8 more").ToArray();
        var trace = new GateTrace(
            new GateScope(JsonContractDefaults.SchemaVersion, false, "code", []),
            new ChangeSummary(20, 0, 0, 0, 100, 0, files, ["A", "B"], true),
            null,
            [Step("hygiene", StageOutcome.Pass, "ok", 1)],
            10,
            GateEffectiveMode.Partial);

        string output = Render(trace);

        Assert.Contains("+8 more", output);
        Assert.DoesNotContain("src/F20.cs", output); // nothing beyond the cap is present
    }

    [Fact]
    public void Unknown_class_case_total_is_surfaced_honestly()
    {
        var trace = new GateTrace(
            new GateScope(JsonContractDefaults.SchemaVersion, false, "code", []),
            new ChangeSummary(1, 0, 0, 0, 1, 0, ["src/a.cs"], ["A"], true),
            new AffectedTestInventory(1, 3, null, null, null, null, "built test assembly not found"),
            [Step("hygiene", StageOutcome.Pass, "ok", 1)],
            10,
            GateEffectiveMode.Partial);

        string output = Render(trace);

        Assert.Contains("1/3 project(s)", output);
        Assert.Contains("unknown", output);
    }
}
