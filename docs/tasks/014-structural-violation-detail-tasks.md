# Tasks — 014 Structural-Engine Violation Detail

**Plan:** [docs/plans/014-structural-violation-detail-plan.md](../plans/014-structural-violation-detail-plan.md). **Stage:** `/04-doti-tasks`.

Code feature: additive render-only violation detail for ArchUnitNET + Sentrux, captured in `*.Core`, rendered in the kernel ladder + standalone commands. The load-bearing invariant: the persisted gate proof + `gateProofDigest` stay byte-identical (FR-007/SC-006). Tasks ordered so contracts land first, capture second, render third, proof last.

## Phase 0 — Contracts (additive, render-only)

- [ ] T001 [test] Contract additivity + proof-boundary: a pre-014 `GateProof`/`PersistedGateProof` still deserializes; `DigestOf(PersistedGateProof)` is byte-identical with a null vs a fully-populated `GateTrace.StructuralViolations`; the new records are nullable/defaulted — `test/Hx.Gate.Tests/` — [covers FR-007, SC-006]
- [ ] T002 Add the records to `Hx.Tooling.Contracts`: `ArchitectureViolation(Rule, Description, ViolatingObjects, UnknownReason?)`; `SentruxViolation(Rule, File?, Function?, Line?, MeasuredValue?, Limit?, Message?, UnknownReason?)`; `StructuralStepViolations(StepName, Architecture, Sentrux)`; `ArchitectureTestCase += IReadOnlyList<ArchitectureViolation>? Violations`; `SentruxCheckResult += IReadOnlyList<SentruxViolation>? RuleViolationDetails`; `GateTrace += IReadOnlyList<StructuralStepViolations>? StructuralViolations` — all nullable/defaulted; `RuleViolations` (string) unchanged — `tools/Hx.Tooling.Contracts/` — [covers FR-002, FR-003]

## Phase 1 — ArchUnit capture (WI-1)

- [ ] T003 [test] `ArchitectureTestRunner` TRX parse extracts `ArchitectureViolation`s from a failing `UnitTestResult`'s `Output/ErrorInfo/Message` (crafted TRX fixture with an emitted description block); a failing test whose message is unparseable yields one violation with `UnknownReason` (never empty); a passing run yields no violations — `test/Hx.Runner.Tests/` (or `test/Hx.Architecture.Tests` host) — [covers FR-001, FR-005]
- [ ] T004 Extend `ArchitectureTestRunner.ParseTrx` to read the failure message and populate `ArchitectureTestCase.Violations` (deterministic order; fail-closed `UnknownReason` on parse failure) — `tools/Hx.Runner.Core/ArchitectureGate/ArchitectureTestRunner.cs` — [covers FR-001, FR-005, FR-006]
- [ ] T005 Update the architecture tests to evaluate-and-emit: assert via `rule.Evaluate(Arch)` and, on failure, emit `EvaluationResult.Description` + the failing object names into the assertion failure message (a single deterministic block the runner parses); outcomes unchanged (still fail on violation) — `test/Hx.Architecture.Tests/ArchitectureTests.cs` AND `scaffold/templates/dotnet-cli/test/HxScaffoldSample.Architecture.Tests/ArchitectureTests.cs` (generated-code template, must still compile + pass green) — [covers FR-001, FR-007]

## Phase 2 — Sentrux capture (WI-2)

- [ ] T006 [test] `SentruxOutputParser` yields a structured `SentruxViolation` per object (rule/file/function/line/value where present) alongside the existing flattened string; a summary-style rule with no per-function attribution (max_cc message / whole-graph signal) → `UnknownReason`, not zero/fabricated — `test/Hx.Sentrux.Tests/` — [covers FR-003, FR-005]
- [ ] T007 `SentruxOutputParser` preserves the structured violation it already parses; `SentruxChecker.Build` sets `SentruxCheckResult.RuleViolationDetails`; `RuleViolations` string list unchanged — `tools/Hx.Sentrux.Core/` — [covers FR-003, FR-005]

## Phase 3 — Render (WI-3)

- [ ] T008 [test] `GateTraceProjector` sets `GateTrace.StructuralViolations` from the rich arch/sentrux results for the failing `architecture-test`/`sentrux-*` steps; the `GateProof`/`GateStep` it builds is unchanged (summary evidence only) — `test/Hx.Gate.Tests/` — [covers FR-004, FR-007]
- [ ] T009 `GateRunner` threads the rich `ArchitectureTestResult`/`SentruxCheckResult` to `GateTraceProjector`; the projector populates `StructuralViolations` on the trace envelope only — `tools/Hx.Gate.Core/GateRunner.cs`, `tools/Hx.Gate.Core/GateTraceProjector.cs` — [covers FR-004, FR-007]
- [ ] T010 [test] `CliRenderer` renders offenders under each FAILING structural ladder step as concise one-line summaries, deterministically ordered, capped with "+N more"; a passing run shows the structural steps with no offender noise — `test/Hx.Cli.Kernel.Tests/` — [covers FR-004, FR-006]
- [ ] T011 `CliRenderer.WriteGateSummary` (gate ladder) + the standalone `architecture test`/`sentrux check` render path emit the offender summaries from `StructuralViolations`/`Violations`/`RuleViolationDetails`; full set stays in `--json` — `tools/Hx.Cli.Kernel/CliRenderer.cs` (+ standalone command render) — [covers FR-004, FR-006]

## Phase 4 — Verify (the deterministic proof)

- [ ] T012 Extend the proof-hash-boundary ArchUnit assertion (`test/Hx.Architecture.Tests`) so no `*ProofHasher` references `ArchitectureViolation`/`SentruxViolation`/`StructuralStepViolations`; load-guard the three records — `test/Hx.Architecture.Tests/ArchitectureTests.cs` — [covers FR-007, SC-006]
- [ ] T013 `gate run --profile normal` green over the change set; the architecture/sentrux outcomes + `gateProofDigest` unchanged by the added detail (visibility-only); all regressions green; stamp implement on green — [covers SC-006, FR-007]

## Coverage

- FR-001 → T003, T004, T005 | FR-002 → T002 | FR-003 → T002, T006, T007 | FR-004 → T008–T011 | FR-005 → T003, T004, T006, T007 | FR-006 → T004, T010, T011 | FR-007/SC-006 → T001, T008, T012, T013.
