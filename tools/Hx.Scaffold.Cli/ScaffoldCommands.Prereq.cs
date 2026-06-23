using Hx.Cli.Kernel;
using Hx.Scaffold.Core;
using Hx.Scaffold.Core.Prerequisites;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Cli;

public static partial class ScaffoldCommands
{
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
