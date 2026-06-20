using Hx.Sentrux.Core;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class SentruxToolPathResolverTests
{
    [Theory]
    [InlineData("win-x64", "tools/sentrux/bin/win-x64/sentrux.exe")]
    [InlineData("linux-x64", "tools/sentrux/bin/linux-x64/sentrux")]
    [InlineData("linux-arm64", "tools/sentrux/bin/linux-arm64/sentrux")]
    [InlineData("osx-arm64", "tools/sentrux/bin/osx-arm64/sentrux")]
    public void ResolvesRepoRelativeToolPath(string rid, string expectedPath)
    {
        Assert.Equal(expectedPath, SentruxToolPathResolver.ResolveRepoRelativeToolPath(rid));
    }
}
