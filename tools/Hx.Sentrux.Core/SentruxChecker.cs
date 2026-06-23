using Hx.Runner.Core.Platform;
using Hx.Tooling.Contracts;

namespace Hx.Sentrux.Core;

/// <summary>
/// Merged Sentrux gate: tool verification, then absolute rule
/// check (`check --include-untracked --json`) plus quality-signal regression
/// (`gate`) against the committed baseline, with the operator tolerance band.
/// Fails closed when Sentrux is enabled but unverified.
/// </summary>
public static partial class SentruxChecker
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

        if (TryDisabled(policy, verification, advisory, out SentruxCheckResult? disabled))
        {
            return disabled!;
        }

        if (TryUnverified(policy, verification, advisory, out SentruxCheckResult? unverified))
        {
            return unverified!;
        }

        StageGrammars(root, rid, advisory);
        string executable = SentruxToolPathResolver.ResolveExecutable(root, rid);
        RulesCheck rules = RunRules(executable, root);
        if (TryMissingBaseline(root, policy, verification, rules, advisory, out SentruxCheckResult? missingBaseline))
        {
            return missingBaseline!;
        }

        RegressionCheck regression = RunRegression(executable, root, policy, rules.QualitySignal);

        StageOutcome outcome = Worst(verification.Outcome, rules.Outcome, regression.Outcome);
        return Build(outcome, verification, rules.Outcome, rules.Violations,
            rules.QualitySignal, regression.BaselineSignal, policy.SignalToleranceBand, regression.Outcome, regression.Notes, advisory);
    }

}
