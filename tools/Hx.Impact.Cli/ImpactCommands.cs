using Hx.Cli.Kernel;
using Hx.Impact.Core;
using Hx.Impact.Core.ChangeDetection;
using Hx.Impact.Core.Planning;
using Hx.Tooling.Contracts;

namespace Hx.Impact.Cli;

/// <summary>
/// The Impact CLI's command bodies: map the deterministic <see cref="AffectedPlan"/> onto the <see cref="CliResult"/>
/// envelope. Kept out of <c>Program.cs</c> wiring so the plan→envelope mapping is unit-testable in-process.
/// A produced plan is always a successful command (exit 0); the fail-closed <c>full-gate-required</c> escalation is
/// surfaced as Direction (a NextAction) + <c>data.outcome</c>, never as a process failure. A planner exception
/// propagates to <see cref="CliHost"/>, which fail-closes to an Internal diagnostic — with no plan in the envelope,
/// a failure can never be misread as "no tests affected".
/// One plan, two audiences (007 T040, FR-043): <c>--for tests</c> reads <c>outcome</c>/<c>selectedTests</c>;
/// <c>--for arch-review</c> reads <c>changedFiles</c>/<c>affectedSourceProjects</c>. The plan itself is identical —
/// <c>--for</c> only tailors the human summary + Direction for the consumer; the structured data carries everything.
/// </summary>
public static class ImpactCommands
{
    /// <summary>Default audience: the affected-test scope (outcome + selected test projects).</summary>
    public const string AudienceTests = "tests";

    /// <summary>The <c>/04-doti-arch-review</c> audience: the changed-files + affected-projects review context.</summary>
    public const string AudienceArchReview = "arch-review";

    /// <summary>The <c>/08-doti-drift-review</c> audience (FR-010): the status-rich change context (each file's
    /// Added/Modified/Deleted/Renamed status + rename old-path) so the removed/renamed-symbol worklist is data-driven.</summary>
    public const string AudienceChangeContext = "change-context";

    /// <summary>Run the deterministic planner for a change set and wrap the plan in the envelope for the given audience.
    /// The <c>change-context</c> audience emits the rich <see cref="ChangeSetContext"/> (status per file) instead of the
    /// affected-test plan — drift-review reads <c>data.files</c> as its diff worklist.</summary>
    public static CliResult Plan(CliMeta meta, string repo, string baseRef, string headRef, string configuration, string audience)
    {
        if (audience == AudienceChangeContext)
        {
            ChangeSetContext context = new ChangeSetContextBuilder().BuildForRepo(Path.GetFullPath(repo), baseRef, headRef);
            return context.RefsResolved
                ? CliResults.Ok(meta, "plan",
                    $"Change context: {context.Files.Count} changed file(s), {context.AffectedSourceProjects.Count} affected source project(s).",
                    context,
                    nextActions:
                    [
                        new CliNextAction(
                            "Drive drift-review from the change context",
                            "Use data.files (path + Added/Modified/Deleted/Renamed status + rename old-path) as the worklist; a Deleted/Renamed symbol must survive in NO doc, skill, or help string."),
                    ])
                : CliResults.Fail(meta, "plan", ExitClass.Validation,
                    [Diag.Of(ErrorCodes.Validation_Failed, context.UnresolvedReason ?? "Could not resolve the change set.", target: "--base")],
                    "Could not resolve the change set for the change context.");
        }

        return FromPlan(meta, "plan", new AffectedTestPlanner().Plan(Path.GetFullPath(repo), baseRef, headRef, configuration), audience);
    }

    /// <summary>Emit the placeholder full plan (smoke / bootstrap) in the envelope.</summary>
    public static CliResult BootstrapPlan(CliMeta meta) =>
        FromPlan(meta, "bootstrap-plan", AffectedPlanFactory.BootstrapFullPlan());

    /// <summary>Map any affected plan onto the success envelope (exit 0) with audience-specific summary + Direction.</summary>
    public static CliResult FromPlan(CliMeta meta, string command, AffectedPlan plan, string audience = AudienceTests) =>
        CliResults.Ok(meta, command, Summary(plan, audience), plan, nextActions: NextActions(plan, audience));

    internal static string Summary(AffectedPlan plan, string audience = AudienceTests) =>
        audience == AudienceArchReview
            ? $"Arch-review context: {plan.ChangedFiles.Count} changed file(s), {plan.AffectedSourceProjects.Count} affected source project(s)."
            : plan.Outcome switch
            {
                AffectedOutcome.Affected => $"Affected: {plan.SelectedTests.Count} test project(s) selected.",
                AffectedOutcome.NoTestsRequired => "No tests required (only documentation/generated paths changed).",
                AffectedOutcome.FullGateRequired => "Full gate required: the change could not be narrowed safely.",
                _ => $"Plan outcome: {plan.Outcome}.",
            };

    internal static IReadOnlyList<CliNextAction> NextActions(AffectedPlan plan, string audience = AudienceTests)
    {
        if (audience == AudienceArchReview)
        {
            return [new CliNextAction(
                "Inject the context into every lens",
                "Triage the footprint from data.changedFiles, then pass data.changedFiles + data.affectedSourceProjects verbatim to each applicable arch-review lens so none rediscovers scope.")];
        }

        return plan.Outcome switch
        {
            AffectedOutcome.FullGateRequired =>
                [new CliNextAction("Run the full gate", "The change is broad or unattributed; run the whole suite.", "gate run --profile normal")],
            AffectedOutcome.Affected =>
                [new CliNextAction("Run the selected test projects", "Only these projects cover the change; see data.selectedTests for the exact commands.")],
            _ => [],
        };
    }
}
