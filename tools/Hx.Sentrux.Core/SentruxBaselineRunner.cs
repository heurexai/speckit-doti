using Hx.Runner.Core.Platform;
using Hx.Runner.Core.Process;
using Hx.Runner.Core.Repository;
using Hx.Tooling.Contracts;

namespace Hx.Sentrux.Core;

/// <summary>
/// Creates/refreshes the Sentrux baseline (`sentrux gate --save` → `.sentrux/baseline.json`).
/// Allowed automatically only on the first scaffold smoke; otherwise an explicit
/// operator action. Fails closed when Sentrux is enabled but unverified.
/// </summary>
public static class SentruxBaselineRunner
{
    public static SentruxBaselineResult Save(string repositoryRoot)
    {
        string root = Path.GetFullPath(repositoryRoot);
        SentruxPolicy policy = SentruxPolicyLoader.Load(root, out _);
        string rid = HostPlatformDetector.DetectCurrent().RuntimeIdentifier;
        ToolVerificationResult verification = SentruxManifestValidator.Verify(root, rid, policy);

        if (!verification.Verified)
        {
            return new SentruxBaselineResult(
                JsonContractDefaults.SchemaVersion, StageOutcome.Blocked, verification, null,
                policy.BaselinePath,
                ["Cannot save a Sentrux baseline until the tool is verified (fail-closed)."]);
        }

        // Stage the vendored grammar first so the baseline is computed with the
        // pinned grammar, not a machine-global install.
        SentruxGrammarStager.EnsureStaged(root, rid);

        string executable = RepositoryPathResolver
            .ResolveInside(root, SentruxToolPathResolver.ResolveRepoRelativeToolPath(rid)).FullPath;

        ProcessRunResult run = ProcessRunner.Run(SentruxProcessAdapter.GateSave(executable, root));
        SentruxOutputParser.GateReport report = SentruxOutputParser.ParseGate(run.StandardOutput);
        StageOutcome outcome = run.ExitCode == 0 ? StageOutcome.Pass : StageOutcome.Fail;

        return new SentruxBaselineResult(
            JsonContractDefaults.SchemaVersion, outcome, verification,
            report.SignalAfter ?? report.SignalBefore, policy.BaselinePath,
            outcome == StageOutcome.Pass
                ? [$"Baseline written to {policy.BaselinePath}."]
                : ["`sentrux gate --save` did not complete successfully."]);
    }
}
