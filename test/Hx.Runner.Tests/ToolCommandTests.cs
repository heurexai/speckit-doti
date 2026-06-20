using Hx.Runner.Core.Process;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class ToolCommandTests
{
    [Fact]
    public void DotNetCommandUsesArgumentList()
    {
        ToolCommand command = ToolCommand.DotNet(".", "run", "--project", "tools/Hx.Runner.Cli", "--", "platform", "probe");

        var startInfo = command.ToStartInfo();

        Assert.Equal("dotnet", startInfo.FileName);
        Assert.False(startInfo.UseShellExecute);
        Assert.Equal(["run", "--project", "tools/Hx.Runner.Cli", "--", "platform", "probe"], startInfo.ArgumentList);
    }
}
