using Hx.Runner.Core.Process;
using Hx.Runner.Core.Repository;
using Hx.Tooling.Contracts;

namespace Hx.Sentrux.Core;

public static partial class SentruxChecker
{
    private static void StageGrammars(string root, string rid, List<string> advisory)
    {
        // Verified path. Stage the vendored grammar so analysis is reproducible offline.
        IReadOnlyList<string> staged = SentruxGrammarStager.EnsureStaged(root, rid);
        if (staged.Count > 0)
        {
            advisory.Add($"Staged {staged.Count} vendored grammar(s) into the Sentrux plugins directory.");
        }
    }

    private static RulesCheck RunRules(string executable, string root)
    {
        ProcessRunResult checkRun = ProcessRunner.Run(SentruxProcessAdapter.Check(executable, root));
        SentruxOutputParser.CheckReport check = SentruxOutputParser.ParseCheck(checkRun.StandardOutput);
        StageOutcome outcome = check.Passed ? StageOutcome.Pass : StageOutcome.Fail;
        return new RulesCheck(outcome, check.QualitySignal, check.Violations, check.ViolationDetails);
    }

    private static bool TryMissingBaseline(
        string root,
        SentruxPolicy policy,
        ToolVerificationResult verification,
        RulesCheck rules,
        List<string> advisory,
        out SentruxCheckResult? result)
    {
        result = null;
        RepositoryPath baselinePath = RepositoryPathResolver.ResolveInside(root, policy.BaselinePath);
        if (File.Exists(baselinePath.FullPath))
        {
            return false;
        }

        advisory.Add($"Sentrux baseline missing at {policy.BaselinePath}; run `sentrux baseline` before the regression gate.");
        result = Build(StageOutcome.Blocked, verification, rules.Outcome, rules.Violations,
            rules.QualitySignal, null, policy.SignalToleranceBand, StageOutcome.Blocked, [], advisory,
            ruleViolationDetails: rules.ViolationDetails);
        return true;
    }

    private sealed record RulesCheck(
        StageOutcome Outcome,
        int? QualitySignal,
        IReadOnlyList<string> Violations,
        IReadOnlyList<SentruxViolation> ViolationDetails);
}
