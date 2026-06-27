# 012 — Affected Test Plan Visibility

## Goal

Give operators a clear, trustworthy view of what the affected-test planner decided and how much of the unit-test suite will actually run before release. Today the planner exposes the outcome and selected test projects, but it does not explain enough of the impact decision or show a measurable full-vs-partial test scope. Operators need to know whether the gate will run the full suite, a partial suite, or no tests; why that scope was selected; how much of the overall test inventory it represents; and which test classes are included without listing individual test methods.

The same telemetry MUST also be **rendered in human-readable (non-JSON) output**: an operator watching `hx gate run` in a terminal MUST see the effective scope (docs-only vs code), each gate step's pass/skip/fail with its reason, and the test-scope summary — without reading raw JSON. Today this data already exists in `GateProof` (each step's outcome + evidence message, the `Scope.docsOnly` flag + `scopeSkippedSteps`, the affected-test plan), but the shared CLI renderer collapses it to a single "Gate <lane>: <outcome>" line, so the operator cannot tell a docs-only run from a full code run or see which steps were skipped and why. Surfacing it is the operator's primary "what is actually happening?" need.

**Summary, not a dump.** The telemetry MUST be a concise summary — enough for an operator to glance at the output and tell whether a path is failing (and what is likely causing it) or passing (with basic timing: per-step duration + total elapsed), without scrolling raw lists. Full per-item detail (every changed file, every selected test, every violating object) stays in `--json`; the human surface shows counts + the few most salient items, capped with an explicit "+N more". During **implementation** specifically, the operator also needs a `git diff`-style **change-set summary** — how many source files / test files / docs changed, lines added/removed, and which classes (types) were touched — shown next to the affected-test selection, so they can judge whether the affected-test plan actually covers what changed (is the partial scope *effective*?).

## User Scenarios & Testing

**Priority Mode** — workflow / tooling change: fail-closed safety + deterministic proof before ergonomics.

### Work Item 1 — Explain the impact planner decision (Priority: P1)

An operator runs the affected-test planner and can see the detailed impact reasoning behind the scope decision, not only the final outcome.

- **Why this priority:** The planner is the source of truth for narrowed test scope; if its decision is opaque, a partial gate feels like an unverified shortcut.
- **Independent Test:** Run the planner for a change set that narrows to a subset of tests and verify the output explains the changed paths, path classifications, affected source projects, selected test projects, and reasons.
- **Acceptance Scenarios:**
  1. **Given** a change to a source project covered by one test project, **When** the operator runs `hx impact plan --for tests --json`, **Then** the output shows an `affected` outcome, the changed files considered, the affected source projects, the selected test projects, and why the plan is partial.
  2. **Given** a broad or unattributed change, **When** the operator runs the planner, **Then** the output shows `full-gate-required` and names the specific broad/unattributed inputs that prevented safe narrowing.

### Work Item 2 — Show measurable full vs partial test scope (Priority: P1)

An operator can tell how much of the unit-test suite will be conducted, including total inventory and selected inventory.

- **Why this priority:** A partial run must be visibly proportional to the whole suite; otherwise the operator cannot judge whether the gate is doing meaningful work.
- **Independent Test:** Run the planner for both full and partial cases and verify the output includes total and selected counts for test projects, test classes, and test cases where discovery is available.
- **Acceptance Scenarios:**
  1. **Given** a partial affected plan, **When** the operator reads the planner output, **Then** it shows selected/total test projects, selected/total test classes, selected/total test cases, and a clear partial indicator.
  2. **Given** a full-gate-required plan, **When** the operator reads the planner output, **Then** it shows that the whole test inventory is in scope and explains the full-run reason.

### Work Item 3 — Surface the effective gate execution trace (Priority: P2)

An operator running the gate sees the final execution scope before tests begin, including release-lane overrides that force a full suite even when the planner could narrow.

- **Why this priority:** The planner decides a candidate scope, but the gate owns the actual execution scope; release runs and planner escalations must be visible at the point tests are about to run.
- **Independent Test:** Run `hx gate run --stream --json` for normal and release profiles and verify an affected-test trace event appears before the test execution step and the final gate result contains the same trace.
- **Acceptance Scenarios:**
  1. **Given** an affected partial plan on a normal gate, **When** `hx gate run --stream --json` starts, **Then** it emits a trace showing partial execution before any test project is executed.
  2. **Given** the same affected partial plan on a release gate, **When** `hx gate run --profile release --stream --json` starts, **Then** it emits a trace showing full execution with the release-lane reason.

### Work Item 4 — Keep the trace safe and non-misleading (Priority: P2)

