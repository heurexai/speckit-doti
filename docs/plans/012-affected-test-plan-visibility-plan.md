# 012 — Gate & Affected-Test Visibility — Plan

## Summary

Make `hx gate run` legible to a human without `--json`: render the effective scope (docs-only vs code), a two-tier change-set summary (basic on every gate; classes + affected-test detail only at the implement code gate), the per-step ladder (pass/skip/fail + reason + duration), and the affected-test plan decision/counts — all as a bounded **summary, not a dump**. The data largely already exists in `GateProof` (steps, `GateScope`, `AffectedTestProof`/`AffectedPlan`); the technical approach is **additive capture** (per-step `DurationMs`, a change-set summary, a best-effort test inventory) assembled by a `*.Core` projector into one `GateTrace` carried in `GateRunResult`, with the **kernel renderer** (`Hx.Cli.Kernel`) showing it — human and JSON from one source of truth (FR-017). Fail-closed proof behavior is untouched (visibility only).

## Technical Context

- **Stack/patterns:** gate logic in `Hx.Gate.Core` (`GateRunner.Run` emits `CliEvent` per step + returns `GateProof`); the affected planner in `Hx.Impact.Core`; contracts in `Hx.Tooling.Contracts`; the thin gate command `RunnerCommands.GateRun` → `CliResult`; human rendering ONLY in `Hx.Cli.Kernel` `CliRenderer` (the `RunWithLiveProgress` step bars + `WriteSummary` panel). JSON is `CliWriter` (byte-stable, untouched).
- **Verified gaps (the design rests on these):** `CliRenderer.OnEvent` reads only "running vs done" and **discards `CliEvent.Status` (the outcome) + the message** (`CliRenderer.cs:221`); `WriteSummary` renders only `result.Summary` + diagnostics (`CliRenderer.cs:284`). `GateStep(Name, Outcome, Evidence)` has **no duration**. `ChangedFile(Path, Status, OldPath)` has **no line counts**. `AffectedPlan` has Outcome/SelectedTests/Reasons/ChangedFiles but **no class/case inventory**. `GateRun` already streams `new CliEvent("step", name, outcome, evidence-message)` — the outcome + message are there, unused.
- No `[NEEDS CLARIFICATION]` — the two blocking ambiguities are resolved in the spec's `## Clarifications` (two-tier change telemetry; test totals never build-all).

## Constitution Check (gate)

Via `hx doti constitution` (§2: pure-`*.Core`/thin-CLI, deterministic fail-closed gates, JSON-first). **PASS.** Capture/projection logic lives in `*.Core`; the CLI delta is render-only (the kernel is the one human-output home — Channel Independence preserved). Bootstrap Honesty: nothing is downgraded — the gate's pass/fail, the proof, and all validation are unchanged (FR-011); the telemetry is visibility-only. Deterministic Ownership: the trace is derived from the same `GateProof`/`ChangeSetContext` the proof uses; it is review context, never a proof-hash input (preserves the 008 FR-020 boundary — `ChangeSetContext` must never enter a proof hash). No §2 convention bent.

## Research (resolve unknowns)

- **R1 — telemetry envelope home.** *Decision:* a `GateTrace` record in `Hx.Tooling.Contracts`, carried additively on `GateRunResult`; the kernel renders it (the kernel already references Contracts). *Rationale:* one source of truth for human + JSON (FR-017); `GateRunResult` is already the gate's `CliResult.Data`. *Alternatives rejected:* a generic kernel "render-model" abstraction (premature for one command — YAGNI; the kernel already special-cases via the live-progress path); a human-only computation (would drift from JSON — FR-017 forbids).
- **R2 — capture lines ± and classes touched.** *Decision:* lines from `git diff --numstat base..HEAD` (∪ working tree) in a `*.Core` helper; classes touched from a lightweight C# top-level-type scanner over changed `.cs` (brace/lexer-aware, reusing the 009 `CSharpMemberChunker` precedent — zero Roslyn). Files/categories from the existing `ChangeSetContext`. *Rationale:* cheap, no new heavy dep; matches the 009 chunker precedent. *Alternatives rejected:* Roslyn (disproportionate for type-name extraction); extend `ChangedFile` with lines (couples the review-context contract to a numstat concern — keep the change summary a separate record).
- **R3 — affected-test inventory (class/case counts).** *Decision:* selected/total **test projects** from the project graph (cheap); class/case counts for the **selected (already-built) projects** via enumerating the built test assemblies; repo-wide class/case **total** best-effort, marked `unknown — not enumerated` when not cheaply available. *Rationale:* preserves the affected-test optimization (the `## Clarifications` decision — never build all test projects for a denominator); honest unknowns (FR-005). *Alternatives rejected:* build/enumerate all test projects (defeats the optimization — explicitly rejected in clarify); a cached inventory artifact (a new freshness-tracked file — drift risk).
- **R4 — the implement-stage detail condition.** *Decision:* the detailed tier (classes + affected-test) renders only when (a) the change set includes code AND (b) the run is the implement-stage gate; the stage is derived in the **render** from the cycle context (a flag the gate command resolves from `CycleStateStore`), so `GateRunner` stays stage-agnostic. *Rationale:* the operator's two-tier decision; keeps the gate engine uncoupled from the cycle (Q1 consequence). *Alternatives rejected:* couple `GateRunner` to the cycle state (spreads the cycle dependency into the stage-agnostic engine).

