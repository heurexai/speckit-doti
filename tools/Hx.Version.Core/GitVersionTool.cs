namespace Hx.Version.Core;

/// <summary>
/// Wraps the vendored GitVersion CLI: verifies the pinned binary (manifest + MIT + RID asset + SHA-256,
/// the Gitleaks/Sentrux pattern), computes the version via <c>gitversion /output json</c>, and records an
/// operator-instructed major/minor bump as an annotated git tag. Fails closed when the binary is not
/// vendored/verified for the host RID — never guesses a version.
/// </summary>
public static partial class GitVersionTool
{
    public const string ManifestRelativePath = "tools/gitversion/gitversion.version.json";
    public const string Tool = "gitversion";
}