Operators receive exact data when available and explicit unknowns when discovery cannot provide exact counts.

- **Why this priority:** A visibility feature must not turn missing discovery data into false confidence. Zero, unknown, skipped, and full are different states.
- **Independent Test:** Force test inventory discovery to be unavailable and verify the output reports unknown counts with diagnostics rather than reporting zero selected tests.
- **Acceptance Scenarios:**
  1. **Given** test class or test case discovery fails while project selection succeeds, **When** the operator reads the output, **Then** project-level scope is still shown and class/case counts are marked unknown with the reason.
  2. **Given** the planner cannot safely select projects, **When** the gate runs, **Then** the trace shows a full suite rather than an empty or partial scope.

### Work Item 5 — Render the scope and gate ladder in human output (Priority: P1)

An operator running `hx gate run` in a terminal sees the effective scope and what each step did — not just a one-line "Pass" — without needing `--json`.

- **Why this priority:** the per-step outcome + evidence, the `Scope.docsOnly` flag + `scopeSkippedSteps`, and the affected-test plan already exist in `GateProof` and already stream as `CliEvent` step events, but the human renderer (`Hx.Cli.Kernel` `CliRenderer`) ignores the outcome/reason and renders only the rollup. The operator's primary question — "is this doing docs-only or code, and how much testing?" — is unanswerable without raw JSON. This is rendering, not new measurement.
- **Independent Test:** run `hx gate run` (human/TTY) for a docs-only change and for a code change and verify the output shows the scope summary, the per-step ladder (pass/skip/fail + reason), and the affected-test summary in both cases, matching the `--json` proof.
- **Acceptance Scenarios:**
  1. **Given** a docs-only change, **When** the operator runs `hx gate run`, **Then** the human output shows a docs-only scope line, "no tests required", and the scope-skipped steps (architecture/Sentrux) with their reason, plus each step's outcome — without `--json`.
  2. **Given** a code change, **When** the operator runs `hx gate run`, **Then** the human output shows the code scope, the affected-test mode (full/partial with selected/total), and every step's pass/skip/fail.
  3. **Given** the live progress display, **When** a step is scope-skipped or fails, **Then** that step is visually distinct from a passed step and its reason is shown, rather than every step's bar completing identically.

### Work Item 6 — Two-tier change telemetry: basic every gate, detailed at the implement code gate (Priority: P1)

**Every** gate run shows a **basic** summary of what changed since the last step — which files and how many lines — so the operator always sees what the gate is checking. The **detailed** code telemetry (the classes touched + the affected-test selection, for judging whether the partial scope is effective) is shown **only at the post-implementation (implement-stage) gate and only when the change includes code**. A docs-only change — or any non-implement gate — shows just the basic summary, like every other gate.

- **Why this priority:** the basic summary answers "what is this gate even looking at?" on every run; the detailed comparison ("these classes changed → these tests selected → effective?") is only meaningful when code actually changed, which in a cycle is implement. Reserving the detail keeps non-code gates terse and keeps the heavy/specific data where it matters.
- **Independent Test:** run a docs-only gate and verify it shows the basic file+lines summary but no classes/test detail; run an implement gate over a code change and verify it ADDS the classes touched + selected/total test projects.
- **Acceptance Scenarios:**
  1. **Given** any gate run, **When** it runs, **Then** the output shows a basic change summary — the files modified since the last step (capped per FR-018) and total lines added/removed — whether the change is docs or code.
  2. **Given** the implement-stage gate over a code change, **When** it runs, **Then** the output ADDS the classes (types) touched and the affected-test selection (selected/total test projects) so the operator can judge effectiveness.
  3. **Given** a docs-only change (any gate, including the implement gate), **When** it runs, **Then** the output shows only the basic summary — no classes, no test counts (there are no tests).

### Edge Cases

- A docs-only change produces `no-tests-required`; the trace must distinguish "no tests required" from "zero tests discovered".
- A broad shared file change escalates to full gate and must list the broad input reason.
- A release lane forces full execution even when the planner outcome is `affected`.
- Test discovery may be unavailable before build outputs exist; output must mark unknown counts instead of estimating.
- Architecture-test projects filtered out of normal unit-test execution must not inflate the unit-test scope.
- New test projects or renamed test projects must appear deterministically in the total inventory and selected inventory.
- A streamed gate event and the final gate envelope must agree on the effective execution mode.

## Scope

Included:

