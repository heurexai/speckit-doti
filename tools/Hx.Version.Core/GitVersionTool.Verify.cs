using System.Text.Json;
using Hx.Runner.Core.Io;
using Hx.Runner.Core.Repository;
using Hx.Runner.Core.Tools;
using Hx.Tooling.Contracts;

namespace Hx.Version.Core;

public static partial class GitVersionTool
{
    public static ToolVerificationResult Verify(string repositoryRoot, string hostRuntimeIdentifier)
    {
        List<string> checks = [];
        List<string> problems = [];

        VerifyContext context = ReadVerificationContext(repositoryRoot, hostRuntimeIdentifier, checks, problems);
        if (context.Result is not null)
        {
            return context.Result;
        }

        ToolVerificationResult? executableResult = VerifyExecutable(context, checks, problems);
        if (executableResult is not null)
        {
            return executableResult;
        }

        bool verified = problems.Count == 0;
        return Result(verified, verified ? StageOutcome.Pass : StageOutcome.Fail, checks, problems);
    }

    private static VerifyContext ReadVerificationContext(
        string repositoryRoot,
        string hostRuntimeIdentifier,
        List<string> checks,
        List<string> problems)
    {
        RepositoryPath manifestPath = RepositoryPathResolver.ResolveInside(repositoryRoot, ManifestRelativePath);
        if (TryReadManifestForVerify(manifestPath, checks, problems, out GitVersionManifest? manifest, out ToolVerificationResult? readResult))
        {
            return new VerifyContext(repositoryRoot, hostRuntimeIdentifier, null, null, readResult);
        }

        ValidateLicense(manifest!, checks, problems);
        if (TrySelectAsset(manifest!, hostRuntimeIdentifier, checks, problems, out GitVersionAsset? asset, out ToolVerificationResult? assetResult))
        {
            return new VerifyContext(repositoryRoot, hostRuntimeIdentifier, manifest, null, assetResult);
        }

        return new VerifyContext(repositoryRoot, hostRuntimeIdentifier, manifest, asset, null);
    }

    private static bool TryReadManifestForVerify(
        RepositoryPath manifestPath,
        List<string> checks,
        List<string> problems,
        out GitVersionManifest? manifest,
        out ToolVerificationResult? result)
    {
        manifest = null;
        result = null;
        if (!File.Exists(manifestPath.FullPath))
        {
            problems.Add($"GitVersion manifest is missing: {ManifestRelativePath}");
            result = Result(false, StageOutcome.Blocked, checks, problems,
                "GitVersion is not vendored yet. Vendor a pinned release before enabling version calculation.");
            return true;
        }

        checks.Add("manifest present");
        return TryDeserializeManifest(manifestPath, checks, problems, out manifest, out result);
    }

    private static bool TryDeserializeManifest(
        RepositoryPath manifestPath,
        List<string> checks,
        List<string> problems,
        out GitVersionManifest? manifest,
        out ToolVerificationResult? result)
    {
        result = null;
        try
        {
            manifest = JsonSerializer.Deserialize<GitVersionManifest>(
                File.ReadAllText(manifestPath.FullPath), JsonContractSerializerOptions.Create());
        }
        catch (JsonException ex)
        {
            manifest = null;
            problems.Add($"GitVersion manifest is not valid JSON: {ex.Message}");
            result = Result(false, StageOutcome.Fail, checks, problems);
            return true;
        }

        if (manifest is not null)
        {
            return false;
        }

        problems.Add("GitVersion manifest is empty.");
        result = Result(false, StageOutcome.Fail, checks, problems);
        return true;
    }

    private static void ValidateLicense(GitVersionManifest manifest, List<string> checks, List<string> problems)
    {
        if (!string.Equals(manifest.License, "MIT", StringComparison.Ordinal))
        {
            problems.Add($"GitVersion manifest license must be MIT, found '{manifest.License}'.");
            return;
        }

        checks.Add("license is MIT");
    }

    private static bool TrySelectAsset(
        GitVersionManifest manifest,
        string hostRuntimeIdentifier,
        List<string> checks,
        List<string> problems,
        out GitVersionAsset? asset,
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

        problems.Add($"No GitVersion asset mapped for host RID '{hostRuntimeIdentifier}'.");
        result = Result(false, StageOutcome.Blocked, checks, problems);
        return true;
    }

    private static ToolVerificationResult? VerifyExecutable(
        VerifyContext context,
        List<string> checks,
        List<string> problems)
    {
        string inRepoExe = RepositoryPathResolver.ResolveInside(context.RepositoryRoot, context.Asset!.ExecutablePath).FullPath;
        string exeFullPath = ToolStoreResolver.ResolveOrFallback(
            Tool,
            context.Manifest!.Version,
            context.HostRuntimeIdentifier,
            context.Asset.ExecutableName,
            context.Asset.ExecutableSha256 ?? string.Empty,
            inRepoExe);
        if (!File.Exists(exeFullPath))
        {
            problems.Add($"GitVersion executable is missing for {context.HostRuntimeIdentifier}: {context.Asset.ExecutablePath}");
            return Result(false, StageOutcome.Blocked, checks, problems,
                "Vendor the GitVersion executable for this RID (operational step), or version calculation stays advisory.");
        }

        VerifyExecutableHash(context.Asset, exeFullPath, checks, problems);
        return null;
    }

    private static void VerifyExecutableHash(
        GitVersionAsset asset,
        string exeFullPath,
        List<string> checks,
        List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(asset.ExecutableSha256))
        {
            return;
        }

        string actual = FileHashing.Sha256OfFile(exeFullPath);
        if (!string.Equals(actual, asset.ExecutableSha256, StringComparison.OrdinalIgnoreCase))
        {
            problems.Add("GitVersion executable hash does not match the manifest.");
            return;
        }

        checks.Add("executable hash matches manifest");
    }

    private static ToolVerificationResult Result(
        bool verified, StageOutcome outcome, List<string> checks, List<string> problems, string? message = null) =>
        new(JsonContractDefaults.SchemaVersion, Tool, verified, outcome, checks, problems, message);

    private sealed record VerifyContext(
        string RepositoryRoot,
        string HostRuntimeIdentifier,
        GitVersionManifest? Manifest,
        GitVersionAsset? Asset,
        ToolVerificationResult? Result);
}
