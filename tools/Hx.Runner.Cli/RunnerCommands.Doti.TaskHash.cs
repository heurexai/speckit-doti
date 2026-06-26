using Hx.Cli.Kernel;
using Hx.Cycle.Core.Tasks;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    public static CliResult TaskHashStamp(CliMeta meta, string repo, string feature)
    {
        try
        {
            TaskHashStampResult result = DotiTaskCompletion.StampFeature(
                repo,
                string.IsNullOrWhiteSpace(feature) ? null : feature);

            if (result.Outcome == StageOutcome.Pass)
            {
                return CliResults.Ok(meta, "doti task-hash stamp", result.Summary, result,
                    effects: result.UpdatedCount > 0
                        ? [new CliEffect("write", result.TaskFile, "task hash markers")]
                        : []);
            }

            List<Diagnostic> errors = result.Diagnostics
                .Select(d => Diag.Of(TaskCompletionCode(d), d.ToEvidenceMessage(), target: d.TaskId ?? d.Path))
                .ToList();
            return CliResults.Fail(meta, "doti task-hash stamp", ExitClass.Validation, errors,
                result.Summary, result);
        }
        catch (InvalidOperationException ex)
        {
            return CliResults.Fail(meta, "doti task-hash stamp", ExitClass.Usage,
                [Diag.Of(ErrorCodes.Usage_InvalidArguments, ex.Message, target: "--feature")],
                "Task hash stamping needs an explicit or active feature.");
        }
    }

    private static string TaskCompletionCode(TaskCompletionDiagnostic diagnostic) =>
        diagnostic.Reason switch
        {
            "missing-task-file" => ErrorCodes.Validation_DotiTaskFileMissing,
            "no-tasks" => ErrorCodes.Validation_DotiTaskFileEmpty,
            "duplicate-task-id" => ErrorCodes.Validation_DotiTaskIdDuplicate,
            "unchecked" => ErrorCodes.Validation_DotiTaskUnchecked,
            "missing-hash" => ErrorCodes.Validation_DotiTaskHashMissing,
            "hash-mismatch" => ErrorCodes.Validation_DotiTaskHashMismatch,
            DotiTaskCompletion.OutOfOrderReason => ErrorCodes.Validation_TaskOutOfOrder,
            _ => ErrorCodes.Validation_Failed
        };
}