- Enrich `hx impact plan --for tests --json` with planner decision detail and measurable test-scope inventory.
- Show full, partial, or no-test mode clearly in both JSON and human-readable output.
- Include selected and total counts for test projects, test classes, and test cases where discovery can provide exact data.
- List selected test classes grouped by test project; do not list individual test methods.
- Emit the final effective affected-test execution trace from `hx gate run`, including release/full-suite overrides.
- **Render** the effective scope (docs-only vs code), the per-step gate ladder (pass/skip/fail + reason), and the affected-test summary in **human-readable** `gate run` output through the shared CLI kernel renderer — surfacing the `GateProof` data that exists today but is JSON-only, and using the streamed per-step outcome (not just "done") in the live progress display.
- Show **per-step duration + running total elapsed** in the human output, and a **change-set summary** at the implement gate (source/test/docs file counts, lines ±, classes touched) next to the affected-test plan.
- Keep all human telemetry a **bounded summary** (counts + capped lists with overflow), never a dump; full per-item detail stays in `--json`.
- Preserve existing fail-closed affected-test proof behavior and gate validation.

Excluded:

- Mapping tests to production classes or lines of code.
- Listing individual test method names.
- Replacing the existing project-graph affected-test selection algorithm.
- Using coverage instrumentation as a prerequisite for this feature.
- Editing `README.md` in this specify step; README work is already in the active 011 cycle and any later README mention must be coordinated after that cycle completes.

## Functional Requirements

- `FR-001`: The planner output MUST include a structured impact-decision section showing outcome, changed files considered, path classifications, affected source projects, selected test projects, and planner reasons. `[Work Item 1]`
- `FR-002`: For a full-gate-required outcome, the planner output MUST identify the concrete reason(s) that prevented safe narrowing, such as broad inputs, graph findings, or unattributed paths. `[Work Item 1]`
- `FR-003`: For an affected partial outcome, the planner output MUST state that execution is partial and show selected test projects against total test projects. `[Work Item 2]`
- `FR-004`: When exact discovery data is available, the planner output MUST show selected/total test case counts and selected/total test class counts. `[Work Item 2]`
- `FR-005`: When exact test class or test case discovery is unavailable, the output MUST mark those measures as unknown with a reason and MUST NOT report unknown counts as zero. `[Work Item 4]`
- `FR-006`: The planner output MUST list selected test classes grouped by selected test project and MUST NOT list individual test method names. `[Work Item 2]`
- `FR-007`: The gate output MUST emit an affected-test execution trace before the test execution step begins, showing the effective mode: `full`, `partial`, or `none`. `[Work Item 3]`
- `FR-008`: The gate execution trace MUST distinguish planner outcome from effective execution mode, including release-lane full-suite overrides and planner full-gate escalations. `[Work Item 3]`
- `FR-009`: The final `gate run` envelope MUST include the same effective affected-test trace that was streamed, so non-stream consumers can inspect the scope after completion. `[Work Item 3]`
- `FR-010`: Human-readable output MUST summarize the same full/partial/no-test scope and the same selected/total measures that JSON exposes. `[Work Item 2]`
- `FR-011`: The trace MUST preserve the existing affected-test proof invariants: planner hash, test-scope hash, executed-tests hash, full-suite flag, and gate-proof validation remain authoritative for transition/release proof. `[Work Item 4]`
- `FR-012`: The trace MUST make architecture-test filtering visible when reporting unit-test totals, so operators can tell which test inventory is counted as normal unit-test execution. `[Work Item 4]`
- `FR-013`: The trace MUST use deterministic ordering for projects, classes, changed files, and reasons so repeated runs over the same change set produce stable output. `[Work Item 4]`
- `FR-014`: The human (non-JSON) `gate run` output MUST render an **effective-scope summary** — docs-only vs code, and the affected-test mode (`full`/`partial`/`none`) with the selected/total measures from WI-2 — derived from the same `GateProof`/trace the JSON exposes. `[Work Item 5]`
- `FR-015`: The human `gate run` output MUST render the **per-step ladder**: each step's name, outcome (pass/skip/fail), and its reason/evidence message, including the scope-skipped steps and why they were skipped. `[Work Item 5]`
- `FR-016`: The live progress display MUST **distinguish step outcomes** — a scope-skipped or failed step MUST be visually distinct from a passed step and MUST surface its reason — rather than completing every step's bar identically (the streamed `CliEvent` already carries the outcome + message; it MUST be used). `[Work Item 5]`
- `FR-017`: All new human rendering MUST go through the **shared CLI kernel renderer** (no direct console writes) and MUST be derived from the existing `GateProof`/`AffectedTestProof`/scope data — one source of truth, no separate human-only computation that could diverge from the JSON or the proof. `[Work Item 5]`
- `FR-018`: **Summary, not dump.** All human telemetry (scope, ladder, trace, change-set, and any violation detail) MUST be a **bounded summary** — counts + the few most salient items, capped with an explicit "+N more" — with full lists available only in `--json`. The human surface MUST NOT dump full file/test/violation lists. `[Work Item 5, 6]`
- `FR-019`: The human gate output MUST show **per-step duration** and a **running total elapsed**, so a passing run still gives basic timing telemetry at a glance. `[Work Item 5]`
- `FR-020`: **Every** gate run MUST show a **basic change-set summary** — the files modified in the gate's change set (since the last step / the cycle base): a count plus the changed file names capped per FR-018, and total lines added/removed — regardless of whether the change is docs or code. `[Work Item 6]`
- `FR-021`: The **detailed code telemetry** — the classes (types) touched plus the affected-test selection (selected/total test projects, with class/case counts best-effort per FR-004/005) for judging effectiveness — MUST be shown ONLY at the **implement-stage gate AND only when the change set includes code**; a docs-only change, or any non-implement gate, shows only the basic summary (FR-020). The implement-stage condition is a **rendering** decision derived from the cycle context; the gate engine stays stage-agnostic. `[Work Item 6]`

