using Hx.Runner.Core.Platform;
using Hx.Runner.Core.Process;
using Hx.Runner.Core.Repository;
using Hx.Tooling.Contracts;

namespace Hx.Sentrux.Core;

/// <summary>
/// Merged Sentrux gate: tool verification, then absolute rule
/// check (`check --include-untracked --json`) plus quality-signal regression
/// (`gate`) against the committed baseline, with the operator tolerance band.
/// Fails closed when Sentrux is enabled but unverified.
/// </summary>
public static class SentruxChecker
{
    public static SentruxCheckResult Check(string repositoryRoot)
    {
        string root = Path.GetFullPath(repositoryRoot);
        SentruxPolicy policy = SentruxPolicyLoader.Load(root, out bool usedDefault);
        string rid = HostPlatformDetector.DetectCurrent().RuntimeIdentifier;
        List<string> advisory = [];
        if (usedDefault)
        {
            advisory.Add("rules/sentrux.json not found; using the built-in default Sentrux policy.");
        }

        ToolVerificationResult verification = SentruxManifestValidator.Verify(root, rid, policy);

        if (!policy.SentruxEnabled)
        {
            advisory.Add("Sentrux is disabled in rules/sentrux.json.");
            return Build(StageOutcome.Skipped, verification, StageOutcome.Skipped, [], null, null,
                policy.SignalToleranceBand, StageOutcome.Skipped, ["Sentrux disabled."], advisory);
        }

        if (!verification.Verified)
        {
            advisory.Add("Sentrux structural gating is unavailable (fail-closed): "
                + (verification.Message ?? string.Join("; ", verification.Problems)));
            return Build(StageOutcome.Blocked, verification, StageOutcome.Skipped, [], null, null,
                policy.SignalToleranceBand, StageOutcome.Skipped, [], advisory);
        }

        // Verified path. Stage the vendored grammar so analysis is reproducible
        // offline and does not rely on a pre-existing global ~/.sentrux/plugins install.
        IReadOnlyList<string> staged = SentruxGrammarStager.EnsureStaged(root, rid);
        if (staged.Count > 0)
        {
            advisory.Add($"Staged {staged.Count} vendored grammar(s) into the Sentrux plugins directory.");
        }

        string executable = SentruxToolPathResolver.ResolveExecutable(root, rid);

        ProcessRunResult checkRun = ProcessRunner.Run(SentruxProcessAdapter.Check(executable, root));
        SentruxOutputParser.CheckReport check = SentruxOutputParser.ParseCheck(checkRun.StandardOutput);
        StageOutcome rulesOutcome = check.Passed ? StageOutcome.Pass : StageOutcome.Fail;

        // The regression gate needs a committed baseline; fail closed when it is absent.
        RepositoryPath baselinePath = RepositoryPathResolver.ResolveInside(root, policy.BaselinePath);
        if (!File.Exists(baselinePath.FullPath))
        {
            advisory.Add($"Sentrux baseline missing at {policy.BaselinePath}; run `sentrux baseline` before the regression gate.");
            return Build(StageOutcome.Blocked, verification, rulesOutcome, check.Violations,
                check.QualitySignal, null, policy.SignalToleranceBand, StageOutcome.Blocked, [], advisory);
        }

        ProcessRunResult gateRun = ProcessRunner.Run(SentruxProcessAdapter.GateCompare(executable, root));
        SentruxOutputParser.GateReport gate = SentruxOutputParser.ParseGate(gateRun.StandardOutput);

        int? before = gate.SignalBefore;
        int? after = gate.SignalAfter ?? check.QualitySignal;
        (StageOutcome regressionOutcome, int? delta) = SentruxRegression.Evaluate(before, after, policy.SignalToleranceBand);

        List<string> notes = [];
        if (before is null)
        {
            // Baseline exists but the gate signal could not be read — fail closed, never pass blindly.
            regressionOutcome = StageOutcome.Blocked;
            notes.Add("Could not read the Sentrux gate baseline signal; failing closed.");
        }
        else if (regressionOutcome == StageOutcome.Fail)
        {
            notes.Add($"Quality signal dropped {-delta!.Value} (> tolerance {policy.SignalToleranceBand}).");
        }

        if (gate.Degraded)
        {
            notes.Add("Sentrux gate reported structural degradation; absolute constraints are enforced by the rule check.");
        }

        StageOutcome outcome = Worst(verification.Outcome, rulesOutcome, regressionOutcome);
        return Build(outcome, verification, rulesOutcome, check.Violations,
            check.QualitySignal, before, policy.SignalToleranceBand, regressionOutcome, notes, advisory);
    }

    private static SentruxCheckResult Build(
        StageOutcome outcome, ToolVerificationResult verification, StageOutcome rulesOutcome,
        IReadOnlyList<string> violations, int? quality, int? baseline, int toleranceBand,
        StageOutcome regressionOutcome, IReadOnlyList<string> notes, IReadOnlyList<string> advisory)
    {
        int? delta = quality is not null && baseline is not null ? quality - baseline : null;
        return new SentruxCheckResult(
            JsonContractDefaults.SchemaVersion, outcome, verification, rulesOutcome, violations,
            quality, baseline, delta, toleranceBand, regressionOutcome, notes, advisory);
    }

    private static StageOutcome Worst(params StageOutcome[] outcomes)
    {
        if (outcomes.Contains(StageOutcome.Blocked)) return StageOutcome.Blocked;
        if (outcomes.Contains(StageOutcome.Fail)) return StageOutcome.Fail;
        if (outcomes.Contains(StageOutcome.Pass)) return StageOutcome.Pass;
        return StageOutcome.Skipped;
    }
}
