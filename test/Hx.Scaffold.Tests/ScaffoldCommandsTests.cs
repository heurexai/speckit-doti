using Hx.Cli.Kernel;
using Hx.Scaffold.Cli;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Scaffold.Tests;

/// <summary>
/// Proves the Scaffold CLI's envelope mapping: <c>profile</c> succeeds; <c>new</c> validates its required
/// args up front (Usage, no generation). The heavy end-to-end <c>new</c> generation envelope is exercised by the
/// gated <see cref="ScaffoldNewSmokeTests"/> (HX_SCAFFOLD_SMOKE).
/// </summary>
public sealed class ScaffoldCommandsTests
{
    private static readonly CliMeta Meta = new("hx-scaffold", "1.0.0");

    [Fact]
    public void Profile_emits_the_default_profile()
    {
        CliResult r = ScaffoldCommands.Profile(Meta);
        Assert.True(r.Ok);
        Assert.Equal((int)ExitClass.Success, r.ExitCode);
        Assert.Equal("profile", r.Command);
        Assert.NotNull(r.Data);
    }

    [Theory]
    [InlineData("", "out")]
    [InlineData("Name", "")]
    public void New_requires_name_and_output(string name, string output)
    {
        CliResult r = ScaffoldCommands.New(Meta, name, "Heurex", output, "dotnet-cli", "codex,claude");
        Assert.False(r.Ok);
        Assert.Equal((int)ExitClass.Usage, r.ExitCode);
        Assert.Equal(ErrorCodes.Usage_InvalidArguments, Assert.Single(r.Errors).Code);
    }
}
