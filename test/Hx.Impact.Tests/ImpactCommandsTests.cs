using Hx.Cli.Kernel;
using Hx.Impact.Cli;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Impact.Tests;

/// <summary>
/// Proves the Impact CLI's plan→envelope mapping: every outcome is a successful command (exit 0) and
/// the fail-closed <c>full-gate-required</c> escalation is surfaced as Direction, not a process failure. The kernel
/// envelope mechanics (LF/byte-identical/schema) are proven separately in <c>Hx.Cli.Kernel.Tests</c>.
/// </summary>
public sealed class ImpactCommandsTests
{
    private static readonly CliMeta Meta = new("hx-impact", "9.9.9");

    private static AffectedPlan PlanWith(string outcome, IReadOnlyList<SelectedTest> selected) =>
        new(JsonContractDefaults.SchemaVersion, outcome, [], selected, ["reason"]);

    [Fact]
    public void BootstrapPlan_emits_a_full_gate_success_envelope()
    {
        CliResult result = ImpactCommands.BootstrapPlan(Meta);

        Assert.Equal("hx-impact", result.Tool);
        Assert.Equal("bootstrap-plan", result.Command);
        Assert.True(result.Ok);
        Assert.Equal((int)ExitClass.Success, result.ExitCode);
        Assert.Equal(CliOutcome.Success, result.Outcome);
        Assert.False(result.RequiresOperator);
        Assert.NotNull(result.Data);
        CliNextAction action = Assert.Single(result.NextActions);
        Assert.Equal("gate run --profile normal", action.Command);
    }

    [Fact]
    public void FullGateRequired_is_a_success_with_the_full_gate_next_action()
    {
        CliResult result = ImpactCommands.FromPlan(Meta, "plan", PlanWith(AffectedOutcome.FullGateRequired, []));

        Assert.True(result.Ok);
        Assert.Equal((int)ExitClass.Success, result.ExitCode);
        Assert.Contains("Full gate required", result.Summary);
        Assert.Equal("gate run --profile normal", Assert.Single(result.NextActions).Command);
    }

    [Fact]
    public void Affected_selects_tests_and_directs_to_run_them()
    {
        SelectedTest test = new("Hx.Sample.Tests", "test/Hx.Sample.Tests/Hx.Sample.Tests.csproj", "dotnet test ...");
        CliResult result = ImpactCommands.FromPlan(Meta, "plan", PlanWith(AffectedOutcome.Affected, [test]));

        Assert.True(result.Ok);
        Assert.Contains("1 test project", result.Summary);
        CliNextAction action = Assert.Single(result.NextActions);
        Assert.Contains("selected", action.Label, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoTestsRequired_is_a_success_with_no_next_actions()
    {
        CliResult result = ImpactCommands.FromPlan(Meta, "plan", PlanWith(AffectedOutcome.NoTestsRequired, []));

        Assert.True(result.Ok);
        Assert.Equal((int)ExitClass.Success, result.ExitCode);
        Assert.Empty(result.NextActions);
    }

    // 007 T040 (FR-043): the same plan, presented for the /04-doti-arch-review audience.
    [Fact]
    public void ArchReview_audience_summarizes_changed_files_and_affected_projects()
    {
        AffectedPlan plan = new(JsonContractDefaults.SchemaVersion, AffectedOutcome.Affected, ["Hx.Foo"], [], ["reason"])
        {
            ChangedFiles = ["docs/x.md", "src/Foo/Bar.cs"],
        };

        CliResult result = ImpactCommands.FromPlan(Meta, "plan", plan, ImpactCommands.AudienceArchReview);

        Assert.True(result.Ok);
        Assert.Contains("2 changed file", result.Summary);
        Assert.Contains("1 affected source project", result.Summary);
        Assert.Contains("lens", Assert.Single(result.NextActions).Label, StringComparison.OrdinalIgnoreCase);
    }
}
