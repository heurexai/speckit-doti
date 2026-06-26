namespace Hx.Scaffold.Core.Release;

public static class LocalReleaseVersionPolicy
{
    /// <summary>
    /// 007 T041 (FR-044/SC-016): the cycle-type-aware default intent for a BLANK <c>hx release</c> intent.
    /// It follows the GitVersion-calculated bump, which the cycle's <c>+semver:</c> trailer already drove —
    /// <c>doti cycle stamp --stage release</c> writes <c>+semver: minor</c> for a feature cycle (so the calculated
    /// bump, and therefore this default, is <c>minor</c>), and a bug-fix-only cycle writes no minor signal (so the
    /// bump is <c>patch</c>). Following the bump keeps the release default in lockstep with the stamp default by
    /// construction — they cannot drift. With no previous tag the delta cannot be classified, so default to the
    /// feature-cycle intent (<c>minor</c>), consistent with FR-044's "minor for a normal feature cycle".
    /// </summary>
    public static string DefaultIntent(string? previousVersion, string currentVersion) =>
        string.IsNullOrWhiteSpace(previousVersion) ? "minor" : ClassifyVersionChange(previousVersion, currentVersion);

    public static void ValidateIntent(string? previousVersion, string currentVersion, string releaseIntent)
    {
        if (string.IsNullOrWhiteSpace(previousVersion))
        {
            return;
        }

        string actual = ClassifyVersionChange(previousVersion, currentVersion);
        if (!string.Equals(actual, releaseIntent, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Release intent mismatch: requested {releaseIntent}, but GitVersion calculated {currentVersion} from previous tag {previousVersion} as a {actual} release. " +
                "Add the appropriate GitVersion +semver commit-message signal before running hx release.");
        }
    }

    public static string ClassifyVersionChange(string previous, string current)
    {
        Semver p = ParseSemver(previous);
        Semver c = ParseSemver(current);
        if (c.Major > p.Major)
        {
            return "major";
        }

        if (c.Major == p.Major && c.Minor > p.Minor)
        {
            return "minor";
        }

        if (c.Major == p.Major && c.Minor == p.Minor && c.Patch >= p.Patch)
        {
            return "patch";
        }

        throw new InvalidOperationException(
            $"GitVersion calculated {current}, which is older than previous release tag {previous}.");
    }

    private static Semver ParseSemver(string value)
    {
        string core = value.Trim().TrimStart('v', 'V');
        int delimiter = core.IndexOfAny(['+', '-']);
        if (delimiter >= 0)
        {
            core = core[..delimiter];
        }

        string[] parts = core.Split('.');
        if (parts.Length < 3
            || !int.TryParse(parts[0], out int major)
            || !int.TryParse(parts[1], out int minor)
            || !int.TryParse(parts[2], out int patch))
        {
            throw new InvalidOperationException($"Release version '{value}' is not a three-part SemVer version.");
        }

        return new Semver(major, minor, patch);
    }

    private sealed record Semver(int Major, int Minor, int Patch);
}
