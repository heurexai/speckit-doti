using Hx.Tooling.Contracts;
using Hx.Version.Core;

namespace Hx.Doti.Core;

/// <summary>
/// 022 T014 (FR-002/020): the SINGLE source of a Doti version relation. Maps the semver comparison
/// (<see cref="GitVersionTool.CompareVersions"/> — the same authority <c>version --repo</c> uses) onto the
/// <see cref="DotiVersionRelation"/> vocabulary: a repo equal to the tool is <see cref="DotiVersionRelation.Current"/>,
/// older is <see cref="DotiVersionRelation.Outdated"/> (an update is available), newer is
/// <see cref="DotiVersionRelation.Ahead"/> (never downgraded, FR-011), and an absent repo version is
/// <see cref="DotiVersionRelation.Unknown"/>.
/// </summary>
public static class DotiVersionRelationCalculator
{
    public static DotiVersionRelation Relate(string? repoVersion, string installedToolVersion)
    {
        if (string.IsNullOrWhiteSpace(repoVersion) || string.IsNullOrWhiteSpace(installedToolVersion))
        {
            return DotiVersionRelation.Unknown;
        }

        int compare = GitVersionTool.CompareVersions(repoVersion, installedToolVersion);
        return compare switch
        {
            0 => DotiVersionRelation.Current,
            < 0 => DotiVersionRelation.Outdated,
            _ => DotiVersionRelation.Ahead,
        };
    }
}
