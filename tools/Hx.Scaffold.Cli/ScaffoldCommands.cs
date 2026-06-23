using Hx.Cli.Kernel;
using Hx.Scaffold.Core;
using Hx.Scaffold.Core.Prerequisites;
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
public static partial class ScaffoldCommands
{
    public static CliResult Profile(CliMeta meta) =>
        CliResults.Ok(meta, "profile", $"Default scaffold profile: {ScaffoldBootstrap.DefaultProfile.Name}.",
            ScaffoldBootstrap.DefaultProfile);

    public static CliResult New(
        CliMeta meta, string name, string company, string output, string profile, string agentsCsv,
        Action<CliEvent>? onEvent = null,
        PrerequisiteServices? prerequisiteServices = null)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(output))
        {
            return CliResults.Fail(meta, "new", ExitClass.Usage,
                [Diag.Of(ErrorCodes.Usage_InvalidArguments, "Both --name and --output are required.")]);
        }

        string[] agents = agentsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var request = new ScaffoldRequest(name, company, output, profile, agents);
        string sourceRoot = ScaffoldRoot.Resolve(Directory.GetCurrentDirectory());
        CliResult? preflight = CheckPrerequisitesForCommand(
            meta,
            "new",
            new PrerequisiteCheckRequest(sourceRoot, PrerequisiteCommands.New, OutputPath: output),
            prerequisiteServices);
        if (preflight is not null)
        {
            return preflight;
        }

        ScaffoldProof proof = ScaffoldNewRunner.Run(request, sourceRoot, onEvent, meta.Version);
        string summary = $"Scaffold '{name}' ({profile}): {proof.Outcome}.";
        return proof.Outcome == StageOutcome.Pass
            ? CliResults.Ok(meta, "new", summary, proof,
                effects: [new CliEffect("create", Path.GetFullPath(output), "generated + finished + smoked repo")])
            : CliResults.Fail(meta, "new", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, $"{summary} {FailureDetail(proof)}")], summary, proof);
    }

    public static CliResult Version(CliMeta meta, string repo, PrerequisiteServices? prerequisiteServices = null)
    {
        string? target = string.IsNullOrWhiteSpace(repo) ? null : repo;
        PrerequisiteCheckReport? prerequisites = null;
        try
        {
            prerequisites = TryPrerequisiteReport(
                new PrerequisiteCheckRequest(
                    ScaffoldRoot.Resolve(Directory.GetCurrentDirectory()),
                    PrerequisiteCommands.Version,
                    RepositoryRoot: target),
                prerequisiteServices);
        }
        catch (InvalidOperationException)
        {
            // Running-only version output must stay read-only and available even if a damaged install lacks payload
            // metadata; `new`/`update` still fail closed through their mandatory preflight.
        }

        ScaffoldVersionReport report = ScaffoldVersionReporter.Report(meta.Version, target, prerequisites);
        string summary = report.Target is null
            ? $"hx version {report.Running.Version}."
            : $"hx version {report.Running.Version}; target {report.Target.Version}; managed assets {report.ManagedAssets?.State ?? "unknown"}.";

        return CliResults.Ok(meta, "version", summary, report);
    }

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

    public static CliResult PrereqCheck(
        CliMeta meta,
        string command,
        string repo,
        string output,
        PrerequisiteServices? prerequisiteServices = null)
    {
        string? normalizedCommand = NormalizePrerequisiteCommand(command, installCommand: false);
        if (normalizedCommand is null)
        {
            return InvalidPrerequisiteCommand(meta, "prereq check", command, "new, update, version, or generated-validation");
        }

        string sourceRoot = ScaffoldRoot.Resolve(Directory.GetCurrentDirectory());
        PrerequisiteCheckReport report;
        try
        {
            report = PrerequisitePreflight.Check(
                new PrerequisiteCheckRequest(
                    sourceRoot,
                    normalizedCommand,
                    RepositoryRoot: string.IsNullOrWhiteSpace(repo) ? null : repo,
                    OutputPath: string.IsNullOrWhiteSpace(output) ? null : output),
                prerequisiteServices);
        }
        catch (InvalidOperationException ex)
        {
            return PrerequisiteManifestFailure(meta, "prereq check", ex);
        }

        return report.Ok
            ? CliResults.Ok(meta, "prereq check", $"Prerequisites for {report.Command}: pass.", report)
            : PrerequisiteFailure(meta, "prereq check", report, blocked: false);
    }

    public static CliResult PrereqInstall(
        CliMeta meta,
        string command,
        string repo,
        string output,
        string confirmPlan,
        PrerequisiteServices? prerequisiteServices = null)
    {
        string? normalizedCommand = NormalizePrerequisiteCommand(command, installCommand: true);
        if (normalizedCommand is null)
        {
            return InvalidPrerequisiteCommand(meta, "prereq install", command, "new or update");
        }

        string sourceRoot = ScaffoldRoot.Resolve(Directory.GetCurrentDirectory());
        PrerequisiteCheckReport report;
        try
        {
            report = PrerequisitePreflight.Install(
                new PrerequisiteCheckRequest(
                    sourceRoot,
                    normalizedCommand,
                    RepositoryRoot: string.IsNullOrWhiteSpace(repo) ? null : repo,
                    OutputPath: string.IsNullOrWhiteSpace(output) ? null : output),
                string.IsNullOrWhiteSpace(confirmPlan) ? null : confirmPlan,
                prerequisiteServices);
        }
        catch (InvalidOperationException ex)
        {
            return PrerequisiteManifestFailure(meta, "prereq install", ex);
        }

        if (report.Ok)
        {
            return CliResults.Ok(meta, "prereq install", $"Prerequisites for {report.Command}: pass.", report);
        }

        bool needsApproval = report.Blockers.Any(b => b.Contains("operator approval", StringComparison.OrdinalIgnoreCase));
        return PrerequisiteFailure(meta, "prereq install", report, blocked: needsApproval);
    }

    private static CliResult? CheckPrerequisitesForCommand(
        CliMeta meta,
        string commandName,
        PrerequisiteCheckRequest request,
        PrerequisiteServices? services)
    {
        PrerequisiteCheckReport report;
        try
        {
            report = PrerequisitePreflight.Check(request, services);
        }
        catch (InvalidOperationException ex)
        {
            return PrerequisiteManifestFailure(meta, commandName, ex);
        }

        return report.Ok ? null : PrerequisiteFailure(meta, commandName, report, blocked: false);
    }

    private static PrerequisiteCheckReport? TryPrerequisiteReport(
        PrerequisiteCheckRequest request,
        PrerequisiteServices? services)
    {
        try
        {
            return PrerequisitePreflight.Check(request, services);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static CliResult PrerequisiteManifestFailure(CliMeta meta, string command, Exception ex) =>
        CliResults.Fail(meta, command, ExitClass.Integrity,
            [Diag.Of(ErrorCodes.Integrity_PrerequisiteManifestUntrusted, ex.Message)]);

    private static CliResult PrerequisiteFailure(
        CliMeta meta,
        string command,
        PrerequisiteCheckReport report,
        bool blocked)
    {
        string code = PrerequisiteCode(report);
        Diagnostic[] diagnostics = report.Blockers.Count == 0
            ? [Diag.Of(code, "Prerequisite preflight failed.")]
            : report.Blockers.Select(b => Diag.Of(code, b)).ToArray();
        CliNextAction[] next = report.NextActions
            .Select(a => new CliNextAction("Install prerequisite", a))
            .ToArray();
        string summary = $"Prerequisites for {report.Command}: {report.Blockers.Count} blocker(s).";
        return blocked
            ? CliResults.Blocked(meta, command, ExitClass.Validation, diagnostics, summary, report, next)
            : CliResults.Fail(meta, command, ExitClass.Validation, diagnostics, summary, report, next);
    }

    private static string PrerequisiteCode(PrerequisiteCheckReport report)
    {
        if (report.Directories.Any(d => !d.Ok))
        {
            return ErrorCodes.Validation_PrerequisiteDirectoryUnavailable;
        }

        if (report.Blockers.Any(b => b.Contains("operator approval", StringComparison.OrdinalIgnoreCase)))
        {
            return ErrorCodes.Validation_PrerequisiteInstallNotApproved;
        }

        if (report.Blockers.Any(b => b.Contains("winget is unavailable", StringComparison.OrdinalIgnoreCase)))
        {
            return ErrorCodes.Validation_PrerequisiteWingetUnavailable;
        }

        if (report.Blockers.Any(b => b.Contains("winget failed", StringComparison.OrdinalIgnoreCase)))
        {
            return ErrorCodes.Validation_PrerequisiteWingetFailed;
        }

        if (report.Items.Any(i => i.Status == "unsupported"))
        {
            return ErrorCodes.Validation_PrerequisiteUnsupportedVersion;
        }

        return ErrorCodes.Validation_PrerequisiteMissing;
    }

    private static CliResult InvalidPrerequisiteCommand(
        CliMeta meta,
        string commandName,
        string value,
        string allowed) =>
        CliResults.Fail(meta, commandName, ExitClass.Usage,
            [Diag.Of(ErrorCodes.Usage_InvalidArguments, $"Unsupported --for value '{value}'. Expected {allowed}.")]);

    private static string? NormalizePrerequisiteCommand(string command, bool installCommand) =>
        command.Trim().ToLowerInvariant() switch
        {
            "new" => PrerequisiteCommands.New,
            "update" => PrerequisiteCommands.Update,
            "version" when !installCommand => PrerequisiteCommands.Version,
            "generated-validation" when !installCommand => PrerequisiteCommands.GeneratedValidation,
            _ => null,
        };

}
