using Hx.Sentrux.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class SentruxManifestValidatorTests
{
    [Fact]
    public void MissingManifestFailsClosed()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-sx-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);

        try
        {
            ToolVerificationResult result = SentruxManifestValidator.Verify(dir, "win-x64", SentruxPolicy.Default());

            Assert.False(result.Verified);
            Assert.Equal(StageOutcome.Blocked, result.Outcome);
            Assert.Contains(result.Problems, p => p.Contains("manifest is missing", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
