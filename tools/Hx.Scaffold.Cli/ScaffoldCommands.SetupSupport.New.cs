using Hx.Cli.Kernel;
using Hx.Tooling.Contracts;
using Hx.Tooling.Contracts.Setup;

namespace Hx.Scaffold.Cli;

public static partial class ScaffoldCommands
{
    /// <summary>029: the outcome of preparing <c>hx new</c> — either an early CLI failure (mutually-exclusive flags,
    /// an invalid <c>--config</c>, missing identity, or a traversal-escaping output), or the validated
    /// <see cref="ScaffoldRequest"/> plus the resolved operator-intent checklist. Keeps the 029 setup-config wiring
    /// (and its type fan-out) out of the thin <see cref="New"/> command method.</summary>
    internal sealed record NewSetupPlan(
        CliResult? Error,
        ScaffoldRequest? Request,
        IReadOnlyList<CliNextAction>? Checklist);

    /// <summary>
    /// 029 D5/D9: resolve the operator setup config (file, wizard, or none — via <see cref="ResolveSetupForNew"/>),
    /// apply the flag-wins effective identity, validate BEFORE anything is generated (SC-006), enforce output
    /// containment (D9), and build the <see cref="ScaffoldRequest"/> + the operator-intent checklist (FR-007). Pure
    /// wiring — the command method only branches on the returned <see cref="NewSetupPlan"/>.
    /// </summary>
    internal static NewSetupPlan PrepareNewSetup(
        CliMeta meta, string name, string company, string output, string profile, string agentsCsv,
        string? configPath, bool interactive, ISetupConsole? console)
    {
        // 029 FR-005: --config and --interactive are mutually exclusive (the wizard re-enters the --config path).
        if (interactive && !string.IsNullOrWhiteSpace(configPath))
        {
            return UsageFail(meta, "Pass either --config or --interactive, not both.");
        }

        // 029 D5: resolve the operator setup config (file or wizard) and VALIDATE before anything is generated.
        SetupConfigResolution setup = ResolveSetupForNew(meta, configPath, interactive, name, company, output, agentsCsv, console);
        if (setup.Error is not null)
        {
            return new NewSetupPlan(setup.Error, null, null);
        }

        // Explicit flags + the resolved config decide the effective identity (flags already win in the resolver).
        string effectiveName = NonEmptyOr(setup.Resolved?.ValueOrDefault(SetupKeys.IdentityName), name);
        string effectiveCompany = NonEmptyOr(setup.Resolved?.ValueOrDefault(SetupKeys.IdentityCompany), company);
        string effectiveOutput = NonEmptyOr(setup.Resolved?.ValueOrDefault(SetupKeys.IdentityOutput), output);
        string[] effectiveAgents = ResolveAgents(setup.Resolved, agentsCsv);

        if (string.IsNullOrWhiteSpace(effectiveName) || string.IsNullOrWhiteSpace(effectiveOutput))
        {
            return UsageFail(meta, "Both --name and --output are required (via flag, --config identity.name/output, or the wizard).");
        }

        // 029 D9: filesystem containment of the output path (reject a traversal that escapes the working tree).
        if (!IsContainedOutput(effectiveOutput))
        {
            return new NewSetupPlan(CliResults.Fail(meta, "new", ExitClass.Validation,
                [Diag.Of(ErrorCodes.Validation_SetupConfigInvalid, "identity.output / --output must stay inside the working tree (no '..' traversal).", target: "identity.output")]), null, null);
        }

        var request = new ScaffoldRequest(effectiveName, effectiveCompany, effectiveOutput, profile, effectiveAgents, setup.Resolved);

        // 029 FR-007: the operator-intent checklist (never executed) — the GitHub/nuget.org steps + the git/CI steps
        // deferred to 030. The NuGet OIDC items appear only when publish intent is set and NAME the resolved
        // owner/repo/workflow/environment so the operator-only step is actionable.
        IReadOnlyList<CliNextAction> checklist = SetupChecklist.AsNextActions(SetupPublishIntent.FromResolved(setup.Resolved));
        return new NewSetupPlan(null, request, checklist);
    }

    private static NewSetupPlan UsageFail(CliMeta meta, string message) =>
        new(CliResults.Fail(meta, "new", ExitClass.Usage, [Diag.Of(ErrorCodes.Usage_InvalidArguments, message)]), null, null);
}
