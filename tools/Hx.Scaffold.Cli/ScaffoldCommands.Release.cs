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
        bool saveReleaseRoot,
        bool major = false,
        bool minor = false,
        bool patch = false)
    {
        int intentCount = (major ? 1 : 0) + (minor ? 1 : 0) + (patch ? 1 : 0);
        if (intentCount > 1)
        {
            return CliResults.Fail(meta, "release", ExitClass.Usage,
                [Diag.Of(ErrorCodes.Usage_InvalidArguments,
                    "Specify at most one release intent: --major, --minor, or --patch. Patch is the default.")]);
        }

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
                meta.Version,
                major ? "major" : minor ? "minor" : "patch"));

            string summary = result.LocalCopyProduced
                ? $"Release {result.ProjectName} {result.Version} ({result.ReleaseIntent}) tagged {result.Tag.Name} and copied to {result.VersionDirectory} and {result.LatestDirectory}."
                : $"Release {result.ProjectName} {result.Version} ({result.ReleaseIntent}) tagged {result.Tag.Name}: local copy skipped ({result.SkippedReason}).";

            return CliResults.Ok(meta, "release", summary, result,
                effects: result.LocalCopyProduced
                    ? [
                        new CliEffect("tag", result.Tag.Name, result.Tag.Created ? "created annotated release tag" : "verified existing release tag"),
                        new CliEffect("created", result.VersionDirectory!, "version release directory"),
                        new CliEffect("modified", result.LatestDirectory!, "latest release directory")
                    ]
                    : [
                        new CliEffect("tag", result.Tag.Name, result.Tag.Created ? "created annotated release tag" : "verified existing release tag")
                    ]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return CliResults.Fail(meta, "release", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_Failed, ex.Message)]);
        }
    }
}
