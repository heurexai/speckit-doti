using System.Text.Json;
using Hx.Runner.Core.Repository;
using Hx.Tooling.Contracts;

namespace Hx.Sentrux.Core;

public static partial class SentruxManifestValidator
{
    private static SentruxManifest ReadManifestOrThrow(
        string repositoryRoot,
        List<string> checks,
        List<string> problems)
    {
        if (TryReadManifest(repositoryRoot, checks, problems, out SentruxManifest? manifest, out ToolVerificationResult? result))
        {
            throw new VerificationFailed(result!);
        }

        return manifest!;
    }

    private static SentruxAsset SelectAssetOrThrow(
        SentruxManifest manifest,
        string hostRuntimeIdentifier,
        List<string> checks,
        List<string> problems)
    {
        if (TrySelectAsset(manifest, hostRuntimeIdentifier, checks, problems, out SentruxAsset? asset, out ToolVerificationResult? result))
        {
            throw new VerificationFailed(result!);
        }

        return asset!;
    }

    private static bool TryReadManifest(
        string repositoryRoot,
        List<string> checks,
        List<string> problems,
        out SentruxManifest? manifest,
        out ToolVerificationResult? result)
    {
        manifest = null;
        result = null;
        RepositoryPath manifestPath = RepositoryPathResolver.ResolveInside(repositoryRoot, ManifestRelativePath);
        if (!File.Exists(manifestPath.FullPath))
        {
            problems.Add($"Sentrux manifest is missing: {ManifestRelativePath}");
            result = Result(false, StageOutcome.Blocked, checks, problems,
                "Sentrux is not vendored yet. Vendor a pinned fork release + grammars before enabling command-backed structural gating.");
            return true;
        }

        checks.Add("manifest present");
        return TryDeserializeManifest(manifestPath, checks, problems, out manifest, out result);
    }

    private static bool TryDeserializeManifest(
        RepositoryPath manifestPath,
        List<string> checks,
        List<string> problems,
        out SentruxManifest? manifest,
        out ToolVerificationResult? result)
    {
        result = null;
        try
        {
            manifest = JsonSerializer.Deserialize<SentruxManifest>(
                File.ReadAllText(manifestPath.FullPath), JsonContractSerializerOptions.Create());
        }
        catch (JsonException ex)
        {
            manifest = null;
            problems.Add($"Sentrux manifest is not valid JSON: {ex.Message}");
            result = Result(false, StageOutcome.Fail, checks, problems);
            return true;
        }

        if (manifest is not null)
        {
            return false;
        }

        problems.Add("Sentrux manifest is empty.");
        result = Result(false, StageOutcome.Fail, checks, problems);
        return true;
    }

    private static void ValidateManifestMetadata(
        SentruxManifest manifest,
        SentruxPolicy policy,
        List<string> checks,
        List<string> problems)
    {
        ValidateLicense(manifest, checks, problems);
        ValidateDistributionIdentity(manifest, policy, checks, problems);
        ValidateRequiredFeatures(manifest, policy, problems);
    }

    private static void ValidateLicense(SentruxManifest manifest, List<string> checks, List<string> problems)
    {
        if (string.Equals(manifest.License, "MIT", StringComparison.Ordinal))
        {
            checks.Add("license is MIT");
            return;
        }

        problems.Add($"Sentrux manifest license must be MIT, found '{manifest.License}'.");
    }

    private static void ValidateDistributionIdentity(
        SentruxManifest manifest,
        SentruxPolicy policy,
        List<string> checks,
        List<string> problems)
    {
        if (manifest.DistributionIdentity.Contains(policy.ForkStamp, StringComparison.OrdinalIgnoreCase))
        {
            checks.Add($"distribution identity declares '{policy.ForkStamp}'");
            return;
        }

        problems.Add($"Sentrux manifest distribution identity must declare '{policy.ForkStamp}'.");
    }

    private static void ValidateRequiredFeatures(SentruxManifest manifest, SentruxPolicy policy, List<string> problems)
    {
        foreach (string feature in policy.RequiredFeatures)
        {
            if (!manifest.RequiredFeatures.Contains(feature, StringComparer.OrdinalIgnoreCase))
            {
                problems.Add($"Sentrux manifest does not declare required feature '{feature}'.");
            }
        }
    }

    private static bool TrySelectAsset(
        SentruxManifest manifest,
        string hostRuntimeIdentifier,
        List<string> checks,
        List<string> problems,
        out SentruxAsset? asset,
        out ToolVerificationResult? result)
    {
        asset = manifest.Assets.FirstOrDefault(a =>
            string.Equals(a.Rid, hostRuntimeIdentifier, StringComparison.OrdinalIgnoreCase));
        result = null;
        if (asset is not null)
        {
            checks.Add($"asset mapped for {hostRuntimeIdentifier} ({asset.SupportLevel})");
            return false;
        }

        problems.Add($"No Sentrux asset mapped for host RID '{hostRuntimeIdentifier}'.");
        result = Result(false, StageOutcome.Blocked, checks, problems);
        return true;
    }
}
