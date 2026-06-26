using Hx.Cli.Kernel;
using Hx.Doti.Core.Converge;
using Hx.Tooling.Contracts;

namespace Hx.Runner.Cli;

public static partial class RunnerCommands
{
    // 007 T038 (FR-039): report the spec requirements no task covers — the deterministic half of converge. The agent
    // assesses each gap against the codebase and appends the genuinely-missing ones as tasks (the command never
    // rewrites the ledger).
    public static CliResult Converge(CliMeta meta, string specPath, string tasksPath)
    {
        if (string.IsNullOrWhiteSpace(specPath) || !File.Exists(specPath))
        {
            return CliResults.Fail(meta, "doti converge", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_ConvergeInput, $"Spec file not found: '{specPath}'.")]);
        }

        if (string.IsNullOrWhiteSpace(tasksPath) || !File.Exists(tasksPath))
        {
            return CliResults.Fail(meta, "doti converge", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_ConvergeInput, $"Tasks file not found: '{tasksPath}'.")]);
        }

        ConvergeAnalysis analysis = ConvergeService.Analyze(File.ReadAllText(specPath), File.ReadAllText(tasksPath));
        string summary = analysis.UncoveredRequirements.Count == 0
            ? $"Converged: all {analysis.SpecRequirements.Count} spec requirement(s) are covered by a task."
            : $"{analysis.UncoveredRequirements.Count} spec requirement(s) not covered by any task: {string.Join(", ", analysis.UncoveredRequirements)}.";
        return CliResults.Ok(meta, "doti converge", summary, analysis);
    }
}
