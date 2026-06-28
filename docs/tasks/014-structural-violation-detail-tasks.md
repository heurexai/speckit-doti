# Tasks — 014 Structural-Engine Violation Detail

**Plan:** [docs/plans/014-structural-violation-detail-plan.md](../plans/014-structural-violation-detail-plan.md). **Stage:** `/04-doti-tasks`.

Code feature: additive render-only violation detail for ArchUnitNET + Sentrux, captured in `*.Core`, rendered in the kernel ladder + standalone commands. The load-bearing invariant: the persisted gate proof + `gateProofDigest` stay byte-identical (FR-007/SC-006). Tasks ordered so contracts land first, capture second, render third, proof last.

## Phase 0 — Contracts (additive, render-only)

- [x] T001 [test] Contract additivity + proof-boundary: a pre-014 `GateProof`/`PersistedGateProof` still deserializes; `DigestOf(PersistedGateProof)` is byte-identical with a null vs a fully-populated `GateTrace.StructuralViolations`; the new records are nullable/defaulted — `test/Hx.Gate.Tests/` — [covers FR-007, SC-006] <!-- doti-task-hash: b53cf3b21e183c9b4367c28d4ee4e3b801d9c7d59568778eb163da7776fddf02 -->
- [x] T002 Add the records to `Hx.Tooling.Contracts`: `ArchitectureViolation(Rule, Description, ViolatingObjects, UnknownReason?)`; `SentruxViolation(Rule, File?, Function?, Line?, MeasuredValue?, Limit?, Message?, UnknownReason?)`; `StructuralStepViolations(StepName, Architecture, Sentrux)`; `ArchitectureTestCase += IReadOnlyList<ArchitectureViolation>? Violations`; `SentruxCheckResult += IReadOnlyList<SentruxViolation>? RuleViolationDetails`; `GateTrace += IReadOnlyList<StructuralStepViolations>? StructuralViolations` — all nullable/defaulted; `RuleViolations` (string) unchanged — `tools/Hx.Tooling.Contracts/` — [covers FR-002, FR-003] <!-- doti-task-hash: 7dd86210be4e426f8d2a602fc77dd739670cac53a0f01f76dbc00b223fc5b7bd -->

## Phase 1 — ArchUnit capture (WI-1)

- [x] T003 [test] `ArchitectureTestRunner` TRX parse extracts `ArchitectureViolation`s from a failing `UnitTestResult`'s `Output/ErrorInfo/Message` (crafted TRX fixture with an emitted description block); a failing test whose message is unparseable yields one violation with `UnknownReason` (never empty); a passing run yields no violations — `test/Hx.Runner.Tests/` (or `test/Hx.Architecture.Tests` host) — [covers FR-001, FR-005] <!-- doti-task-hash: a7e14f32791837e64d1e664896097018073358ee7b729d35849e1d38fb8ee2ee -->
- [x] T004 Extend `ArchitectureTestRunner.ParseTrx` to read the failure message and populate `ArchitectureTestCase.Violations` (deterministic order; fail-closed `UnknownReason` on parse failure) — `tools/Hx.Runner.Core/ArchitectureGate/ArchitectureTestRunner.cs` — [covers FR-001, FR-005, FR-006] <!-- doti-task-hash: a3e2931d8401cdbf609c864cb70f55402e337d820e1d37b27346c1e9f2451ec3 -->
- [x] T005 Update the architecture tests to evaluate-and-emit: assert via `rule.Evaluate(Arch)` and, on failure, emit `EvaluationResult.Description` + the failing object names into the assertion failure message (a single deterministic block the runner parses); outcomes unchanged (still fail on violation) — `test/Hx.Architecture.Tests/ArchitectureTests.cs` AND `scaffold/templates/dotnet-cli/test/HxScaffoldSample.Architecture.Tests/ArchitectureTests.cs` (generated-code template, must still compile + pass green) — [covers FR-001, FR-007] <!-- doti-task-hash: 2c344efaaab7ee0a47152e10a68c708a60a258e2b2ba7547757ec48ea707248a -->

## Phase 2 — Sentrux capture (WI-2)

