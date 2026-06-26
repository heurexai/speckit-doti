using Hx.Doti.Core.Converge;
using Xunit;

namespace Hx.Doti.Tests;

/// <summary>007 T038 (FR-039): converge computes the requirement coverage gap (spec requirements no task covers).</summary>
public sealed class ConvergeServiceTests
{
    [Fact]
    public void Analyze_reports_spec_requirements_not_covered_by_any_task()
    {
        string spec = "- `FR-001`: A\n- `FR-002`: B\n- `SC-001`: C";
        string tasks =
            "- [ ] T001 — do FR-001 — `x` — [covers FR-001]\n" +
            "- [ ] T002 — do SC-001 — `y` — [covers SC-001]";

        ConvergeAnalysis analysis = ConvergeService.Analyze(spec, tasks);

        Assert.Equal(new[] { "FR-001", "FR-002", "SC-001" }, analysis.SpecRequirements);
        Assert.Equal(new[] { "FR-001", "SC-001" }, analysis.CoveredRequirements);
        Assert.Equal(new[] { "FR-002" }, analysis.UncoveredRequirements); // FR-002 is the unbuilt-work gap
    }

    [Fact]
    public void A_bare_requirement_mention_in_a_task_is_not_coverage()
    {
        string spec = "- `FR-007`: only mentioned in prose, never covered";
        // The task references FR-007 in its description but covers a different requirement.
        string tasks = "- [ ] T001 — touches the FR-007 area — `x` — [covers FR-999]";

        ConvergeAnalysis analysis = ConvergeService.Analyze(spec, tasks);

        Assert.Contains("FR-007", analysis.UncoveredRequirements);
        Assert.DoesNotContain("FR-007", analysis.CoveredRequirements);
    }

    [Fact]
    public void A_fully_covered_spec_has_no_gap()
    {
        ConvergeAnalysis analysis = ConvergeService.Analyze("`FR-001` `FR-002`", "[covers FR-001, FR-002]");

        Assert.Empty(analysis.UncoveredRequirements);
    }
}
