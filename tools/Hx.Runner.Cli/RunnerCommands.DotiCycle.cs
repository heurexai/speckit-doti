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
            // 030 (bug-release-bridge): wire the bug-cycle members so the release-stage transition readiness counts a
            // test-passed /doti-bug mini-cycle (and a single drift-review-complete feature), consistent with `hx release`.
            CycleState state = new CycleService(repo, Hx.Doti.Core.Bug.BugCycleService.ReleaseReadyBugMembers).Stamp(
                stage,
                string.IsNullOrWhiteSpace(feature) ? null : feature,
                string.IsNullOrWhiteSpace(baseRef) ? null : baseRef,
                string.IsNullOrWhiteSpace(releaseIntent) ? null : releaseIntent);
            return CliResults.Ok(meta, "doti cycle stamp", $"Stamped stage '{stage}'.", state);
        }
        catch (CycleReviewRebindRequiredException ex)
        {
            // 028 FR-004/B1: the in-Stamp eligibility fence refused — route to the recorded reviewed-rebind verb,
            // not the feature-slug usage error.
            return CliResults.Fail(meta, "doti cycle stamp", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_CycleReviewRebindRequiresAttest, ex.Message, target: ex.Target)],
                $"Stage '{ex.Target}' needs a reviewed-no-impact attestation, not a bare stamp.",
                nextActions:
                [
                    new CliNextAction(
                        $"Record a reviewed-no-impact verdict for '{ex.Target}'",
                        "Read the surfaced upstream diff first; clearing the flag without assessing impact is forbidden.",
                        $"doti cycle review-rebind --target {ex.Target} --attest no-impact"),
                ]);
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

    public static CliResult CycleStatus(CliMeta meta, string repo)
    {
        // Non-enforcing: a STALE stage is reported in data, not gated. 028 FR-010: the agent's "what next" affordances
        // are projected from the action model (CliActionRendering), not hand-authored — the status surface carries the
        // valid workflow next-actions for the current decision point.
        // 030 (bug-release-bridge): the status train surfaces bug-cycle members so it agrees with `hx release`.
        var service = new CycleService(repo, Hx.Doti.Core.Bug.BugCycleService.ReleaseReadyBugMembers);
        CycleStatusReport report = service.Status();
        IReadOnlyList<CliNextAction> nextActions = WorkflowNextActions(repo, service, report);
        // 039 WI4/FR-033: no cycle-state is a dead-end. A cycle wedged at release-stage (its local state never
        // finalized — e.g. a dev→main→CI publish that bypassed MarkReleaseTrainReleased) surfaces the coded recovery.
        if (service.CanFinalizeReleasedCycle())
        {
            nextActions = [.. nextActions, new CliNextAction(
                "finalize-release",
                "This released cycle is wedged: its local state was never finalized (a dev→main→CI publish bypasses it). Finalize it so the next feature's specify can start.",
                "hx doti cycle finalize-release")];
        }

        return CliResults.Ok(meta, "doti cycle status", "Cycle status.", report, nextActions: nextActions);
    }

    // 039 WI4/FR-032: finalize a cycle wedged at the release stage so the next feature's specify can start. Idempotent;
    // fail-closed unless the cycle is at release-stage and a release tag exists (there is a shipped release to finalize).
    public static CliResult CycleFinalizeRelease(CliMeta meta, string repo)
    {
        var service = new CycleService(repo, Hx.Doti.Core.Bug.BugCycleService.ReleaseReadyBugMembers);
        try
        {
            CycleReleaseTrain train = service.FinalizeReleasedCycle();
            return CliResults.Ok(meta, "doti cycle finalize-release",
                "Finalized the released cycle; the next feature's specify can now start.", train);
        }
        catch (InvalidOperationException ex)
        {
            return CliResults.Fail(meta, "doti cycle finalize-release", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, ex.Message)]);
        }
    }

    public static CliResult CycleCheck(CliMeta meta, string repo, string stage)
    {
        if (string.IsNullOrWhiteSpace(stage))
        {
            return Usage(meta, "doti cycle check", "--stage is required.");
        }

        // 030 (bug-release-bridge): wire bug-cycle members so `doti cycle check release` agrees with `hx release`.
        var service = new CycleService(repo, Hx.Doti.Core.Bug.BugCycleService.ReleaseReadyBugMembers);
        CycleCheckReport report = service.Check(stage);
        if (report.Passed)
        {
            return CycleCheckPassed(meta, stage, report);
        }

        List<Diagnostic> errors = report.Prerequisites
            .Where(p => !p.Ok)
            .Select(p => Diag.Of(ErrorCodes.Validation_Failed, $"{p.Stage}: {p.Status}" + (p.Reason is { } r ? $" ({r})" : ""), target: p.Stage))
            .ToList();
        // FR-002: every failure carries the single recommended next command (a safe refresh, the agent-gated
        // reviewed-no-impact rebind, or re-running the stage). For an attest-eligible step the seam surfaces the exact
        // upstream diff the verdict needs (self-describing staleness) — computed lazily here, never in the pure leaf.
        List<CliNextAction> nextActions = service.RecoveryPlanFor(report).Steps
            .Select(s => RecoveryNextAction(repo, service, s))
            .ToList();
        return CliResults.Fail(meta, "doti cycle check", ExitClass.Validation, errors,
            $"Prerequisites for '{stage}' are not all fresh.", report, nextActions: nextActions);
    }

    public static CliResult CycleRefreshPlan(CliMeta meta, string repo, string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return Usage(meta, "doti cycle refresh-plan", "--target is required.");
        }

        CycleRecoveryPlan plan = new CycleService(repo).RecoveryPlan(target);
        string summary = plan.Steps.Count == 0
            ? $"'{target}' prerequisites are all fresh."
            : plan.Recoverable
                ? $"{plan.Steps.Count} stale step(s) for '{target}', all safely refreshable (`doti cycle refresh --apply-safe`)."
                : $"{plan.Steps.Count} stale step(s) for '{target}'; some require re-running the stage.";
        return CliResults.Ok(meta, "doti cycle refresh-plan", summary, plan,
            nextActions: plan.Steps
                .Select(s => new CliNextAction($"Recover '{s.Stage}'", s.Reason ?? s.Status, s.NextCommand))
                .ToList());
    }

    public static CliResult CycleRefresh(CliMeta meta, string repo, string target, bool applySafe)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return Usage(meta, "doti cycle refresh", "--target is required.");
        }

        CycleRefreshResult result = new CycleService(repo).Refresh(target, applySafe);
        // 027: ReBindContentEqual is auto-refreshable by --apply-safe (content-equal / edge-only rebind), exactly like
        // SafeReinterpret; only RerunRequired / inserted-stage / unclassified steps are true blockers needing a re-run.
        List<StageRecoveryStep> safe = result.Remaining
            .Where(s => s.Safety is RestampSafety.SafeReinterpret or RestampSafety.ReBindContentEqual)
            .ToList();
        List<StageRecoveryStep> blockers = result.Remaining
            .Where(s => s.Safety is not (RestampSafety.SafeReinterpret or RestampSafety.ReBindContentEqual))
            .ToList();
        List<CliNextAction> nextActions = result.Remaining
            .Select(s => new CliNextAction($"Recover '{s.Stage}'", s.Reason ?? s.Status, s.NextCommand))
            .ToList();

        // Dry run (no --apply-safe): informational preview, never a failure.
        if (!applySafe)
        {
            string summary = result.Remaining.Count == 0
                ? $"'{target}' prerequisites are all fresh."
                : $"{safe.Count} step(s) safely refreshable (`doti cycle refresh --target {target} --apply-safe`), {blockers.Count} need re-running.";
            return CliResults.Ok(meta, "doti cycle refresh", summary, result, nextActions: nextActions);
        }

        if (blockers.Count == 0)
        {
            string okSummary = result.Refreshed.Count > 0
                ? $"Refreshed {result.Refreshed.Count} stage(s); '{target}' is recoverable."
                : $"Nothing to refresh; '{target}' prerequisites are fresh.";
            return CliResults.Ok(meta, "doti cycle refresh", okSummary, result);
        }

        List<Diagnostic> errors = blockers
            .Select(b => Diag.Of(RefreshBlockerCode(b.Safety),
                $"{b.Stage}: {b.Status}" + (b.Reason is { } r ? $" ({r})" : ""), target: b.Stage))
            .ToList();
        return CliResults.Fail(meta, "doti cycle refresh", ExitClass.Validation, errors,
            $"Refreshed {result.Refreshed.Count} stage(s); {blockers.Count} step(s) require a re-run for '{target}'.",
            result, nextActions: blockers
                .Select(b => new CliNextAction($"Re-run '{b.Stage}'", b.Reason ?? "Stage requires re-running.", b.NextCommand))
                .ToList());
    }

    private static string RefreshBlockerCode(RestampSafety? safety) => safety switch
    {
        RestampSafety.NotBound => ErrorCodes.Validation_CycleRefreshNotBound,
        RestampSafety.RerunRequired => ErrorCodes.Validation_CycleRefreshRerunRequired,
        _ => ErrorCodes.Validation_Failed, // a missing/invalid prerequisite (not stamped / open marker)
    };

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
