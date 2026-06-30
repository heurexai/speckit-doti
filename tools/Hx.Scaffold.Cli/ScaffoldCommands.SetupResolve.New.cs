using Hx.Cli.Kernel;
using Hx.Doti.Core.Setup;
using Hx.Tooling.Contracts.Setup;

namespace Hx.Scaffold.Cli;

public static partial class ScaffoldCommands
{
    /// <summary>029: resolve the setup config for <c>hx new</c> — from <c>--config</c> (validate-before-generate),
    /// the <c>--interactive</c> wizard (re-enters the IDENTICAL <c>--config</c> resolve), or nothing (no-config path →
    /// null resolved). The load+validate+resolve logic is shared via <see cref="SetupConfigInput"/> (Doti.Core); this
    /// CLI seam only runs the wizard and maps a validation failure to the <see cref="SetupConfigInvalid"/> envelope.</summary>
    private static SetupConfigResolution ResolveSetupForNew(
        CliMeta meta, string? configPath, bool interactive,
        string name, string company, string output, string agentsCsv, ISetupConsole? console)
    {
        var flags = new SetupFlagOverrides(
            NonEmptyOrNull(name), NonEmptyOrNull(company), NonEmptyOrNull(output), AgentsOrNull(agentsCsv));

        if (interactive)
        {
            SetupConfig wizardConfig = SetupWizard.Run(console ?? SystemSetupConsole.Instance, SetupAudience.New, flags);
            return new SetupConfigResolution(SetupConfigInput.ResolveInteractive(wizardConfig, flags, SetupAudience.New), null);
        }

        SetupResolveOutcome outcome = SetupConfigInput.ResolveFromFile(
            configPath, Directory.GetCurrentDirectory(), flags, SetupAudience.New);
        return outcome.Ok
            ? new SetupConfigResolution(outcome.Resolved, null)
            : new SetupConfigResolution(null, SetupConfigInvalid(meta, "new", outcome.Errors));
    }
}
