using Hx.Runner.Core.Gitleaks;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class GitleaksManifestValidatorTests
{
    [Fact]
    public void MissingManifestFailsClosed()
    {
        string dir = Path.Combine(Path.GetTempPath(), "hx-gl-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);

        try
        {
            ToolVerificationResult result = GitleaksManifestValidator.Verify(dir, "win-x64");

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
