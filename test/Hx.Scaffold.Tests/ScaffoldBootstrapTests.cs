using Hx.Scaffold.Core;
using Xunit;

namespace Hx.Scaffold.Tests;

public sealed class ScaffoldBootstrapTests
{
    [Fact]
    public void DefaultProfileIsDotnetCli()
    {
        Assert.Equal("dotnet-cli", ScaffoldBootstrap.DefaultProfile.Name);
        Assert.Equal("net10.0", ScaffoldBootstrap.DefaultProfile.TargetFramework);
    }

    [Fact]
    public void CreateRequestDefaultsToCodexAndClaude()
    {
        var request = ScaffoldBootstrap.CreateRequest("Sample", "Heurex", "out");

        Assert.Equal(["codex", "claude"], request.Agents);
    }
}
