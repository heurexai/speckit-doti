# Arch Review — 014 Structural-Engine Violation Detail

**Stage:** `/06-doti-arch-review`. **Change under review:** [spec](../specs/014-structural-violation-detail.md) / [plan](../plans/014-structural-violation-detail-plan.md) / [tasks](../tasks/014-structural-violation-detail-tasks.md). Changed-files context: `tools/Hx.Tooling.Contracts/*`, `tools/Hx.Runner.Core/ArchitectureGate/ArchitectureTestRunner.cs`, `tools/Hx.Sentrux.Core/SentruxOutputParser.cs` + `SentruxChecker.*`, `tools/Hx.Gate.Core/GateRunner.cs` + `GateTraceProjector.cs`, `tools/Hx.Cli.Kernel/CliRenderer.cs`, the standalone CLI render, `test/Hx.Architecture.Tests/ArchitectureTests.cs` + the `scaffold/templates/**` arch tests.

## Triage

**Production code + contracts + a generated-code template.** The code lenses apply (this is NOT a Doti-prose change). The `scaffold/templates/**/Architecture.Tests` change is **real generated code** — its lens runs like production. Lenses activated: data-contract, fit-with-current-architecture, edge-case/failure-mode, blast-radius, modularity, testability, simpler-alternative, security. (Sentrux/ArchUnitNET are not run here — they measure implemented code at `/07`.)

## Lens findings

### Fit-with-current-architecture / data-contract — **the load-bearing lens** (no open BLOCKER; one MUST-VERIFY)

- **F1 (BLOCKER-class concern, resolved in design):** the persisted gate proof must be byte-identical (FR-007/SC-006). `gateProofDigest = SHA-256(JsonSerialize(PersistedGateProof))` covers the **whole** persisted proof incl. `GateProof.Steps[].Evidence` (verified at `CycleService.CommitPreparation.cs:21,83`). **Resolution:** offender detail attaches ONLY to `GateRunResult.Trace` (NOT persisted into `PersistedGateProof`) + the standalone result contracts — never to `GateStep.Evidence`. This is the exact 012 M1 boundary. **MUST-VERIFY at implement (T001/T012/T013):** a digest-equality test (null vs populated `StructuralViolations`) + the ArchUnit proof-hash-boundary assertion extended to the three new records. *Not a blocker because the design forecloses it; it is the one invariant drift-review will re-prove.* Evidence: plan "Architecture delta"; `test/Hx.Gate.Tests/ProofHashBoundaryTests.cs` (012 precedent).
- **F2 (MEDIUM, resolved):** additive contracts only — `ArchitectureTestCase.Violations?`, `SentruxCheckResult.RuleViolationDetails?`, `GateTrace.StructuralViolations?` nullable/defaulted; `RuleViolations` (string) unchanged. A pre-014 proof still deserializes (T001). Evidence: plan Design.

### Edge-case / failure-mode (no blocker)

- **F3 (HIGH → mitigated):** ArchUnit capture round-trips the description through the **test failure message → TRX → parser** — a fragile text path. Mitigation: a single deterministic emitted block + `ArchitectureTestRunner` parsing it (T004), and **fail-closed** — a failing test whose message is unparseable yields one `ArchitectureViolation` with `UnknownReason`, never an empty "no violations" (FR-005, Edge Case). Unit-tested over a crafted TRX (T003), so capture is proven without needing a real gate failure. Evidence: spec Edge Cases, FR-005.
- **F4 (MEDIUM → mitigated):** Sentrux summary-style rules (`max_cc` message, whole-graph structural degradation) lack per-function attribution. Mitigation: `SentruxViolation` with null `Function/File` + `UnknownReason` (T006/T007), never zero/fabricated (FR-005). The spec's clarification verified this with evidence (cycle-009 `max_cc` emitted only a summary message). Evidence: spec Clarifications, FR-005.
- **F5 (LOW):** a pre-build run where ArchUnit assemblies / Sentrux output are unavailable → report the capture failure as a diagnostic, not an empty "no violations" (spec Edge Case). Handled by the fail-closed `UnknownReason` path.

### Blast-radius (no blocker)

- **F6 (HIGH → mitigated):** the **generated-code template** arch tests (`scaffold/templates/**`) get the evaluate-and-emit change — they must still **compile and pass green in a generated repo**, or `hx new` output breaks. Mitigation: T005 applies the change to the template with production care; the template test suite (`Hx.Templates.Tests`) + the gate's generated-repo coverage guard it. Evidence: plan "generated-code template".
- **F7 (MEDIUM → mitigated):** `GateRunner` must thread the rich `ArchitectureTestResult`/`SentruxCheckResult` to the projector — touching the gate's hot path. Mitigation: the `GateStep`/`GateProof` it builds is unchanged (summary evidence only); only the Trace gains data (T008/T009). The boundary test (T001) catches any leak into the proof.

### Modularity / testability / thin-CLI (no blocker)

- **F8 (PASS):** capture in `Hx.Runner.Core` (ArchUnit) + `Hx.Sentrux.Core` (Sentrux); projection in `Hx.Gate.Core`; render in the kernel/CLI. Logic stays in `*.Core`; the CLI parses→delegates→renders. Each capture/parse unit is separately testable (T003/T006/T008/T010). New `*.Core` units stay within the Sentrux function-size limit (verified at the `/07` gate).

### Simpler-alternative (no blocker)

- **F9 (PASS):** the two rejected alternatives (offender detail on `GateStep.Evidence`; replacing `RuleViolations` with the structured record) are correctly rejected — the first breaks the proof digest (FR-007), the second is a breaking contract change for no benefit. Additive-on-the-envelope is the simplest design that satisfies the FRs without weakening the gate. Evidence: plan "Alternatives rejected".

### Security (no blocker)

- **F10 (LOW):** violation detail surfaces file paths/type names already in the repo — no new secret/PII exposure; no new input parsing of untrusted data beyond the engines' own already-trusted output. No SCA/SAST surface change.

## Verdict

**No open BLOCKER in any applicable lens.** The single load-bearing invariant (the proof-hash boundary, F1) is foreclosed by the design and is the explicit MUST-VERIFY at implement (T001/T012/T013) + drift-review. The two HIGH items (F3 ArchUnit capture fragility, F6 generated-template) are mitigated by fail-closed capture + template care, both test-covered. Cleared for `/07-doti-implement`.
