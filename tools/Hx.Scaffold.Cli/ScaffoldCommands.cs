using Hx.Cli.Kernel;
using Hx.Scaffold.Core;
using Hx.Scaffold.Core.Update;
using Hx.Scaffold.Core.Versioning;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Cli;

/// <summary>
/// The Scaffold CLI's command bodies: map generation onto the <see cref="CliResult"/> envelope. Kept out of
/// <c>Program.cs</c> wiring so the mapping is unit-testable in-process. A successful <c>new</c> carries the generated
/// repo as an Effect; a missing <c>--name</c>/<c>--output</c> is a Usage error; a generation/smoke failure is a
/// Validation failure with the <see cref="ScaffoldProof"/> preserved in <c>data</c>.
/// </summary>
public static class ScaffoldCommands
{
    public static CliResult Profile(CliMeta meta) =>
        CliResults.Ok(meta, "profile", $"Default scaffold profile: {ScaffoldBootstrap.DefaultProfile.Name}.",
            ScaffoldBootstrap.DefaultProfile);

    public static CliResult New(
        CliMeta meta, string name, string company, string output, string profile, string agentsCsv,
        Action<CliEvent>? onEvent = null)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(output))
        {
            return CliResults.Fail(meta, "new", ExitClass.Usage,
                [Diag.Of(ErrorCodes.Usage_InvalidArguments, "Both --name and --output are required.")]);
        }

        string[] agents = agentsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var request = new ScaffoldRequest(name, company, output, profile, agents);
        string sourceRoot = ScaffoldRoot.Resolve(Directory.GetCurrentDirectory());

        ScaffoldProof proof = ScaffoldNewRunner.Run(request, sourceRoot, onEvent, meta.Version);
        string summary = $"Scaffold '{name}' ({profile}): {proof.Outcome}.";
        return proof.Outcome == StageOutcome.Pass
            ? CliResults.Ok(meta, "new", summary, proof,
                effects: [new CliEffect("create", Path.GetFullPath(output), "generated + finished + smoked repo")])
            : CliResults.Fail(meta, "new", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, summary)], summary, proof);
    }

    public static CliResult Version(CliMeta meta, string repo)
    {
        string? target = string.IsNullOrWhiteSpace(repo) ? null : repo;
        ScaffoldVersionReport report = ScaffoldVersionReporter.Report(meta.Version, target);
        string summary = report.Target is null
            ? $"hx version {report.Running.Version}."
            : $"hx version {report.Running.Version}; target {report.Target.Version}; managed assets {report.ManagedAssets?.State ?? "unknown"}.";

        return CliResults.Ok(meta, "version", summary, report);
    }

    public static CliResult Update(CliMeta meta, string repo, bool dryRun, bool force, bool noWorktree, bool jsonOutput = false)
    {
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