## Success Criteria

- `SC-001`: For a partial source change in this repository, `hx impact plan --for tests --json` shows `partial` scope with selected/total test project counts and the selected test project names.
- `SC-002`: For a broad change, `hx impact plan --for tests --json` shows `full-gate-required` with at least one concrete escalation reason and no implication that tests were skipped.
- `SC-003`: For a discoverable test inventory, planner or gate output reports selected/total test cases and selected/total test classes; selected test classes are listed, individual test methods are not.
- `SC-004`: For unavailable test discovery, output marks class/case measures unknown with a reason and still reports the project-level scope.
- `SC-005`: `hx gate run --stream --json` emits an affected-test trace event before `restore-build-test`, and the final `GateRunResult` carries the same effective trace.
- `SC-006`: Release-profile gate output reports full execution even when the planner would otherwise be partial.
- `SC-007`: Existing affected-test proof validation still rejects stale, forged, mismatched, or wrong full-suite proofs.
- `SC-008`: For a docs-only gate, the human (non-JSON) `hx gate run` output shows a docs-only scope line, the scope-skipped steps with reasons, and "no tests required" — matching the JSON proof, with no `--json` required.
- `SC-009`: For a code gate, the human output shows the code scope, the affected-test mode (full/partial with selected/total), and each step's pass/skip/fail outcome.
- `SC-010`: In the live progress display, a scope-skipped or failed step is visually distinct from a passed step, and its reason is shown.
- `SC-011`: Every gate shows a basic change summary (files modified + lines ±); the implement-stage gate over a **code** change additionally shows the classes touched + selected/total test projects, while a docs-only change shows only the basic summary.
- `SC-012`: All human telemetry is bounded — counts + capped lists with "+N more"; no full file/test/violation dump appears in the human output (full detail is `--json` only).
- `SC-013`: A passing gate shows per-step duration and total elapsed time.

## Key Entities

- **Impact Decision** — the planner's explanation of the changed inputs, classifications, affected projects, selected tests, and reasons.
- **Test Inventory** — the total discoverable unit-test scope for the repository after applying the gate's unit-test filter.
- **Affected Test Trace** — the operator-facing summary of planned and effective test execution scope, including full/partial/none mode, counts, selected projects, selected test classes, and unknown-count diagnostics.
- **Effective Execution Mode** — the gate's final scope decision after lane policy and planner escalation are applied.

## Deterministic Surfaces

- `hx impact plan --repo . --for tests --json` — existing planner command; enriched output is planned by this feature.
- `hx gate run --repo . --profile <auto|advisory|normal|release> --stream --json` — existing stream surface; affected-test trace event is planned by this feature.
- `GateRunResult` / final `CliResult.data` — existing final envelope; affected-test trace payload is planned by this feature.
- `AffectedPlan` — existing planner contract; new detail may be an additive contract or nested trace structure.
- `AffectedTestProof` — existing proof contract; remains the transition/release authority and is not weakened by visibility data.
- `ProjectGraph` and test discovery output — deterministic sources for project inventory and class/case measures.

## Architecture Impact

