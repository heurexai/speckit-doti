using Hx.Cli.Kernel;
using Hx.Scaffold.Core;
using Hx.Scaffold.Core.Prerequisites;
using Hx.Scaffold.Core.Update;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Cli;

public static partial class ScaffoldCommands
{
    public static CliResult Update(
        CliMeta meta,
        string repo,
        bool dryRun,
        bool force,
        bool noWorktree,
        bool jsonOutput = false,
        PrerequisiteServices? prerequisiteServices = null)
    {
        string sourceRoot = ScaffoldRoot.Resolve(Directory.GetCurrentDirectory());
        CliResult? preflight = CheckPrerequisitesForCommand(
            meta,
            "update",
            new PrerequisiteCheckRequest(sourceRoot, PrerequisiteCommands.Update, RepositoryRoot: repo),
            prerequisiteServices);
        if (preflight is not null)
        {
            return preflight;
        }

        ScaffoldUpdateReport report;
        try
        {
            report = ScaffoldUpdateService.Plan(new ScaffoldUpdateRequest(repo, dryRun, force, noWorktree, meta.Version, jsonOutput));
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            return CliResults.Fail(meta, "update", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, ex.Message)]);
        }

        string summary = report.Blockers.Count > 0
            ? $"Update refused for {report.TargetRepo}: {report.Blockers.Count} blocker(s)."
            : dryRun
                ? $"Update dry-run for {report.TargetRepo}: ready."
                : report.Delegated
                    ? $"Update delegated for {report.TargetRepo}."
                    : $"Update applied for {report.TargetRepo}: {report.ChangedPaths.Count} file(s) changed.";

        if (report.Blockers.Count == 0)
        {
            return CliResults.Ok(meta, "update", summary, report);
        }

        return CliResults.Fail(meta, "update", ExitClass.Validation,
            report.Diagnostics.Count > 0
                ? report.Diagnostics.Select(d => Diag.Of(ErrorCodes.Validation_Failed, d.Message, d.Path)).ToArray()
                : report.Blockers.Select(b => Diag.Of(ErrorCodes.Validation_Failed, b)).ToArray(),
            summary,
            report);
    }
}
