using Hx.Runner.Core.Gitleaks;
using Xunit;

namespace Hx.Runner.Tests;

public sealed class GitleaksExitClassifierTests
{
    [Theory]
    [InlineData(0, true, GitleaksRunStatus.Clean)]   // exit 0 is clean regardless of any stray report
    [InlineData(0, false, GitleaksRunStatus.Clean)]
    [InlineData(1, true, GitleaksRunStatus.Findings)] // leaks found and reported
    public void ClassifiesCleanAndFindings(int exitCode, bool reportExists, GitleaksRunStatus expected)
    {
        Assert.Equal(expected, GitleaksExitClassifier.Classify(exitCode, reportExists));
    }

    [Theory]
    [InlineData(1, false)]  // leak exit code but no report => tool errored before writing (the old fail-open)
    [InlineData(2, false)]  // any other non-zero exit code
    [InlineData(2, true)]
    [InlineData(-1, false)]
    public void ClassifiesToolErrorsAsErrorSoTheScanFailsClosed(int exitCode, bool reportExists)
    {
        Assert.Equal(GitleaksRunStatus.Error, GitleaksExitClassifier.Classify(exitCode, reportExists));
    }
}
