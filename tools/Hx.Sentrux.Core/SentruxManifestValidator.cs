using System.Text.Json;
using Hx.Runner.Core.Io;
using Hx.Runner.Core.Process;
using Hx.Runner.Core.Repository;
using Hx.Runner.Core.Tools;
using Hx.Tooling.Contracts;

namespace Hx.Sentrux.Core;

/// <summary>
/// Verifies the vendored Sentrux localization: manifest presence, MIT license,
/// Heurex fork identity, host-RID executable + hash, required features, required
/// grammars, and the native rules config. Fails closed (no fallback to a global
/// `sentrux`) when Sentrux is enabled.
/// </summary>
public static class SentruxManifestValidator
{
    public const string ManifestRelativePath = "tools/sentrux/sentrux.version.json";
    public const string Tool = "sentrux";

    public static ToolVerificationResult Verify(string repositoryRoot, string hostRuntimeIdentifier, SentruxPolicy policy)
    {
        List<string> checks = [];
        List<string> problems = [];

        RepositoryPath manifestPath = RepositoryPathResolver.ResolveInside(repositoryRoot, ManifestRelativePath);
        if (!File.Exists(manifestPath.FullPath))
        {
            problems.Add($"Sentrux manifest is missing: {ManifestRelativePath}");
            return Result(false, StageOutcome.Blocked, checks, problems,
                "Sentrux is not vendored yet. Vendor a pinned fork release + grammars before enabling command-backed structural gating.");
        }

        checks.Add("manifest present");

        SentruxManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<SentruxManifest>(
                File.ReadAllText(manifestPath.FullPath), JsonContractSerializerOptions.Create());
        }
        catch (JsonException ex)
        {
            problems.Add($"Sentrux manifest is not valid JSON: {ex.Message}");
            return Result(false, StageOutcome.Fail, checks, problems);
        }

        if (manifest is null)
        {
            problems.Add("Sentrux manifest is empty.");
            return Result(false, StageOutcome.Fail, checks, problems);
        }

        if (string.Equals(manifest.License, "MIT", StringComparison.Ordinal))
        {
            checks.Add("license is MIT");
        }
        else
        {
            problems.Add($"Sentrux manifest license must be MIT, found '{manifest.License}'.");
        }

        if (manifest.DistributionIdentity.Contains(policy.ForkStamp, StringComparison.OrdinalIgnoreCase))
        {
            checks.Add($"distribution identity declares '{policy.ForkStamp}'");
        }
        else
        {
            problems.Add($"Sentrux manifest distribution identity must declare '{policy.ForkStamp}'.");
        }

        foreach (string feature in policy.RequiredFeatures)
        {
            if (!manifest.RequiredFeatures.Contains(feature, StringComparer.OrdinalIgnoreCase))
            {
                problems.Add($"Sentrux manifest does not declare required feature '{feature}'.");
            }
        }

        SentruxAsset? asset = manifest.Assets.FirstOrDefault(a =>
            string.Equals(a.Rid, hostRuntimeIdentifier, StringComparison.OrdinalIgnoreCase));
        if (asset is null)
        {
            problems.Add($"No Sentrux asset mapped for host RID '{hostRuntimeIdentifier}'.");
            return Result(false, StageOutcome.Blocked, checks, problems);
        }

        checks.Add($"asset mapped for {hostRuntimeIdentifier} ({asset.SupportLevel})");

        string inRepoExe = RepositoryPathResolver.ResolveInside(repositoryRoot, asset.ExecutablePath).FullPath;
        string exeFullPath = ToolStoreResolver.ResolveOrFallback(
            Tool, manifest.ReleaseTag, hostRuntimeIdentifier, asset.ExecutableName, asset.ExecutableSha256 ?? string.Empty, inRepoExe);
        if (!File.Exists(exeFullPath))
        {
            problems.Add($"Sentrux executable is missing for {hostRuntimeIdentifier}: {asset.ExecutablePath}");
            return Result(false, StageOutcome.Blocked, checks, problems,
                "Vendor the Sentrux executable for this RID, or disable Sentrux in rules/sentrux.json.");
        }