## Design

**Selection rule applied:** simplest correct + modular — additive contracts, one `*.Core` projector per concern, render-only CLI delta.

- **Contracts (`Hx.Tooling.Contracts`, all additive — no schema break):**
  - `GateStep` += `long? DurationMs = null` (FR-019).
  - `ChangeSummary(int Source, int Test, int Docs, int Other, int LinesAdded, int LinesRemoved, IReadOnlyList<string> Files, IReadOnlyList<string> ClassesTouched, bool ClassesIncluded)` — the basic + detailed change telemetry (FR-020/021).
  - `AffectedTestInventory(int SelectedProjects, int TotalProjects, int? SelectedCases, int? TotalCases, int? SelectedClasses, int? TotalClasses, string? UnknownReason)` (FR-003/004/005).
  - `GateTrace(GateScope Scope, ChangeSummary Change, AffectedTestInventory? Tests, IReadOnlyList<GateStep> Steps, long TotalMs, string EffectiveMode)` — the operator-facing trace (FR-007/008/009).
  - `GateRunResult` += `GateTrace? Trace = null`.
- **`*.Core` units:**
  - `Hx.Impact.Core/ChangeSummaryProjector` — files-by-category + lines (numstat) + classes-touched (the C# scanner); single responsibility, IO via an injected git runner (testable).
  - `Hx.Impact.Core/AffectedTestInventory` computation — extend the planner to emit project totals + selected class/case from built assemblies (best-effort), `unknown` otherwise.
  - `Hx.Gate.Core/GateTraceProjector` — assemble `GateTrace` from the `GateProof` + `ChangeSetContext` + `AffectedPlan` + the per-step durations + the (injected) `implement-stage code` flag. Pure; unit-tested without a gate run.
  - `GateRunner.Run` — wrap each step in a `Stopwatch`; populate `GateStep.DurationMs`.
- **Kernel render (`Hx.Cli.Kernel/CliRenderer`):**
  - `OnEvent`: color the step bar by `CliEvent.Status` (skip = muted + reason, fail = red + reason); use the message (FR-016).
  - `WriteGateSummary(GateTrace)`: the scope line, the basic change summary (every gate), the detailed line (implement+code), the per-step ladder (icon · name · duration · terse reason), total elapsed — all bounded/capped (FR-018). Rendered when `result.Data is GateRunResult { Trace: { } }`.
- **CLI:** `gate run` gains `--stream` (codex WI-3) — NDJSON `CliEvent`s before `restore-build-test` + the final trace in the envelope; thin wiring only (the events already exist).

**Architecture delta.** No new project; no namespace move. New `*.Core` types (`ChangeSummaryProjector`, `GateTraceProjector`, the inventory computation) sit behind the existing thin-CLI families — `cliSurfaceConfinement`/`cliDelegation` already confine `*Projector` (008 BL-5). **No ArchUnit family change.** Sentrux: new `*.Core` logic stays within the function-size limit; no layer/boundary change. **The 008 FR-020 boundary is preserved by construction** — `GateTrace`/`ChangeSummary` are review context and are NOT added to `AffectedTestProofHasher` (an architecture test already pins "proof hashers ↛ ChangeSetContext"; the new records must stay out of the hashers too — add a parity assertion).

## CLI surface & error contract

`gate run` adds `--stream` (Boolean) — no new error code, no new exit class (Success/Validation unchanged). The `describe` entry gains `--stream`. Envelope: `CliResult` with `GateRunResult.Trace` (additive). Channel boundary: the trace is built in `Hx.Gate.Core` (`GateTraceProjector`); the CLI renders only (kernel). No direct console writes outside the kernel.

## Command Availability

| Area | Command | Status |
| --- | --- | --- |
| Gate (human + JSON trace) | `hx gate run --profile normal [--stream] [--json]` | implemented; trace **planned (this feature)** |
| Affected plan detail | `hx impact plan --for tests --json` | implemented; richer detail **planned** |
| Gate | `hx gate run` | implemented — gates this change |

No planned gate downgraded; the telemetry is visibility-only and never gates.

## Complexity Tracking

(Empty — Constitution Check surfaced no violation.)

## Risks

- **Proof-hash leakage (highest):** the new review-context records (`GateTrace`/`ChangeSummary`) must never enter `AffectedTestProofHasher`/the proof hashes (008 FR-020). Mitigation: an ArchUnit parity assertion (`*ProofHasher ↛ GateTrace/ChangeSummary`) + a test that the proof hash is unchanged by adding the trace.
- **Test inventory cost:** the class/case counts must not trigger a build of unaffected test projects. Mitigation: enumerate only already-built (selected) assemblies; total class/case = `unknown` otherwise (R3) — verified against the clarify decision.
- **Kernel/gate coupling:** the kernel rendering `GateRunResult` is a (small, Contracts-level) coupling. Acceptable — the gate is first-class and `GateRunResult` is shared; revisit only if a second command needs the same shape (then extract a generic model).
- **Determinism:** classes-touched + ladder ordering must be deterministic (FR-013) — sort Ordinal; cap with "+N more".
