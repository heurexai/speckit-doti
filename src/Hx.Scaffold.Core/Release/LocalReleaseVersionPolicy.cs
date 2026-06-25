namespace Hx.Scaffold.Core.Release;

public static class LocalReleaseVersionPolicy
{
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
