using Hx.Runner.Core.Io;
using Hx.Runner.Core.Process;
using Hx.Runner.Core.Repository;
using Hx.Runner.Core.Tools;
using Hx.Tooling.Contracts;

namespace Hx.Sentrux.Core;

public static partial class SentruxManifestValidator
{
    private static void VerifyExecutableOrThrow(
        string repositoryRoot,
        string hostRuntimeIdentifier,
        SentruxManifest manifest,
        SentruxAsset asset,
        string forkStamp,
        List<string> checks,
        List<string> problems)
    {
        if (TryVerifyExecutable(repositoryRoot, hostRuntimeIdentifier, manifest, asset, forkStamp, checks, problems, out ToolVerificationResult? result))
        {
            throw new VerificationFailed(result!);
        }
    }

    private static bool TryVerifyExecutable(
        string repositoryRoot,
        string hostRuntimeIdentifier,
        SentruxManifest manifest,
        SentruxAsset asset,
        string forkStamp,
        List<string> checks,
        List<string> problems,
        out ToolVerificationResult? result)
    {
        result = null;
        string inRepoExe = RepositoryPathResolver.ResolveInside(repositoryRoot, asset.ExecutablePath).FullPath;
        string exeFullPath = ToolStoreResolver.ResolveOrFallback(
            Tool, manifest.ReleaseTag, hostRuntimeIdentifier, asset.ExecutableName, asset.ExecutableSha256 ?? string.Empty, inRepoExe);
        if (!File.Exists(exeFullPath))
        {
            problems.Add($"Sentrux executable is missing for {hostRuntimeIdentifier}: {asset.ExecutablePath}");
            result = Result(false, StageOutcome.Blocked, checks, problems,
                "Vendor the Sentrux executable for this RID, or disable Sentrux in rules/sentrux.json.");
            return true;
        }

        VerifyHash(exeFullPath, asset.ExecutableSha256, "executable", checks, problems);
        VerifyForkStamp(exeFullPath, forkStamp, checks, problems);
        return false;
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

    private static void VerifyRulesConfig(
        string repositoryRoot,
        SentruxPolicy policy,
        List<string> checks,
        List<string> problems)
    {
        RepositoryPath rulesPath = RepositoryPathResolver.ResolveInside(repositoryRoot, policy.RulesConfigPath);
        if (File.Exists(rulesPath.FullPath))
        {
            checks.Add($"{policy.RulesConfigPath} present");
            return;
        }

        problems.Add($"Native Sentrux config is missing: {policy.RulesConfigPath}");
    }
}
