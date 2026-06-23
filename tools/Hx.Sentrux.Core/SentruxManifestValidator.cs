using Hx.Tooling.Contracts;

namespace Hx.Sentrux.Core;

/// <summary>
/// Verifies the vendored Sentrux localization: manifest presence, MIT license,
/// Heurex fork identity, host-RID executable + hash, required features, required
/// grammars, and the native rules config. Fails closed (no fallback to a global
/// `sentrux`) when Sentrux is enabled.
/// </summary>
public static partial class SentruxManifestValidator
{
    public const string ManifestRelativePath = "tools/sentrux/sentrux.version.json";
    public const string Tool = "sentrux";

    public static ToolVerificationResult Verify(string repositoryRoot, string hostRuntimeIdentifier, SentruxPolicy policy)
    {
        List<string> checks = [];
        List<string> problems = [];
        try
        {
            SentruxManifest manifest = ReadManifestOrThrow(repositoryRoot, checks, problems);
            ValidateManifestMetadata(manifest, policy, checks, problems);
            SentruxAsset asset = SelectAssetOrThrow(manifest, hostRuntimeIdentifier, checks, problems);
            VerifyExecutableOrThrow(repositoryRoot, hostRuntimeIdentifier, manifest, asset, policy.ForkStamp, checks, problems);
            VerifyGrammars(repositoryRoot, hostRuntimeIdentifier, manifest, policy, checks, problems);
            VerifyRulesConfig(repositoryRoot, policy, checks, problems);

            bool verified = problems.Count == 0;
            return Result(verified, verified ? StageOutcome.Pass : StageOutcome.Fail, checks, problems);
        }
        catch (VerificationFailed ex)
        {
            return ex.Result;
        }
    }

    private static ToolVerificationResult Result(
        bool verified, StageOutcome outcome, List<string> checks, List<string> problems, string? message = null)
    {
        return new ToolVerificationResult(
            JsonContractDefaults.SchemaVersion, Tool, verified, outcome, checks, problems, message);
    }

    private sealed class VerificationFailed(ToolVerificationResult result) : Exception
    {
        public ToolVerificationResult Result { get; } = result;
    }
}
