using Hx.Cli.Kernel;
using Hx.Cycle.Core;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

// 028: the reviewed-no-impact attestation verb. Its own partial so its review-rebind exception/error-code
// references do not add to the fan-out of the action-model projection surface (DotiCycleReconcile).
public static partial class RunnerCommands
{
    public static CliResult CycleReviewRebind(CliMeta meta, string repo, string target, string attest, string reason)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return Usage(meta, "doti cycle review-rebind", "--target is required.");
        }

        if (string.IsNullOrWhiteSpace(attest))
        {
            return Usage(meta, "doti cycle review-rebind", "--attest is required (no-impact).");
        }

        try
        {
            CycleState state = new CycleService(repo).ReviewRebind(
                target, attest, string.IsNullOrWhiteSpace(reason) ? null : reason);
            return CliResults.Ok(meta, "doti cycle review-rebind",
                $"Recorded a reviewed-no-impact rebind for '{target}'; its prerequisite binding was rebound to the current upstream content.",
                state);
        }
        catch (CycleReviewRebindIneligibleException ex)
        {
            string code = ex.Refusal == ReviewRebindRefusal.NotStale
                ? ErrorCodes.Validation_CycleReviewRebindNotStale
                : ErrorCodes.Validation_CycleReviewRebindIneligible;
            return CliResults.Fail(meta, "doti cycle review-rebind", ExitClass.Validation,
                [Diag.Of(code, ex.Message, target: target)],
                $"'{target}' is not eligible for a reviewed-no-impact rebind.",
                nextActions:
                [
                    new CliNextAction($"Re-run '{target}'", "Re-author the stage instead of attesting.", $"/{target}"),
                ]);
        }
        catch (CycleInputException ex)
        {
            return Usage(meta, "doti cycle review-rebind", ex.Message);
        }
    }
}