- Production code and contracts are affected: the change touches CLI contracts, impact planning output, gate runner output, and possibly shared CLI rendering.
- The change must preserve thin command bodies and keep output rendering through the shared CLI kernel.
- The change likely touches `tools/Hx.Tooling.Contracts`, `tools/Hx.Impact.Core`, `tools/Hx.Impact.Cli`, `tools/Hx.Gate.Core`, and `tools/Hx.Runner.Cli`.
- The WI-5 human rendering lives in the shared CLI kernel (`tools/Hx.Cli.Kernel` `CliRenderer` — the live-progress `OnEvent` path that currently ignores `CliEvent.Status`, and the `WriteSummary` panel that currently renders only `CliResult.Summary`); it surfaces existing `GateProof.Steps`/`Scope`/`AffectedTestProof` data, so it is a rendering + small trace-contract change, NOT new measurement (the new measurement is WI-2's class/case counts). One source of truth: human and JSON render the same trace.
- The WI-6 change-set summary reuses the existing `ChangeSetContext` (008 — changed files, categories, affected source projects); the "classes touched" measure extracts top-level type names from the changed `.cs` (a lightweight parse, no Roslyn dependency — the 009 `Hx.Embedding.Core` member-chunker is a precedent for brace-aware C# scanning). Per-step timing is captured in the gate runner/kernel (today only total `ElapsedMs` exists). FR-018's "summary not dump" is a rendering rule enforced by capping in the kernel renderer; full lists remain in `--json`.
- Architecture tests and Sentrux should run because this is a runtime tooling and contract change, not docs-only.

## Sentrux And Hygiene Impact

- No Sentrux baseline change is expected.
- New contracts and planner/gate logic must remain library-first with thin CLI adapters.
- JSON output remains agent-first and must avoid direct console writes outside the shared CLI kernel.
- Public hygiene impact is low unless release-facing docs are updated after implementation.

## Assumptions

- "Classes being tested" means test classes that contain discoverable test cases, not production classes under test. Mapping production classes to test coverage is out of scope because it requires coverage instrumentation or an explicit mapping contract.
- "Actual tests being used" means individual test method names; counts may include test cases, but method names are not listed.
- Exact test class/test case counts are required when discovery can produce them; when discovery cannot run, the product must surface unknowns honestly rather than estimate.
- The gate remains the source of truth for actual execution scope because release lanes and planner escalations can force full execution after the planner has produced its candidate scope.

## Acceptance

- Command-backed today: `hx impact plan --repo . --for tests --json`, `hx gate run --repo . --profile normal --stream --json`, `hx gate run --repo . --profile release --stream --json`, `dotnet test`, `architecture test`, `sentrux check`, `doti render-skills --check`, and `doti payload check`.
- Planned by this feature: richer impact-plan details, test inventory counts, selected test class lists, and affected-test trace payloads.
- Advisory until implemented: exact class/case inventory shape and any human-readable formatting beyond the existing shared CLI renderer.

## Clarifications

### Clarify session 2026-06-28

- **Q: Does the affected-test planner / change-set summary run at every step, including the doc stages and docs-only changes?**
  **A:** No. The gate (and the affected-test planner) run only at the **implement** stage and the diff/release transitions; the doc stages (`specify`–`arch-review`) stamp their doc with no gate. For a **docs-only** change the planner does only a cheap change classification, concludes **no-tests-required**, and the gate runs **no build, no tests, no architecture, no Sentrux** (verified on cycle 011: 0 tests executed; those steps skipped). The planner cannot be skipped entirely because that cheap classification IS what determines "docs-only → skip the test work," but it never builds or enumerates for docs. **Therefore the change-set summary + affected-test detail (FR-020/021, WI-6) render ONLY for code-affecting gate runs (the implement stage in a cycle); a docs-only gate shows only the docs-only scope line — no test counts and no change-vs-tests comparison, since there are no tests.** The affected-test telemetry (WI-1/WI-2) is inherently a code-change concern and is not produced for docs-only changes beyond the "no-tests-required" verdict.

- **Q: Show the change-set summary on every gate, or only at implement? And how to handle the test totals given the enumeration cost?**
  **A (operator):** **Two tiers.** (1) **Every** gate run shows a **basic** summary of what changed since the last step — which files and how many lines — whether docs or code (FR-020). (2) The **detailed** code telemetry — the classes touched + the affected-test selection for judging effectiveness — is shown **only at the post-implementation (implement-stage) gate AND only when the change includes code** (FR-021); a docs-only change outputs just the basic summary like every other gate. The implement-stage condition is a rendering decision derived from the cycle context — the gate engine stays stage-agnostic. For the test totals: the repo-wide class/case **total** is never worth a full build (option A) — the cheap denominator is selected/total **test projects** (from the project graph); class/case counts are reported for the selected (already-built) projects; the repo-wide class/case total is best-effort and marked unknown if not cheaply available, rather than building every test project for a denominator.
