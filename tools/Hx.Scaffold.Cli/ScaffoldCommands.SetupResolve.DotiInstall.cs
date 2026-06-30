using Hx.Cli.Kernel;
using Hx.Doti.Core.Setup;
using Hx.Tooling.Contracts.Setup;

namespace Hx.Scaffold.Cli;

public static partial class ScaffoldCommands
{
    /// <summary>029 C3/D5: resolve the Install-subset setup config for <c>hx doti install</c> — from <c>--config</c>
    /// (validate-before-install), the wizard (Install audience), or nothing (no-config → null, D10 byte-identical). The
    /// load+validate+resolve logic is shared via <see cref="SetupConfigInput"/> (Doti.Core); this CLI seam only runs the
    /// wizard and maps a validation failure to the <see cref="SetupConfigInvalid"/> envelope.</summary>
    private static SetupConfigResolution ResolveSetupForInstall(
        CliMeta meta, string? configPath, bool interactive, string agentsCsv, ISetupConsole? console)
    {
        var flags = new SetupFlagOverrides(Agents: AgentsOrNull(agentsCsv));

        if (interactive)
        {
            SetupConfig wizardConfig = SetupWizard.Run(console ?? SystemSetupConsole.Instance, SetupAudience.Install, flags);
            return new SetupConfigResolution(SetupConfigInput.ResolveInteractive(wizardConfig, flags, SetupAudience.Install), null);
        }

        SetupResolveOutcome outcome = SetupConfigInput.ResolveFromFile(
            configPath, Directory.GetCurrentDirectory(), flags, SetupAudience.Install);
        return outcome.Ok
            ? new SetupConfigResolution(outcome.Resolved, null)
            : new SetupConfigResolution(null, SetupConfigInvalid(meta, "doti install", outcome.Errors));
    }
}
