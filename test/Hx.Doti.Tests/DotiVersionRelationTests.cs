using Hx.Doti.Core;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Doti.Tests;

/// <summary>022 T013 (FR-002/020): the single-sourced relation — equal→Current, older→Outdated, newer→Ahead,
/// null→Unknown.</summary>
public sealed class DotiVersionRelationTests
{
    [Theory]
    [InlineData("1.2.3", "1.2.3", DotiVersionRelation.Current)]
    [InlineData("0.13.2", "0.13.5", DotiVersionRelation.Outdated)]
    [InlineData("1.0.0", "2.0.0", DotiVersionRelation.Outdated)]
    [InlineData("2.0.0", "1.0.0", DotiVersionRelation.Ahead)]
    [InlineData("0.13.5", "0.13.2", DotiVersionRelation.Ahead)]
    public void Relate_maps_semver_comparison(string repo, string tool, DotiVersionRelation expected) =>
        Assert.Equal(expected, DotiVersionRelationCalculator.Relate(repo, tool));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Null_or_blank_repo_version_is_unknown(string? repo) =>
        Assert.Equal(DotiVersionRelation.Unknown, DotiVersionRelationCalculator.Relate(repo, "1.0.0"));
}
