using System.CommandLine;
using Hx.Cli.Kernel;
using Hx.Scaffold.Cli;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Scaffold.Tests;

/// <summary>
/// 007 FR-045: the installed <c>hx</c> must surface the runner + impact workflow command trees source-free, with the
/// two overlaps reconciled (one <c>doti</c> group, one <c>version</c>, one <c>describe</c>). These assertions walk the
/// composed describe model so a regression in <see cref="ScaffoldCommandFactory"/> composition is caught in-process.
/// </summary>
public sealed class UnifiedWorkflowSurfaceTests
{
    private static readonly CliMeta Meta = new("hx", "0.0.0-test");

    private static CliDescribe Describe()
    {
        RootCommand root = ScaffoldCommandFactory.Create(Meta, AppContext.BaseDirectory);
        return DescribeWalker.Describe(Meta, root, ErrorCodes.All);
    }

    [Theory]
    [InlineData("gate", "run")]
    [InlineData("impact", "plan")]
    [InlineData("sentrux", "check")]
    [InlineData("version", "calculate")]
    [InlineData("doti", "cycle")]
    public void Installed_hx_surfaces_workflow_group_and_subcommand(string group, string subcommand)
    {
        CliDescribeCommand parent = Describe().Root.Subcommands.Single(command => command.Name == group);
        Assert.Contains(parent.Subcommands, command => command.Name == subcommand);
    }

    [Theory]
    [InlineData("architecture")]
    [InlineData("security")]
    [InlineData("hygiene")]
    [InlineData("gate")]
    [InlineData("impact")]
    public void Installed_hx_surfaces_workflow_command(string command)
    {
        Assert.Contains(Describe().Root.Subcommands, sub => sub.Name == command);
    }

    [Fact]
    public void Doti_group_keeps_scaffold_install_after_merging_runner_subcommands()
    {
        CliDescribeCommand doti = Describe().Root.Subcommands.Single(command => command.Name == "doti");

        // The hx payload `install` (FR-002/019) wins the merge; the runner's cycle/payload/render-skills join it.
        Assert.Contains(doti.Subcommands, command => command.Name == "install");
        Assert.Contains(doti.Subcommands, command => command.Name == "cycle");
        Assert.Contains(doti.Subcommands, command => command.Name == "payload");
        Assert.Equal(1, doti.Subcommands.Count(command => command.Name == "install"));
    }

    [Fact]
    public void Version_command_carries_both_the_leaf_report_and_the_calculate_subcommand()
    {
        CliDescribeCommand version = Describe().Root.Subcommands.Single(command => command.Name == "version");

        // The scaffold leaf keeps its --repo report action; the runner's `calculate` joins as a subcommand.
        Assert.Contains(version.Options, option => option.Name == "--repo");
        Assert.Contains(version.Subcommands, command => command.Name == "calculate");
    }

    [Fact]
    public void Composition_leaves_exactly_one_describe_command()
    {
        // The runner's own `describe` is dropped so hx exposes a single capability surface.
        Assert.Equal(1, Describe().Root.Subcommands.Count(command => command.Name == "describe"));
    }
}
