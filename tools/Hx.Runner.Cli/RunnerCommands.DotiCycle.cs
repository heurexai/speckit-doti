using Hx.Cli.Kernel;
using Hx.Cycle.Core;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    public static CliResult CycleStamp(CliMeta meta, string repo, string stage, string feature, string baseRef)
    {
        if (string.IsNullOrWhiteSpace(stage))
        {
            return Usage(meta, "doti cycle stamp", "--stage is required.");
        }

        try
        {
            CycleState state = new CycleService(repo).Stamp(
                stage,
                string.IsNullOrWhiteSpace(feature) ? null : feature,
                string.IsNullOrWhiteSpace(baseRef) ? null : baseRef);
            return CliResults.Ok(meta, "doti cycle stamp", $"Stamped stage '{stage}'.", state);
        }
        catch (CycleInputException ex)
        {
            return CliResults.Fail(meta, "doti cycle stamp", ExitClass.Usage,
                [Diag.Of(ErrorCodes.Usage_InvalidArguments, ex.Message, target: "--feature")],
                "Invalid cycle feature slug.",
                nextActions:
                [
                    new CliNextAction(
                        "Use a numbered feature slug",
                        "Doti specs sort chronologically by the Spec Kit-style numeric prefix.",
                        "doti cycle stamp --stage specify --feature 001-my-feature"),
                ]);
        }
    }

    public static CliResult CycleStatus(CliMeta meta, string repo) =>
        // Non-enforcing: a STALE stage is reported in data, not gated.
        CliResults.Ok(meta, "doti cycle status", "Cycle status.", new CycleService(repo).Status());

    public static CliResult CycleCheck(CliMeta meta, string repo, string stage)
    {
        if (string.IsNullOrWhiteSpace(stage))
        {
            return Usage(meta, "doti cycle check", "--stage is required.");
        }

        CycleCheckReport report = new CycleService(repo).Check(stage);
        if (report.Passed)
        {
            return CycleCheckPassed(meta, stage, report);
        }

        List<Diagnostic> errors = report.Prerequisites
            .Where(p => !p.Ok)
            .Select(p => Diag.Of(ErrorCodes.Validation_Failed, $"{p.Stage}: {p.Status}" + (p.Reason is { } r ? $" ({r})" : ""), target: p.Stage))
            .ToList();
        return CliResults.Fail(meta, "doti cycle check", ExitClass.Validation, errors,
            $"Prerequisites for '{stage}' are not all fresh.", report);
    }

    public static CliResult CycleCommit(CliMeta meta, string repo, string message)
    {
        CycleCommitResult result = new CycleService(repo).Commit(message);
        if (result.Committed)
        {
            return CycleCommitSucceeded(meta, result);
        }

        if (result.AlreadyCompleted)
        {
            return CycleCommitAlreadyCompleted(meta, result);
        }

        List<Diagnostic> errors = result.Reasons
            .Select(r => Diag.Of(ErrorCodes.Validation_Failed, r))
            .ToList();
        return CliResults.Blocked(meta, "doti cycle commit", ExitClass.Validation, errors,
            "Commit refused: the sanctioned-commit prerequisites are not all met.", result,
            nextActions:
            [
                new CliNextAction("Resolve the listed blockers, then retry", "The commit chokepoint is fail-closed.", "doti cycle commit --message \"...\""),
                new CliNextAction("Re-run the gate if its proof is stale", "A fresh passing gate proof is required.", "gate run --profile normal"),
            ]);
    }

    private static CliResult CycleCheckPassed(CliMeta meta, string stage, CycleCheckReport report) =>
        report.Completion is not null
            ? CliResults.Ok(meta, "doti cycle check", $"Cycle completed at {report.Completion.CommitSha}.", report)
            : CliResults.Ok(meta, "doti cycle check", $"All prerequisites for '{stage}' are stamped + fresh.", report);

    private static CliResult CycleCommitSucceeded(CliMeta meta, CycleCommitResult result) =>
        CliResults.Ok(meta, "doti cycle commit", $"Committed {result.CommitSha}.", result,
            effects: [new CliEffect("commit", result.CommitSha ?? "HEAD", "sanctioned commit")]);

    private static CliResult CycleCommitAlreadyCompleted(CliMeta meta, CycleCommitResult result) =>
        CliResults.Ok(meta, "doti cycle commit", $"Cycle already completed at {result.CommitSha}.", result);

    public static CliResult PrecommitGuard(CliMeta meta)
    {
        if (PrecommitGuard_IsSanctioned())
        {
            return CliResults.Ok(meta, "doti cycle precommit-guard", "Sanctioned commit in progress.");
        }

        return CliResults.Fail(meta, "doti cycle precommit-guard", ExitClass.Usage,
            [Diag.Of(ErrorCodes.Usage_InvalidArguments, global::Hx.Cycle.Core.PrecommitGuard.RedirectMessage)],
            "Bare git commit is redirected to the sanctioned path.",
            nextActions: [new CliNextAction("Use the sanctioned commit path", "Bare commits are blocked by the insurance hook.", "doti cycle commit --message \"...\"")]);
    }

    public static CliResult InstallHooks(CliMeta meta, string repo)
    {
        DotiHookInstallResult result = HookInstaller.InstallIfSafe(repo);
        if (!result.Success)
        {
            return CliResults.Fail(meta, "doti install-hooks", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, result.Message, target: result.Inspection.HookPath)],
                "Insurance hook was not installed.", result,
                nextActions:
                [
                    new CliNextAction(
                        "Review the existing hook",
                        $"Doti will not overwrite a non-Doti pre-commit hook automatically: {result.Inspection.HookPath}"),
                ]);
        }

        IReadOnlyList<CliEffect> effects = result.Changed && result.Inspection.HookPath is not null
            ? [new CliEffect("write", result.Inspection.HookPath, "insurance pre-commit hook")]
            : [];
        return CliResults.Ok(meta, "doti install-hooks", result.Message, result, effects);
    }

    private static bool PrecommitGuard_IsSanctioned() => global::Hx.Cycle.Core.PrecommitGuard.IsSanctioned();
}
