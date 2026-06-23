namespace Hx.Version.Core;

internal sealed record SemVer(int Major, int Minor, int Patch, string? Prerelease)
{
    public static SemVer Parse(string version)
    {
        string value = StripBuildMetadata(version.Trim().TrimStart('v', 'V'));
        string? prerelease = ReadPrerelease(value, out string core);
        string[] parts = core.Split('.');
        return new SemVer(ParsePart(parts, 0), ParsePart(parts, 1), ParsePart(parts, 2), prerelease);
    }

    private static string StripBuildMetadata(string value)
    {
        int plus = value.IndexOf('+');
        return plus >= 0 ? value[..plus] : value;
    }

    private static string? ReadPrerelease(string value, out string core)
    {
        int dash = value.IndexOf('-');
        if (dash < 0)
        {
            core = value;
            return null;
        }

        core = value[..dash];
        return value[(dash + 1)..];
    }

    private static int ParsePart(string[] parts, int index) =>
        parts.Length > index && int.TryParse(parts[index], out int value) ? value : 0;
}
