using Hx.Impact.Core.ChangeDetection;
using Hx.Impact.Core.Domain;
using Hx.Impact.Core.Graph;
using Hx.Impact.Core.Planning;
using Hx.Tooling.Contracts;

namespace Hx.Gate.Core;

/// <summary>
/// 012 (FR-007/008/014/020/021): assembles the operator-facing <see cref="GateTrace"/> from a completed
/// <see cref="GateProof"/> + the change context + the affected plan + the injected <c>implement-stage code</c> flag.
/// Two-tier (the clarify decision):
/// <list type="bullet">
/// <item>the <b>basic</b> <see cref="ChangeSummary"/> (files + lines, no classes) is built for EVERY run;</item>
/// <item>the <b>detailed</b> tier (classes touched + the <see cref="AffectedTestInventory"/>) is built ONLY when the
/// run is the implement-stage gate AND the change set includes code (FR-021).</item>
/// </list>
/// The <c>GateRunner</c> engine stays stage-agnostic — the runner resolves the flag from the cycle state and passes
/// it here. The trace is REVIEW/TELEMETRY context: it lives on the <see cref="GateRunResult"/> envelope, never on
/// the hashed <c>AffectedTestProof.Plan</c>, and no <c>*ProofHasher</c> may depend on it (M1).
/// </summary>
public sealed class GateTraceProjector
{
    private readonly ChangeSummaryProjector _changeSummary;

    public GateTraceProjector(INumstatReader? numstat = null) =>
        _changeSummary = new ChangeSummaryProjector(numstat);

    /// <summary>The production entry: build the trace for a finished gate run. <paramref name="implementStageCode"/>
    /// is true only when the run is the implement-stage gate AND the change set includes code (resolved in the
    /// runner). Best-effort throughout — any failure to enrich falls back to the basic tier so the trace is always
    /// produced.</summary>
    public GateTrace Project(
        string repositoryRoot,
        GateProof proof,
        ChangeSetContext change,
        AffectedPlan plan,
        Lane lane,
        string baseRef,
        string headRef,
        string configuration,
        bool implementStageCode,
        long totalMs,
        IReadOnlyList<StructuralStepViolations>? structuralViolations = null)
    {
        bool changeHasCode = ChangeHasCode(change);
        bool detailed = implementStageCode && changeHasCode;

        ChangeSummary summary = _changeSummary.Project(repositoryRoot, change, baseRef, headRef, includeClasses: detailed);
        AffectedTestInventory? inventory = detailed
            ? TryBuildInventory(repositoryRoot, plan, lane, configuration)
            : null;

        return Assemble(proof, summary, inventory, plan, lane, totalMs, structuralViolations);
    }

    /// <summary>Pure assembly (no IO) — the testable core. Composes the trace and computes the effective mode from
    /// the plan + lane (release/escalation force <c>full</c>; an affected plan with selections is <c>partial</c>;
    /// no-tests is <c>none</c>).</summary>
    public static GateTrace Assemble(
        GateProof proof,
        ChangeSummary summary,
        AffectedTestInventory? inventory,
        AffectedPlan plan,
        Lane lane,
        long totalMs,
        IReadOnlyList<StructuralStepViolations>? structuralViolations = null) =>
        new(
            proof.Scope ?? new GateScope(JsonContractDefaults.SchemaVersion, false, "scope: not recorded", []),
            summary,
            inventory,
            proof.Steps,
            totalMs,
            EffectiveMode(plan, lane),
            // 014 (FR-004): null when no structural step has offenders, so a green run carries no offender noise.
            structuralViolations is { Count: > 0 } ? structuralViolations : null);

    /// <summary>FR-007/008: the effective EXECUTION mode, distinct from the planner outcome — the release lane or a
    /// planner escalation forces a full suite even when the planner could narrow.</summary>
    public static string EffectiveMode(AffectedPlan plan, Lane lane)
    {
        if (lane == Lane.Release || plan.Outcome == AffectedOutcome.FullGateRequired)
        {
            return GateEffectiveMode.Full;
        }

        return plan.Outcome switch
        {
            AffectedOutcome.Affected when plan.SelectedTests.Count > 0 => GateEffectiveMode.Partial,
            _ => GateEffectiveMode.None,
        };
    }

    private AffectedTestInventory? TryBuildInventory(
        string repositoryRoot,
        AffectedPlan plan,
        Lane lane,
        string configuration)
    {
        try
        {
            string[] solutions = Directory.GetFiles(repositoryRoot, "*.slnx");
            if (solutions.Length != 1)
            {
                return new AffectedTestInventory(0, 0, null, null, null, null, "no single .slnx solution to resolve the test inventory");
            }

            ProjectGraph graph = new ProjectGraphBuilder().Build(repositoryRoot, Path.GetFileName(solutions[0]));

            // Release (and any escalation) runs the full suite, so the selected scope is every unit-test project.
            IReadOnlyList<string> selected = lane == Lane.Release || plan.Outcome == AffectedOutcome.FullGateRequired
                ? graph.Nodes.Values.Where(n => n.IsTestProject).Select(n => n.Path).ToArray()
                : plan.SelectedTests.Select(s => s.ProjectPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            return AffectedTestInventoryProjector.Build(repositoryRoot, graph, plan, selected, configuration);
        }
        catch (Exception ex)
        {
            // Best-effort: never let inventory enrichment crash the trace. An honest unknown beats a missing trace.
            return new AffectedTestInventory(0, 0, null, null, null, null, "test inventory unavailable: " + ex.Message);
        }
    }

    // The change includes code when any changed path is a .cs/.csproj (a docs-only change has none). Mirrors the
    // change-summary source/test categorization without re-reading git.
    private static bool ChangeHasCode(ChangeSetContext change) =>
        change.Files.Any(f =>
            f.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            || f.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
}
