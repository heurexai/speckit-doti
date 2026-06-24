using Hx.Cli.Kernel;
using Hx.Scaffold.Core.Release;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Cli;

public static partial class ScaffoldCommands
{
    public static CliResult Release(
        CliMeta meta,
        string repo,
        string rid,
        string releaseRoot,
        string releaseRootEnv,
        bool saveReleaseRoot)
    {
        if (!string.IsNullOrWhiteSpace(releaseRootEnv)
            && !LocalReleaseRootResolver.IsValidEnvironmentVariableName(releaseRootEnv.Trim()))
        {
            return CliResults.Fail(meta, "release", ExitClass.Usage,
                [Diag.Of(ErrorCodes.Usage_InvalidArguments,
                    $"Invalid release-root environment variable name '{releaseRootEnv}'. Use letters, digits, and underscores; the first character must not be a digit.")]);
        }

        if (saveReleaseRoot && string.IsNullOrWhiteSpace(releaseRoot))
        {
            return CliResults.Fail(meta, "release", ExitClass.Usage,
                [Diag.Of(ErrorCodes.Usage_InvalidArguments,
                    "--save-release-root requires an explicit --release-root <path>; environment-discovered roots are not persisted.")]);
        }

        try
        {
            LocalReleaseResult result = LocalReleaseService.Run(new LocalReleaseRequest(
                repo,
                string.IsNullOrWhiteSpace(releaseRoot) ? null : releaseRoot,
                string.IsNullOrWhiteSpace(releaseRootEnv) ? null : releaseRootEnv,
                saveReleaseRoot,
                string.IsNullOrWhiteSpace(rid) ? null : rid,
                meta.Version));

            string summary = result.LocalCopyProduced
                ? $"Release {result.ProjectName} {result.Version} copied to {result.VersionDirectory} and {result.LatestDirectory}."
                : $"Release {result.ProjectName} {result.Version}: local copy skipped ({result.SkippedReason}).";

            return CliResults.Ok(meta, "release", summary, result,
                effects: result.LocalCopyProduced
                    ? [
                        new CliEffect("created", result.VersionDirectory!, "version release directory"),
                        new CliEffect("modified", result.LatestDirectory!, "latest release directory")
                    ]
                    : null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return CliResults.Fail(meta, "release", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, ex.Message)]);
        }
    }
}
