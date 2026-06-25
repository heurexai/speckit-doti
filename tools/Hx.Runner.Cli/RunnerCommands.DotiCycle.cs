using Hx.Cli.Kernel;
using Hx.Cycle.Core;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    public static CliResult CycleStamp(
        CliMeta meta,
        string repo,
        string stage,
        string feature,
        string baseRef,
        string releaseIntent = "")
    {
        if (string.IsNullOrWhiteSpace(stage))
        {
            return Usage(meta, "doti cycle stamp", "--stage is required.");
        }

        if (!string.IsNullOrWhiteSpace(releaseIntent))
        {
            if (!string.Equals(stage, "release", StringComparison.OrdinalIgnoreCase))
            {
                return Usage(meta, "doti cycle stamp", "--release-intent is only valid with --stage release.");
            }

            string normalized = releaseIntent.Trim().ToLowerInvariant();
            if (normalized is not ("major" or "minor" or "patch"))
            {
                return Usage(meta, "doti cycle stamp", "--release-intent must be major, minor, or patch.");
            }
        }

        try
        {
            CycleState state = new CycleService(repo).Stamp(
                stage,
                string.IsNullOrWhiteSpace(feature) ? null : feature,
                string.IsNullOrWhiteSpace(baseRef) ? null : baseRef,
                string.IsNullOrWhiteSpace(releaseIntent) ? null : releaseIntent);
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

    private static CliResult CycleCheckPassed(CliMeta meta, string stage, CycleCheckReport report) =>
        report.Completion is not null
            ? CliResults.Ok(meta, "doti cycle check", $"Cycle completed at {report.Completion.CommitSha}.", report)
            : CliResults.Ok(meta, "doti cycle check", $"All prerequisites for '{stage}' are stamped + fresh.", report);

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

}
