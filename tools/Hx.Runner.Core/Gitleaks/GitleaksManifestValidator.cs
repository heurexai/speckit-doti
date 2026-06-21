using System.Text.Json;
using Hx.Runner.Core.Io;
using Hx.Runner.Core.Platform;
using Hx.Runner.Core.Repository;
using Hx.Runner.Core.Tools;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Core.Gitleaks;

/// <summary>
/// Verifies the vendored Gitleaks localization: manifest presence, MIT license,
/// host-RID asset, executable hash, and rendered config hash. Fails closed (no
/// fallback to a global <c>gitleaks</c>) when Gitleaks is enabled.
/// </summary>
public static class GitleaksManifestValidator
{
    public const string ManifestRelativePath = "tools/gitleaks/gitleaks.version.json";
    public const string Tool = "gitleaks";

    public static ToolVerificationResult Verify(string repositoryRoot, string hostRuntimeIdentifier)
    {
        List<string> checks = [];
        List<string> problems = [];

        RepositoryPath manifestPath = RepositoryPathResolver.ResolveInside(repositoryRoot, ManifestRelativePath);
        if (!File.Exists(manifestPath.FullPath))
        {
            problems.Add($"Gitleaks manifest is missing: {ManifestRelativePath}");
            return Result(false, StageOutcome.Blocked, checks, problems,
                "Gitleaks is not vendored yet. Vendor a pinned release before enabling command-backed secret scanning.");
        }

        checks.Add("manifest present");

        GitleaksManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<GitleaksManifest>(
                File.ReadAllText(manifestPath.FullPath),
                JsonContractSerializerOptions.Create());
        }
        catch (JsonException ex)
        {
            problems.Add($"Gitleaks manifest is not valid JSON: {ex.Message}");
            return Result(false, StageOutcome.Fail, checks, problems);
        }

        if (manifest is null)
        {
            problems.Add("Gitleaks manifest is empty.");
            return Result(false, StageOutcome.Fail, checks, problems);
        }

        if (!string.Equals(manifest.License, "MIT", StringComparison.Ordinal))
        {
            problems.Add($"Gitleaks manifest license must be MIT, found '{manifest.License}'.");
        }
        else
        {
            checks.Add("license is MIT");
        }

        GitleaksAsset? asset = manifest.Assets.FirstOrDefault(a =>
            string.Equals(a.Rid, hostRuntimeIdentifier, StringComparison.OrdinalIgnoreCase));
        if (asset is null)
        {
            problems.Add($"No Gitleaks asset mapped for host RID '{hostRuntimeIdentifier}'.");
            return Result(false, StageOutcome.Blocked, checks, problems);
        }

        checks.Add($"asset mapped for {hostRuntimeIdentifier} ({asset.SupportLevel})");

        string inRepoExe = RepositoryPathResolver.ResolveInside(repositoryRoot, asset.ExecutablePath).FullPath;
        string exeFullPath = ToolStoreResolver.ResolveOrFallback(
            Tool, manifest.Version, hostRuntimeIdentifier, asset.ExecutableName, asset.ExecutableSha256 ?? string.Empty, inRepoExe);
        if (!File.Exists(exeFullPath))
        {
            problems.Add($"Gitleaks executable is missing for {hostRuntimeIdentifier}: {asset.ExecutablePath}");
            return Result(false, StageOutcome.Blocked, checks, problems,
                "Vendor the Gitleaks executable for this RID, or disable Gitleaks in rules/hygiene.json.");
        }

        if (!string.IsNullOrWhiteSpace(asset.ExecutableSha256))
        {
            string actual = FileHashing.Sha256OfFile(exeFullPath);
            if (!string.Equals(actual, asset.ExecutableSha256, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add("Gitleaks executable hash does not match the manifest.");
            }
            else
            {
                checks.Add("executable hash matches manifest");
            }
        }

        RepositoryPath configPath = RepositoryPathResolver.ResolveInside(repositoryRoot, manifest.ConfigPath);
        if (!File.Exists(configPath.FullPath))
        {
            problems.Add($"Rendered Gitleaks config is missing: {manifest.ConfigPath}");
        }
        else if (!string.IsNullOrWhiteSpace(manifest.ConfigSha256))
        {
            string actual = FileHashing.Sha256OfFile(configPath.FullPath);
            if (!string.Equals(actual, manifest.ConfigSha256, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add("Rendered Gitleaks config hash does not match the manifest.");
            }
            else
            {
                checks.Add("config hash matches manifest");
            }
        }

        bool verified = problems.Count == 0;
        return Result(verified, verified ? StageOutcome.Pass : StageOutcome.Fail, checks, problems);
    }

    private static ToolVerificationResult Result(
        bool verified,
        StageOutcome outcome,
        List<string> checks,
        List<string> problems,
        string? message = null)
    {
        return new ToolVerificationResult(
            JsonContractDefaults.SchemaVersion, Tool, verified, outcome, checks, problems, message);
    }
}
