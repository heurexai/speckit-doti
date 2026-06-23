using Hx.Cli.Kernel;
using Hx.Cycle.Core;
using Hx.Doti.Core;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    public static CliResult DotiInstall(CliMeta meta, string targetRepo, string agentsCsv)
    {
        if (!TryParseAgents(agentsCsv, out List<DotiAgentTarget> agents, out string? error))
        {
            return Usage(meta, "doti install", error!);
        }

        string target = Path.GetFullPath(targetRepo);
        string? source = FindDotiSource(Directory.GetCurrentDirectory());
        if (source is null)
        {
            return Usage(meta, "doti install", "Could not locate doti/core/skills.json above the current directory.");
        }

        string repoName = Path.GetFileName(target.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
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
                        $"Resolve or move the non-Doti pre-commit hook before retrying: {hookPlan.HookPath}"),
                ]);
        }

        DotiInstallResult result = DotiInstaller.Install(source, target, agents, repoName);
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
        return CliResults.FromStage(meta, "doti install", result.Outcome,
            $"Doti install into {target}.{hookSummary}", new { install = result, hook });
    }
}
