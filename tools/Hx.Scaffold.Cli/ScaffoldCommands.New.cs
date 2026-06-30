using Hx.Cli.Kernel;
using Hx.Scaffold.Core.Prerequisites;
using Hx.Tooling.Contracts;

namespace Hx.Scaffold.Cli;

public static partial class ScaffoldCommands
{
    public static CliResult New(
        CliMeta meta, string name, string company, string output, string profile, string agentsCsv,
        string? configPath = null,
        bool interactive = false,
        Action<CliEvent>? onEvent = null,
        PrerequisiteServices? prerequisiteServices = null,
        ISetupConsole? console = null)
    {
        // Thin parse→delegate→render: the 029 setup-config wiring (resolve/validate/contain + build the request and the
        // operator-intent checklist) lives in PrepareNewSetup; the generate+finish+smoke+render lives in ExecuteNew.
        NewSetupPlan plan = PrepareNewSetup(meta, name, company, output, profile, agentsCsv, configPath, interactive, console);
        return plan.Error ?? ExecuteNew(meta, plan, onEvent, prerequisiteServices);
    }
}