        VerifyHash(exeFullPath, asset.ExecutableSha256, "executable", checks, problems);
        VerifyGrammars(repositoryRoot, hostRuntimeIdentifier, manifest, policy, checks, problems);
        VerifyForkStamp(exeFullPath, policy.ForkStamp, checks, problems);

        RepositoryPath rulesPath = RepositoryPathResolver.ResolveInside(repositoryRoot, policy.RulesConfigPath);
        if (File.Exists(rulesPath.FullPath))
        {
            checks.Add($"{policy.RulesConfigPath} present");
        }
        else
        {
            problems.Add($"Native Sentrux config is missing: {policy.RulesConfigPath}");
        }

        bool verified = problems.Count == 0;
        return Result(verified, verified ? StageOutcome.Pass : StageOutcome.Fail, checks, problems);
    }

    private static void VerifyHash(string fullPath, string? expected, string label, List<string> checks, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return;
        }

        if (string.Equals(FileHashing.Sha256OfFile(fullPath), expected, StringComparison.OrdinalIgnoreCase))
        {
            checks.Add($"{label} hash matches manifest");
        }
        else
        {
            problems.Add($"Sentrux {label} hash does not match the manifest.");
        }
    }

    private static void VerifyGrammars(
        string repositoryRoot, string rid, SentruxManifest manifest, SentruxPolicy policy,
        List<string> checks, List<string> problems)
    {
        foreach (string required in policy.RequiredGrammars)
        {
            SentruxGrammar? grammar = manifest.Grammars.FirstOrDefault(g =>
                string.Equals(g.Name, required, StringComparison.OrdinalIgnoreCase)
                && string.Equals(g.Rid, rid, StringComparison.OrdinalIgnoreCase));
            if (grammar is null)
            {
                problems.Add($"Sentrux manifest does not vendor the '{required}' grammar for {rid}.");
                continue;
            }

            RepositoryPath grammarPath = RepositoryPathResolver.ResolveInside(repositoryRoot, grammar.Path);
            if (!File.Exists(grammarPath.FullPath))
            {
                problems.Add($"Sentrux grammar '{required}' is missing for {rid}: {grammar.Path}");
                continue;
            }

            VerifyHash(grammarPath.FullPath, grammar.Sha256, $"grammar '{required}'", checks, problems);
            checks.Add($"grammar '{required}' present for {rid}");
        }
    }

    private static void VerifyForkStamp(string executablePath, string forkStamp, List<string> checks, List<string> problems)
    {
        try
        {
            ProcessRunResult result = ProcessRunner.Run(new ToolCommand(
                executablePath,
                ["--version"],
                Path.GetDirectoryName(executablePath) ?? ".",
                new Dictionary<string, string> { ["SENTRUX_SKIP_GRAMMAR_DOWNLOAD"] = "1" }));

            if (result.StandardOutput.Contains(forkStamp, StringComparison.OrdinalIgnoreCase)
                || result.StandardError.Contains(forkStamp, StringComparison.OrdinalIgnoreCase))
            {
                checks.Add($"`sentrux --version` confirms '{forkStamp}'");
            }
            else
            {
                problems.Add($"`sentrux --version` did not confirm the '{forkStamp}' stamp.");
            }
        }
        catch (Exception ex)
        {
            problems.Add($"Could not run `sentrux --version`: {ex.Message}");
        }
    }

    private static ToolVerificationResult Result(
        bool verified, StageOutcome outcome, List<string> checks, List<string> problems, string? message = null)
    {
        return new ToolVerificationResult(
            JsonContractDefaults.SchemaVersion, Tool, verified, outcome, checks, problems, message);
    }
}
