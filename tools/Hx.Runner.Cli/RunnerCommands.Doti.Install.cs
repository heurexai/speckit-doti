using Hx.Cli.Kernel;
using Hx.Cycle.Core;
using Hx.Doti.Core;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    public static CliResult DotiInstall(CliMeta meta, string? targetRepo, string agentsCsv, bool force, bool noCommit)
    {
        if (string.IsNullOrWhiteSpace(targetRepo))
        {
            return Usage(meta, "doti install", "doti install requires an explicit --repo <target-directory>; it never defaults to the current directory.");
        }

        if (!TryParseAgents(agentsCsv, out List<DotiAgentTarget> agents, out string? error))
        {
            return Usage(meta, "doti install", error!);
        }

        string target = Path.GetFullPath(targetRepo);
        // 031 FR-001/002: default the source to the running tool's bundled payload (fail closed if unresolvable).
        if (!TryResolveDotiPayloadSource(meta, "doti install", out string source, out string origin, out CliResult? failure))
        {
            return failure!;
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
                        $"Resolve or move the non-Doti pre-commit hook before retrying: {hookPlan.HookPath}"),
                ]);
        }

        DotiInstallResult result = DotiInstallBootstrapper.Install(
            source,
            new DotiInstallBootstrapRequest(target, agents, Force: force));
        if (result.Outcome != StageOutcome.Pass)
        {
            return CliResults.FromStage(meta, "doti install", result.Outcome, $"Doti install into {target}.",
                new { install = result, hook = hookPlan, source = origin });
        }

        DotiHookInstallResult? hook = hookPlan.Verdict == HookInstaller.VerdictNotGitRepository
            ? null
            : HookInstaller.InstallIfSafe(target);
        if (hook is { Success: false })
        {
            return CliResults.Fail(meta, "doti install", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, hook.Message, target: hook.Inspection.HookPath)],
                "Doti install completed, but hook arming was blocked.",
                new { install = result, hook, source = origin });
        }

        // 031 FR-007/008/009/010: the install owns its commit on the target — stage exactly the touched managed
        // paths (minus .new + gitignored) and make one sanctioned commit (default on; --no-commit opts out; a non-git
        // target skips with no error; no managed change → no commit).
        DotiReconcileCommitOutcome commitOutcome = DotiReconcileCommit.Commit(
            target, DotiReconcileCommit.TouchedPaths(result),
            beforeVersion: null, afterVersion: RepoPayloadStore.ReadPayloadVersion(target),
            prunedPaths: result.Removed.Select(e => e.Path.Replace('\\', '/')).ToList(), commit: !noCommit);

        // 035 (C): surface a FAILED self-commit — mirror `doti update`'s 032 D1(c) Integrity arm. The reconcile
        // succeeded but the commit did not (assets staged-but-uncommitted), so this must be ok:false/non-zero, not
        // swallowed under the Pass render outcome (the swallow the update command already guarded against, but install
        // — which shares the same commit primitive — did not).
        if (commitOutcome.Status == DotiCommitStatus.Failed)
        {
            return CliResults.Fail(meta, "doti install", ExitClass.Integrity,
                [Diag.Of(ErrorCodes.Integrity_DotiUpdateFailed, commitOutcome.Reason ?? "reconcile commit failed", target: target)],
                $"Doti install into {target} reconciled but the self-owned commit failed.",
                new { install = result, hook, source = origin, commit = commitOutcome });
        }

        string hookSummary = hook is null ? " Hook skipped because the target is not a Git repo." : " Hook armed.";
        string pathSummary =
            $" Classification: {result.Classification}; installed={result.Installed.Count}, preserved={result.Preserved.Count}, removed={result.Removed.Count}, skipped={result.Skipped.Count}, blocked={result.Blocked.Count}.";
        string commitSummary = commitOutcome.Status switch
        {
            DotiCommitStatus.Committed => $" Committed {commitOutcome.StagedPaths.Count} path(s).",
            DotiCommitStatus.Disabled => " --no-commit: changes left uncommitted.",
            DotiCommitStatus.NonGit => string.Empty,
            _ => string.Empty,
        };
        return CliResults.FromStage(meta, "doti install", result.Outcome,
            $"Doti install into {target}.{hookSummary}{pathSummary}{commitSummary}",
            new { install = result, hook, source = origin, commit = commitOutcome });
    }
}