- [x] T006 [test] `SentruxOutputParser` yields a structured `SentruxViolation` per object (rule/file/function/line/value where present) alongside the existing flattened string; a summary-style rule with no per-function attribution (max_cc message / whole-graph signal) → `UnknownReason`, not zero/fabricated — `test/Hx.Sentrux.Tests/` — [covers FR-003, FR-005] <!-- doti-task-hash: 4e82acec6a99f3537d84907248a6f4654b3ac4ad879dbc12d8ca10346483687a -->
- [x] T007 `SentruxOutputParser` preserves the structured violation it already parses; `SentruxChecker.Build` sets `SentruxCheckResult.RuleViolationDetails`; `RuleViolations` string list unchanged — `tools/Hx.Sentrux.Core/` — [covers FR-003, FR-005] <!-- doti-task-hash: f69f577e0b14c7b95391a2ca39ecfa0ed34292a3e30a7e6d1f400d786cf6d29a -->

## Phase 3 — Render (WI-3)

- [x] T008 [test] `GateTraceProjector` sets `GateTrace.StructuralViolations` from the rich arch/sentrux results for the failing `architecture-test`/`sentrux-*` steps; the `GateProof`/`GateStep` it builds is unchanged (summary evidence only) — `test/Hx.Gate.Tests/` — [covers FR-004, FR-007] <!-- doti-task-hash: be5a2be75e1b0785a8e94c727fca3bb510b83a9a427bacd4612724e0e386a66a -->
- [x] T009 `GateRunner` threads the rich `ArchitectureTestResult`/`SentruxCheckResult` to `GateTraceProjector`; the projector populates `StructuralViolations` on the trace envelope only — `tools/Hx.Gate.Core/GateRunner.cs`, `tools/Hx.Gate.Core/GateTraceProjector.cs` — [covers FR-004, FR-007] <!-- doti-task-hash: 7abae7a062251d030a5e398ee6ffdc95adc9ab0b186e319c9626f15480b60180 -->
- [x] T010 [test] `CliRenderer` renders offenders under each FAILING structural ladder step as concise one-line summaries, deterministically ordered, capped with "+N more"; a passing run shows the structural steps with no offender noise — `test/Hx.Cli.Kernel.Tests/` — [covers FR-004, FR-006] <!-- doti-task-hash: 4f10e753ec878d73b823206958ebefa0ac8147d9324034664c63a779b3ef3436 -->
- [x] T011 `CliRenderer.WriteGateSummary` (gate ladder) + the standalone `architecture test`/`sentrux check` render path emit the offender summaries from `StructuralViolations`/`Violations`/`RuleViolationDetails`; full set stays in `--json` — `tools/Hx.Cli.Kernel/CliRenderer.cs` (+ standalone command render) — [covers FR-004, FR-006] <!-- doti-task-hash: bdc740cb432c9fa1a9aa71dbec82695eef3e9bdfc9879ed041491131f6fc92f7 -->

## Phase 4 — Verify (the deterministic proof)

- [x] T012 Extend the proof-hash-boundary ArchUnit assertion (`test/Hx.Architecture.Tests`) so no `*ProofHasher` references `ArchitectureViolation`/`SentruxViolation`/`StructuralStepViolations`; load-guard the three records — `test/Hx.Architecture.Tests/ArchitectureTests.cs` — [covers FR-007, SC-006] <!-- doti-task-hash: 24a358e9371dc5207379fef365e484b7aa80194df9a097335ba0872f81e88990 -->
- [x] T013 `gate run --profile normal` green over the change set; the architecture/sentrux outcomes + `gateProofDigest` unchanged by the added detail (visibility-only); all regressions green; stamp implement on green — [covers SC-006, FR-007] <!-- doti-task-hash: 0fbc4a2cf8e2f3d30f88419f0444da79c50033c716a7d16e2e37ce8c000bd22f -->

## Phase 5 — Operator-directed Sentrux scope correction (mid-implement, see spec Clarifications)

- [x] T014 Scope Sentrux to production code: add `test/` to `.sentruxignore` (exclude the repo's test tree from the scan + quality graph), then raise the baseline once to the production-only signal via the authorized `hx sentrux baseline --authorize-rebaseline` (FR-031: change-set-fresh arch-review + `functionality-driven-growth` classification). Verify production-only god files stay at 5 (no 014 regression) and the gate's sentrux-check passes against the raised baseline — `.sentruxignore`, `.sentrux/baseline.json` — [covers spec Clarifications / Sentrux And Hygiene Impact] <!-- doti-task-hash: 978be8a0d753c10cbfecfa53b97a8f617d9c27897bedfaecd8d8ceceb391b2bb -->

## Coverage

- FR-001 → T003, T004, T005 | FR-002 → T002 | FR-003 → T002, T006, T007 | FR-004 → T008–T011 | FR-005 → T003, T004, T006, T007 | FR-007/SC-006 → T001, T008, T012, T013 | Sentrux scope correction → T014.
