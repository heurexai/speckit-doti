using Hx.Runner.Core.Repository;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class RepositoryPathResolverTests
{
    [Fact]
    public void ResolveInsideReturnsRepoRelativeSlashPath()
    {
        string root = Path.GetFullPath("repo");

        RepositoryPath path = RepositoryPathResolver.ResolveInside(root, Path.Combine("tools", "sentrux", "manifest.json"));

        Assert.Equal("tools/sentrux/manifest.json", path.RelativePath);
    }

    [Fact]
    public void ResolveInsideRejectsPathOutsideRepository()
    {
        string root = Path.GetFullPath("repo");

        Assert.Throws<InvalidOperationException>(() => RepositoryPathResolver.ResolveInside(root, ".."));
    }

    [Fact]
    public void NormalizeManifestPathUsesForwardSlashes()
    {
        Assert.Equal("tools/sentrux/bin/win-x64/sentrux.exe", RepositoryPathResolver.NormalizeManifestPath(@"tools\sentrux\bin\win-x64\sentrux.exe"));
    }
}
