using Hx.Cli.Kernel;
using Hx.Doti.Core;
using Hx.Tooling.Contracts;
using Hx.Tooling.Contracts.Setup;

namespace Hx.Scaffold.Cli;

public static partial class ScaffoldCommands
{
    /// <summary>029: the validated inputs for <c>hx doti install</c> — either an early CLI failure (missing <c>--repo</c>,
    /// mutually-exclusive flags, a bad <c>--agents</c> CSV, or an invalid <c>--config</c>), or the parsed agent targets
    /// plus the resolved setup config. Keeps the argument-validation + setup-config type fan-out out of the command method.</summary>
    internal sealed record DotiInstallSetup(
        CliResult? Error,
        IReadOnlyList<DotiAgentTarget> Agents,
        SetupConfigResolution Setup);

    /// <summary>029 C3/D5: validate the <c>hx doti install</c> arguments and resolve the Install-subset setup config
    /// (file, wizard, or none — via <see cref="ResolveSetupForInstall"/>) BEFORE any install write — so the command
    /// method only branches on the result.</summary>
    internal static DotiInstallSetup PrepareInstallSetup(
        CliMeta meta, string? targetRepo, string agentsCsv, string? configPath, bool interactive, ISetupConsole? console)
    {
        if (string.IsNullOrWhiteSpace(targetRepo))
        {
            return Fail("doti install requires an explicit --repo <target-directory>; it never defaults to the current directory.");
        }

        if (interactive && !string.IsNullOrWhiteSpace(configPath))
        {
            return Fail("Pass either --config or --interactive, not both.");
        }

        if (!DotiAgentTarget.TryParseCsv(agentsCsv, out IReadOnlyList<DotiAgentTarget> agents, out string? error))
        {
            return Fail(error!);
        }

        // 029 C3/D5: resolve the Install-subset setup config (file or wizard) and validate BEFORE any install write.
        SetupConfigResolution setup = ResolveSetupForInstall(meta, configPath, interactive, agentsCsv, console);
        return new DotiInstallSetup(setup.Error, agents, setup);

        DotiInstallSetup Fail(string message) => new(
            CliResults.Fail(meta, "doti install", ExitClass.Usage, [Diag.Of(ErrorCodes.Usage_InvalidArguments, message)]),
            [], new SetupConfigResolution(null, null));
    }

    /// <summary>029 FR-007: the operator checklist for <c>hx doti install</c>, surfaced only when a setup config was
    /// supplied (a no-config install stays byte-identical — SC-007). The NuGet OIDC items appear only when publish
    /// intent is set and NAME the resolved owner/repo/workflow/environment.</summary>
    private static IReadOnlyList<CliNextAction>? InstallChecklist(SetupConfigResolution setup) =>
        setup.Resolved is null
            ? null
            : SetupChecklist.AsNextActions(SetupPublishIntent.FromResolved(setup.Resolved));
}
