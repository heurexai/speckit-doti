using Hx.Cli.Kernel;
using Hx.Scaffold.Core;
using Hx.Scaffold.Core.Prerequisites;
using Hx.Scaffold.Core.Versioning;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Cli;

public static partial class ScaffoldCommands
{
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
}
