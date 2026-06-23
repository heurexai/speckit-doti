using Hx.Runner.Core.Process;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Core.Prerequisites;

public static class PrerequisiteCommands
{
    public const string New = "new";
    public const string Update = "update";
    public const string Version = "version";
    public const string GeneratedValidation = "generated-validation";
}

public sealed record PrerequisiteManifest(
    int SchemaVersion,
    IReadOnlyList<PrerequisiteRequirement> Requirements);

public sealed record PrerequisiteRequirement(
    string Id,
    string DisplayName,
    IReadOnlyList<string> HardFor,
    IReadOnlyList<string> AdvisoryFor,
    PrerequisiteProbe Probe,
    string? MinimumVersion,
    IReadOnlyList<string> Instructions,
    WingetPackageMapping? Winget);

public sealed record PrerequisiteProbe(
    string Executable,
    IReadOnlyList<string> Arguments,
    string VersionPattern);

public sealed record WingetPackageMapping(
    string PackageId,
    string Source);

public sealed record PrerequisiteCheckRequest(
    string SourceRoot,
    string Command,
    string? RepositoryRoot = null,
    string? OutputPath = null);

public sealed record PrerequisiteCheckReport(
    int SchemaVersion,
    string Command,
    string ManifestPath,
    string ManifestSha256,
    bool Ok,
    IReadOnlyList<PrerequisiteCheckItem> Items,
    IReadOnlyList<PrerequisiteDirectoryCheck> Directories,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> NextActions,
    PrerequisiteInstallPlan? InstallPlan,
    IReadOnlyList<PrerequisiteInstallExecution> InstallExecutions);

public sealed record PrerequisiteCheckItem(
    string Id,
    string DisplayName,
    string Level,
    string Status,
    string? DetectedVersion,
    string? RequiredVersion,
    string? Path,
    string? Reason,
    WingetPackageMapping? Winget);

public sealed record PrerequisiteDirectoryCheck(
    string Id,
    string Path,
    bool Ok,
    string? Reason);

public sealed record PrerequisiteInstallPlan(
    string Digest,
    string Command,
    IReadOnlyList<PrerequisiteInstallPlanItem> Items);

public sealed record PrerequisiteInstallPlanItem(
    string PrerequisiteId,
    string Reason,
    string PackageId,
    string Source,
    string? RequiredVersion);

public sealed record PrerequisiteInstallExecution(
    string PrerequisiteId,
    string PackageId,
    string Source,
    int ExitCode,
    string StandardOutput,
    string StandardError);

public sealed class PrerequisiteServices
{
    public Func<string, IReadOnlyList<string>, string, ProcessRunResult> RunProcess { get; init; } =
        (fileName, arguments, workingDirectory) =>
            Hx.Runner.Core.Process.ProcessRunner.Run(new ToolCommand(fileName, arguments, workingDirectory));

    public Func<bool> IsWindows { get; init; } = OperatingSystem.IsWindows;
}
