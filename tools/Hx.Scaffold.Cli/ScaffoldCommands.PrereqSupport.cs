using Hx.Cli.Kernel;
using Hx.Scaffold.Core.Prerequisites;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Cli;

public static partial class ScaffoldCommands
{
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
}
