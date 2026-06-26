using System.Reflection;
using Hx.Cycle.Core;
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

    // 007 T041 (FR-044/SC-016): the blank-intent default follows the GitVersion-calculated bump, and that default
    // validates by construction — a feature cycle (minor bump from the +semver: minor trailer) defaults to minor,
    // a bug-fix-only cycle (patch bump, no signal) defaults to patch.
    [Fact]
    public void Default_feature_cycle_intent_is_minor_and_validates()
    {
        // The cycle stamp wrote +semver: minor, so GitVersion calculated 1.3.0 from 1.2.3.
        string intent = LocalReleaseVersionPolicy.DefaultIntent("1.2.3", "1.3.0");

        Assert.Equal("minor", intent);
        LocalReleaseVersionPolicy.ValidateIntent("1.2.3", "1.3.0", intent); // does not throw
    }

    [Fact]
    public void Default_bug_fix_only_cycle_intent_is_patch_and_validates()
    {
        // No minor signal, so GitVersion calculated a patch bump 1.2.4 from 1.2.3.
        string intent = LocalReleaseVersionPolicy.DefaultIntent("1.2.3", "1.2.4");

        Assert.Equal("patch", intent);
        LocalReleaseVersionPolicy.ValidateIntent("1.2.3", "1.2.4", intent); // does not throw
    }

    [Fact]
    public void Default_intent_with_no_previous_tag_is_the_feature_minor()
    {
        // First release: the delta cannot be classified, so default to the feature-cycle intent.
        Assert.Equal("minor", LocalReleaseVersionPolicy.DefaultIntent(null, "0.1.0"));
    }

    // 007 T041 (FR-044/SC-016): the cycle-stamp counterpart kept in lockstep — the release-stage transition's
    // default +semver signal is `minor` (feature cycle); an explicit intent overrides and is normalized.
    [Theory]
    [InlineData(null, "minor")]
    [InlineData("", "minor")]
    [InlineData("   ", "minor")]
    [InlineData("patch", "patch")]
    [InlineData("MAJOR", "major")]
    public void Release_transition_semver_signal_defaults_to_minor(string? intent, string expected) =>
        Assert.Equal(expected, CycleService.ReleaseSemverSignal(intent));
}
