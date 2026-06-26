using Hx.Cli.Kernel;
using Hx.Cycle.Core;
using Hx.Doti.Core;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Cli;

public static partial class ScaffoldCommands
{
    public static CliResult DotiInstall(CliMeta meta, string? targetRepo, string agentsCsv, bool force, string sourceDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetRepo))
        {
            return CliResults.Fail(meta, "doti install", ExitClass.Usage,
                [Diag.Of(ErrorCodes.Usage_InvalidArguments, "doti install requires an explicit --repo <target-directory>; it never defaults to the current directory.")]);
        }

        if (!DotiAgentTarget.TryParseCsv(agentsCsv, out IReadOnlyList<DotiAgentTarget> agents, out string? error))
        {
            return CliResults.Fail(meta, "doti install", ExitClass.Usage,
                [Diag.Of(ErrorCodes.Usage_InvalidArguments, error!)]);
        }

        string target = Path.GetFullPath(targetRepo);
        string? source = FindInstalledDotiSource(sourceDirectory);
        if (source is null)
        {
            return CliResults.Fail(meta, "doti install", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, "Could not locate installed .doti/core/skills.json beside hx.exe.")],
                "Doti install cannot run because the installed hx payload is incomplete.",
                nextActions:
                [
                    new CliNextAction(
                        "Repair hx installation",
                        "Reinstall the hx global tool (dotnet tool update -g Heurex.SpeckitDoti) or the Microsoft Store package so the .doti payload is present beside the executable.")
                ]);
        }

        DotiHookInspection hookPlan = HookInstaller.Inspect(target);
        if (hookPlan.Verdict == HookInstaller.VerdictExternal)
        {
            return CliResults.Fail(meta, "doti install", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, hookPlan.Message, target: hookPlan.HookPath)],
                "Doti install refused to overwrite a non-Doti pre-commit hook.",
                new { hook = hookPlan },
                nextActions:
                [
                    new CliNextAction(
                        "Review the existing hook",
                        $"Resolve or move the non-Doti pre-commit hook before retrying: {hookPlan.HookPath}")
                ]);
        }

        try
        {
            DotiInstallResult result = DotiInstallBootstrapper.Install(
                source,
                new DotiInstallBootstrapRequest(target, agents, Force: force));
            if (result.Outcome != StageOutcome.Pass)
            {
                return CliResults.FromStage(meta, "doti install", result.Outcome, $"Doti install into {target}.",
                    new { install = result, hook = hookPlan });
            }

            DotiHookInstallResult? hook = hookPlan.Verdict == HookInstaller.VerdictNotGitRepository
                ? null
                : HookInstaller.InstallIfSafe(target);
            if (hook is { Success: false })
            {
                return CliResults.Fail(meta, "doti install", ExitClass.Validation,
                    [Diag.Of(ErrorCodes.Validation_Failed, hook.Message, target: hook.Inspection.HookPath)],
                    "Doti install completed, but hook arming was blocked.",
                    new { install = result, hook });
            }

            string hookSummary = hook is null ? " Hook skipped because the target is not a Git repo." : " Hook armed.";
            string pathSummary =
                $" Classification: {result.Classification}; installed={result.Installed.Count}, preserved={result.Preserved.Count}, removed={result.Removed.Count}, skipped={result.Skipped.Count}, blocked={result.Blocked.Count}.";
            return CliResults.FromStage(meta, "doti install", result.Outcome,
                $"Doti install into {target}.{hookSummary}{pathSummary}", new { install = result, hook });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return CliResults.Fail(meta, "doti install", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, ex.Message)]);
        }
    }

    private static string? FindInstalledDotiSource(string sourceDirectory)
    {
        string candidate = Path.GetFullPath(sourceDirectory);
        return File.Exists(Path.Combine(candidate, ".doti", "core", "skills.json"))
            ? candidate
            : null;
    }
}
