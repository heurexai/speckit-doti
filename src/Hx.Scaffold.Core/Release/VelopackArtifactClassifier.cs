namespace Hx.Scaffold.Core.Release;

public static class VelopackArtifactClassifier
{
    public static bool IsVelopackArtifactName(string name) => Classify(name) is not null;

    public static string? Classify(string name)
    {
        if (name.Equals("RELEASES", StringComparison.OrdinalIgnoreCase))
        {
            return "velopack-update-metadata";
        }

        if (name.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
        {
            return "velopack-package";
        }

        if ((name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
            || name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".dmg", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".deb", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".rpm", StringComparison.OrdinalIgnoreCase))
        {
            return "velopack-installer";
        }

        return null;
    }
}
