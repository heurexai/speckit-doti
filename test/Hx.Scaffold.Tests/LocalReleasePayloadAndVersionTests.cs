using System.Reflection;
using Hx.Scaffold.Core.Release;
using Xunit;

namespace Hx.Scaffold.Tests;

/// <summary>
/// Channel-independent release behavior that survives the 007 T016 Velopack removal: the doti payload filter (runtime
/// cycle state is never staged into a release) and the GitVersion-calculated release-intent policy. The Velopack
/// packaging/install-smoke tests were dropped with Velopack; the no-Velopack regression gate lands in T018.
/// </summary>
public sealed class LocalReleasePayloadAndVersionTests
{
    [Theory]
    [InlineData(".doti/cycle-state.json", false)]
    [InlineData(".doti/gate-proof.json", false)]
    [InlineData(".doti/agent-context.md", true)]
    [InlineData(".doti/workflows/doti/workflow.yml", true)]
    public void Release_payload_excludes_runtime_cycle_state(string path, bool expected)
    {
        MethodInfo filter = typeof(LocalReleaseService).GetMethod(
            "IncludeDotiReleasePayloadFile",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Release payload filter was not found.");

        bool actual = (bool)filter.Invoke(null, [path])!;

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("1.2.3", "2.0.0", "major")]
    [InlineData("1.2.3", "1.3.0", "minor")]
    [InlineData("1.2.3", "1.2.4", "patch")]
    public void Release_version_policy_classifies_gitversion_calculated_increment(string previous, string current, string expected) =>
        Assert.Equal(expected, LocalReleaseVersionPolicy.ClassifyVersionChange(previous, current));

    [Fact]
    public void Release_version_policy_rejects_mismatched_operator_intent()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => LocalReleaseVersionPolicy.ValidateIntent("1.2.3", "1.3.0", "patch"));

        Assert.Contains("Release intent mismatch", ex.Message);
        Assert.Contains("requested patch", ex.Message);
        Assert.Contains("as a minor release", ex.Message);
        Assert.Contains("+semver", ex.Message);
    }
}
