namespace Hx.Runner.Core.Hygiene;

/// <summary>
/// Stable scaffold-owned hygiene policy (the <c>rules/hygiene.json</c> surface).
/// Native tool config (gitleaks.toml) is rendered from this; it is never the
/// source of truth itself.
/// </summary>
public sealed record HygienePolicy(
    int SchemaVersion,
    bool GitleaksEnabled,
    string DefaultCommitScope,
    bool FullScanRequiredForRelease,
    int ChangedFileThreshold,
    bool GitleaksBaselineAllowed,
    IReadOnlyList<string> ScanPaths,
    IReadOnlyList<string> ExcludePaths,
    IReadOnlyList<string> AllowedUrlPrefixes,
    IReadOnlyList<string> LocalPathMarkers,
    IReadOnlyList<string> PrivateKeyMarkers,
    IReadOnlyList<string> BinaryExtensions,
    IReadOnlyList<string> ShellRunnerExtensions)
{
    public static HygienePolicy Default()
    {
        return new HygienePolicy(
            SchemaVersion: 1,
            GitleaksEnabled: true,
            DefaultCommitScope: "changed",
            FullScanRequiredForRelease: true,
            ChangedFileThreshold: 2000,
            GitleaksBaselineAllowed: false,
            ScanPaths: ["."],
            ExcludePaths:
            [
                ".git", "artifacts", "publish", "msix-layout", "bin", "obj",
                "tools/gitleaks/bin", "tools/sentrux/bin", "tools/sentrux/grammars",
                "tools/gitversion/bin", "rules/hygiene.json",
                "tools/Hx.Runner.Core/Hygiene/HygienePolicy.cs", "test/Hx.Runner.Tests/Fixtures"
            ],
            AllowedUrlPrefixes:
            [
                "https://github.com/",
                "https://api.github.com/",
                "https://keepachangelog.com/",
                "https://semver.org/",
                "https://learn.microsoft.com/",
                "https://aka.ms/",
                "https://developercertificate.org/",
                "https://www.nuget.org/",
                "https://dotnet.microsoft.com/",
                "https://archunitnet.readthedocs.io/",
                "https://json-schema.org/",
                "https://heurex.dev/",
                "http://json.schemastore.org/",
                "http://schemas.microsoft.com/"
            ],
            LocalPathMarkers:
            [
                "C:\\Users\\",
                "/Users/",
                "/home/",
                "OneDrive",
                "AppData",
                "\\Local\\Temp"
            ],
            PrivateKeyMarkers:
            [
                "-----BEGIN RSA PRIVATE KEY-----",
                "-----BEGIN PRIVATE KEY-----",
                "-----BEGIN OPENSSH PRIVATE KEY-----",
                "-----BEGIN EC PRIVATE KEY-----",
                "-----BEGIN PGP PRIVATE KEY BLOCK-----"
            ],
            BinaryExtensions: [".pfx", ".p12", ".snk", ".dll", ".exe", ".so", ".dylib"],
            ShellRunnerExtensions: [".ps1", ".psm1", ".sh", ".bash", ".cmd", ".bat"]);
    }
}
