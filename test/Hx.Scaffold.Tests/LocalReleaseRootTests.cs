using Hx.Cli.Kernel;
using Hx.Scaffold.Cli;
using Hx.Scaffold.Core.Release;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Scaffold.Tests;

public sealed class LocalReleaseRootTests
{
    [Fact]
    public void Explicit_root_wins_over_named_environment_variable()
    {
        LocalReleaseRootDecision decision = LocalReleaseRootResolver.Resolve(
            @"D:\releases",
            "CUSTOM_RELEASE_ROOT",
            name => throw new InvalidOperationException("environment should not be read: " + name));

        Assert.Equal("explicit", decision.Source);
        Assert.Equal(@"D:\releases", decision.ReleaseRoot);
        Assert.Equal("CUSTOM_RELEASE_ROOT", decision.EffectiveEnvironmentVariableName);
        Assert.False(decision.EnvironmentVariableRead);
        Assert.True(decision.EnvironmentVariableIgnored);
    }

    [Fact]
    public void Default_environment_variable_is_doti_release_root()
    {
        LocalReleaseRootDecision decision = LocalReleaseRootResolver.Resolve(
            null,
            null,
            name => name == "DOTI_RELEASE_ROOT" ? @"D:\releases" : null);

        Assert.Equal("default-environment", decision.Source);
        Assert.Equal("DOTI_RELEASE_ROOT", decision.EffectiveEnvironmentVariableName);
        Assert.Equal(@"D:\releases", decision.ReleaseRoot);
        Assert.True(decision.EnvironmentVariableRead);
    }

    [Fact]
    public void Named_environment_variable_is_read_when_no_explicit_root_is_provided()
    {
        LocalReleaseRootDecision decision = LocalReleaseRootResolver.Resolve(
            null,
            "HX_RELEASES",
            name => name == "HX_RELEASES" ? @"D:\hx-releases" : null);

        Assert.Equal("named-environment", decision.Source);
        Assert.Equal("HX_RELEASES", decision.EffectiveEnvironmentVariableName);
        Assert.Equal(@"D:\hx-releases", decision.ReleaseRoot);
    }

    [Fact]
    public void Save_release_root_requires_an_explicit_release_root()
    {
        CliResult result = ScaffoldCommands.Release(
            new CliMeta("hx", "0.0.0-test"),
            ".",
            "",
            "",
            "",
            saveReleaseRoot: true);

        Assert.False(result.Ok);
        Assert.Equal((int)ExitClass.Usage, result.ExitCode);
        Assert.Contains("--save-release-root requires", result.Errors.Single().Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("DOTI_RELEASE_ROOT")]
    [InlineData("_CUSTOM123")]
    public void Environment_variable_name_validation_accepts_safe_names(string name)
    {
        bool valid = string.IsNullOrEmpty(name) || LocalReleaseRootResolver.IsValidEnvironmentVariableName(name);
        Assert.True(valid);
    }

    [Theory]
    [InlineData("1BAD")]
    [InlineData("BAD-NAME")]
    [InlineData("BAD NAME")]
    public void Environment_variable_name_validation_rejects_unsafe_names(string name) =>
        Assert.False(LocalReleaseRootResolver.IsValidEnvironmentVariableName(name));
}
