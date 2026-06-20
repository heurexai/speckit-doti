using Hx.Runner.Core.Gitleaks;
using Hx.Runner.Core.Hygiene;
using Hx.Runner.Core.Io;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class GitleaksConfigRendererTests
{
    [Fact]
    public void RendersUseDefaultAndAllowlist()
    {
        string toml = GitleaksConfigRenderer.Render(HygienePolicy.Default());

        Assert.Contains("useDefault = true", toml);
        Assert.Contains("[allowlist]", toml);
    }

    [Fact]
    public void TextHashIsDeterministic()
    {
        Assert.Equal(FileHashing.Sha256OfText("scaffold-dotnet"), FileHashing.Sha256OfText("scaffold-dotnet"));
        Assert.NotEqual(FileHashing.Sha256OfText("a"), FileHashing.Sha256OfText("b"));
    }

    [Fact]
    public void RenderUsesLfOnlySoTheConfigHashIsPlatformStable()
    {
        // The rendered config is hashed and pinned in gitleaks.version.json; it must
        // be byte-identical on every platform, so it may never emit CRLF.
        string toml = GitleaksConfigRenderer.Render(HygienePolicy.Default());

        Assert.DoesNotContain("\r", toml);
        Assert.Contains("\n", toml);
    }
}
