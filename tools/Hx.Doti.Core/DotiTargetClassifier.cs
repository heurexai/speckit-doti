namespace Hx.Doti.Core;

public static class DotiInstallClassification
{
    public const string InstalledNewTarget = "installed-new-target";
    public const string InstalledEmptyTarget = "installed-empty-target";
    public const string InstalledNonEmptyNonDotiTarget = "installed-non-empty-non-doti-target";
    public const string UpgradedExistingDotiRepo = "upgraded-existing-doti-repo";
}

public sealed record DotiTargetClassification(
    string Classification,
    bool Exists,
    bool Empty,
    bool HasDotiWorkflow,
    bool HasLegacyRootDoti,
    bool HasGitRepository,
    string Reason);

public static class DotiTargetClassifier
{
    public static DotiTargetClassification Classify(string targetRoot)
    {
        string full = Path.GetFullPath(targetRoot);
        bool exists = Directory.Exists(full);
        bool empty = !exists || !Directory.EnumerateFileSystemEntries(full).Any();
        bool hasDotiWorkflow = Directory.Exists(Path.Combine(full, ".doti"))
            || Directory.Exists(Path.Combine(full, ".agents", "skills"))
            || File.Exists(Path.Combine(full, ".doti", "integration.json"))
            || File.Exists(Path.Combine(full, ".doti", "managed-assets.json"));
        bool hasLegacyRootDoti = Directory.Exists(Path.Combine(full, "doti"));
        bool hasGit = Directory.Exists(Path.Combine(full, ".git")) || File.Exists(Path.Combine(full, ".git"));

        string classification =
            !exists ? DotiInstallClassification.InstalledNewTarget :
            empty ? DotiInstallClassification.InstalledEmptyTarget :
            hasDotiWorkflow || hasLegacyRootDoti ? DotiInstallClassification.UpgradedExistingDotiRepo :
            DotiInstallClassification.InstalledNonEmptyNonDotiTarget;

        string reason = classification switch
        {
            DotiInstallClassification.InstalledNewTarget => "target directory did not exist",
            DotiInstallClassification.InstalledEmptyTarget => "target directory existed and was empty",
            DotiInstallClassification.UpgradedExistingDotiRepo => "target already contained Doti workflow assets",
            _ => "target was non-empty and did not contain Doti workflow assets",
        };

        return new DotiTargetClassification(classification, exists, empty, hasDotiWorkflow, hasLegacyRootDoti, hasGit, reason);
    }
}
