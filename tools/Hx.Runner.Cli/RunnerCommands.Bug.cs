using Hx.Cli.Kernel;
using Hx.Doti.Core.Bug;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    // 007 T033 (FR-034): thin CLI over BugCycleService — the assess/fix/test boundary + fail-closed enforcement live
    // in the service; the CLI maps a blocked stage to its registry error code.
    public static CliResult BugAssess(
        CliMeta meta, string repo, string bugId, string verdict, string severity, string remediation, string summary)
    {
        try
        {
            BugStageResult result = BugCycleService.Assess(repo,
                new BugAssessment(BugCycleService.SchemaVersion, bugId, verdict, severity, remediation, summary));
            return FromBugStage(meta, "doti bug assess", result, $"Bug {bugId} assessed ({verdict}).");
        }
        catch (ArgumentException ex)
        {
            return Usage(meta, "doti bug assess", ex.Message);
        }
    }

    public static CliResult BugFix(CliMeta meta, string repo, string bugId, string summary, string changedCsv)
    {
        try
        {
            // Auto-bind the fix to the current assessment; an absent assessment is reported by the service.
            string boundSha = BugCycleService.CurrentAssessmentSha(repo, bugId) ?? "";
            string[] changed = string.IsNullOrWhiteSpace(changedCsv)
                ? []
                : changedCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            BugStageResult result = BugCycleService.Fix(repo, bugId, boundSha, summary, changed);
            return FromBugStage(meta, "doti bug fix", result, $"Bug {bugId} fix recorded.");
        }
        catch (ArgumentException ex)
        {
            return Usage(meta, "doti bug fix", ex.Message);
        }
    }

    public static CliResult BugTest(CliMeta meta, string repo, string bugId, string outcome, string evidence)
    {
        try
        {
            BugStageResult result = BugCycleService.Test(repo, bugId, outcome, evidence);
            return FromBugStage(meta, "doti bug test", result, $"Bug {bugId} verified ({result.Outcome}).");
        }
        catch (ArgumentException ex)
        {
            return Usage(meta, "doti bug test", ex.Message);
        }
    }

    private static CliResult FromBugStage(CliMeta meta, string command, BugStageResult result, string summary)
    {
        if (result.Outcome == BugStageOutcome.Blocked)
        {
            string code = result.FailureCode == BugCycleService.CodeAssessmentMissing
                ? ErrorCodes.Validation_BugAssessmentMissing
                : ErrorCodes.Validation_BugFixUnbound;
            return CliResults.Fail(meta, command, ExitClass.Validation,
                [Diag.Of(code, result.FailureMessage ?? "Bug-cycle stage refused.")], data: result);
        }

        StageOutcome stage = result.Outcome == BugStageOutcome.Pass ? StageOutcome.Pass : StageOutcome.Fail;
        return CliResults.FromStage(meta, command, stage, summary, result);
    }

    // 034 (bug-only-release-doc-commit): thin CLI over BugReleaseDocCommit — the fail-closed gate (a release-ready
    // bug member must justify the commit) and the precise README.md/CHANGELOG.md staging live in the service.
    public static CliResult BugReleaseDocs(CliMeta meta, string repo, string bugIdCsv)
    {
        string[] bugIds = string.IsNullOrWhiteSpace(bugIdCsv)
            ? []
            : bugIdCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        BugReleaseDocCommitOutcome outcome = BugReleaseDocCommit.Commit(repo, bugIds);
        if (outcome.Status == DotiCommitStatus.Refused)
        {
            return CliResults.Fail(meta, "doti bug release-docs", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_BugReleaseDocsIneligible, outcome.Reason)], data: outcome);
        }

        if (outcome.Status == DotiCommitStatus.Failed)
        {
            // Parity with `doti update`'s 032 D1(c) arm: a self-owned sanctioned commit that failed AFTER the gate
            // passed leaves the docs staged-but-not-committed — an integrity condition, not a bad-input (Validation) one.
            return CliResults.Fail(meta, "doti bug release-docs", ExitClass.Integrity,
                [Diag.Of(ErrorCodes.Integrity_BugReleaseDocsCommitFailed, outcome.Reason ?? "release-documentation commit failed")],
                data: outcome);
        }

        string summary = outcome.Status switch
        {
            DotiCommitStatus.Committed => $"Committed {outcome.StagedPaths.Count} release-documentation path(s).",
            DotiCommitStatus.NoChange => "No release-documentation change to commit.",
            DotiCommitStatus.NonGit => "Target is not a git work tree; release-doc commit skipped.",
            _ => outcome.Reason ?? "Release-doc commit outcome.",
        };
        return CliResults.Ok(meta, "doti bug release-docs", summary, outcome);
    }
}
