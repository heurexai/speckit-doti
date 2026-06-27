# Architecture Review — Feature 012: Gate & Affected-Test Visibility

**Stage:** `/06-doti-arch-review`. **Date:** 2026-06-28.
**Artifacts:** [spec](../specs/012-affected-test-plan-visibility.md) · [plan](../plans/012-affected-test-plan-visibility-plan.md) · [tasks](../tasks/012-affected-test-plan-visibility-tasks.md).

## Triage
Runtime code + contracts: `Hx.Tooling.Contracts` (additive records), `Hx.Impact.Core` (change summary + test inventory), `Hx.Gate.Core` (timing + trace projector), `Hx.Cli.Kernel` (render), `Hx.Runner.Cli` (wiring + `--stream`). Code lenses ON: data-contract, security/boundary, blast-radius, modularity, testability, fit-with-architecture, design-soundness, simpler-alternative. Edge-case ON (unknown counts). No generated-template change, no persistence/DB.

## Findings

### BLOCKER 0 · HIGH 0
The design fits the established patterns (capture/projection in `*.Core`, render-only kernel, additive contracts) and preserves the fail-closed proof. Cleared for implement with the MEDIUMs honoured.

### MEDIUM
- **M1 — proof-hash boundary (data-contract + security; the load-bearing one).** The new `GateTrace`/`ChangeSummary`/`AffectedTestInventory` are **review context** and MUST never enter `AffectedTestProofHasher` or any `*ProofHasher` (008 FR-020/SC-009 — review context is not a proof input; a leak would let advisory change-context influence a deterministic proof). **Fix:** T013's ArchUnit assertion (`*ProofHasher ↛ GateTrace/ChangeSummary/AffectedTestInventory`) + the byte-identical-proof-hash test are mandatory, and the new contracts must be added to `GateProof`/`GateRunResult` (the envelope), NOT to the hashed `AffectedTestProof.Plan`. An existing test pins `*ProofHasher ↛ ChangeSetContext`; extend it to the new records.
- **M2 — schema additivity (data-contract).** `GateRunResult`/`GateProof`/`GateStep` are serialized contracts a persisted gate-proof and `--json` consumers read; every addition MUST be nullable/defaulted so a pre-012 proof still deserializes and `SchemaVersion` need not bump (T001). A required field would break reading older proofs at release-train validation.
- **M3 — test-inventory cost (edge-case + design-soundness).** Class/case counts MUST come only from already-built (selected) test assemblies; the repo-wide total is `unknown` unless cheaply available — never trigger a build of unaffected test projects (the clarify decision). A reflection-load of a test assembly must be failure-isolated (a bad load → `unknown` with reason, not a gate crash).
- **M4 — kernel/gate coupling (fit-with-architecture).** The kernel rendering `GateRunResult` is a small Contracts-level coupling (the kernel already imports Contracts + special-cases the live-progress path). Acceptable for one first-class command; if 014 (architecture/sentrux detail) needs the same shape, extract a generic render-model then — do NOT pre-abstract now (simpler-alternative lens agrees).

### LOW
- **L1 — classes-touched scanner.** Reuse the 009 `CSharpMemberChunker` lexer approach (skip strings/comments before scanning declarations); a naive regex would mis-handle `class` in a string/comment. Keep it in `Hx.Impact.Core` (zero Roslyn).
- **L2 — determinism.** Sort classes-touched + changed files Ordinal; cap with "+N more" (FR-013/018) so repeated runs are byte-stable.

## Rule/Boundary deltas
- ArchUnit: extend the proof-hasher boundary test to the new records (T013). `cliSurfaceConfinement`/`cliDelegation` already confine `*Projector` (008 BL-5) — no family addition. `Gate/Cycle ↛ Semantic` unaffected. No `.sentrux/rules.toml` change.

## Verdict
**0 BLOCKER / 0 HIGH.** A modular, additive, fail-closed-preserving visibility change. The proof-hash boundary (M1) is the must-verify; T013 enforces it. Cleared for `/07-implement`.
