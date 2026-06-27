namespace Hx.Tooling.Contracts;

/// <summary>
/// 012 (FR-020/021): the change-set summary the gate renders next to the affected-test plan — a bounded
/// telemetry view, NEVER a proof input (M1: this is review/telemetry context and must never enter a
/// <c>*ProofHasher</c>). Two tiers: the <b>basic</b> tier (<see cref="ClassesIncluded"/> false) carries the
/// file category counts + lines and a capped file list and is built on every gate run; the <b>detailed</b> tier
/// (<see cref="ClassesIncluded"/> true) additionally carries <see cref="ClassesTouched"/> and is built only at the
/// implement-stage gate over a code change. <see cref="Files"/> and <see cref="ClassesTouched"/> are bounded and
/// deterministically ordered (FR-013/018) — the renderer caps them with an explicit "+N more".
/// </summary>
public sealed record ChangeSummary(
    int Source,
    int Test,
    int Docs,
    int Other,
    int LinesAdded,
    int LinesRemoved,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> ClassesTouched,
    bool ClassesIncluded);

/// <summary>
/// 012 (FR-003/004/005): the measurable affected-test inventory for an implement-stage code gate. Project totals
/// are cheap (the project graph); class/case counts are reported for the already-built selected test assemblies
/// only. The repo-wide class/case <b>total</b> is honestly <c>null</c> with an <see cref="UnknownReason"/> when not
/// cheaply available — never reported as zero and NEVER computed by building unaffected test projects (M3, the
/// clarify decision). Review/telemetry context only — never a proof input (M1).
/// </summary>
public sealed record AffectedTestInventory(
    int SelectedProjects,
    int TotalProjects,
    int? SelectedCases,
    int? TotalCases,
    int? SelectedClasses,
    int? TotalClasses,
    string? UnknownReason);

/// <summary>
/// 012 (FR-007/008/009/014): the operator-facing gate execution trace — the effective scope (docs-only vs code),
/// the two-tier <see cref="ChangeSummary"/>, the best-effort <see cref="AffectedTestInventory"/> (null for a
/// docs-only or non-implement gate), the per-step ladder with durations, and the total elapsed time. Assembled by
/// <c>GateTraceProjector</c> from the <see cref="GateProof"/> + change context + affected plan; carried additively
/// on <see cref="GateRunResult"/>. This is REVIEW/TELEMETRY context — it lives on the envelope, never on the hashed
/// <c>AffectedTestProof.Plan</c>, and no <c>*ProofHasher</c> may depend on it (M1, 008 FR-020).
/// </summary>
public sealed record GateTrace(
    GateScope Scope,
    ChangeSummary Change,
    AffectedTestInventory? Tests,
    IReadOnlyList<GateStep> Steps,
    long TotalMs,
    string EffectiveMode);

/// <summary>The effective affected-test execution mode shown in a <see cref="GateTrace"/> (FR-007).</summary>
public static class GateEffectiveMode
{
    public const string Full = "full";
    public const string Partial = "partial";
    public const string None = "none";
}
