# Analyze Report — Feature 012: Gate & Affected-Test Visibility

**Stage:** `/05-doti-analyze`. **Date:** 2026-06-28.
**Artifacts:** [spec](../specs/012-affected-test-plan-visibility.md) · [plan](../plans/012-affected-test-plan-visibility-plan.md) · [tasks](../tasks/012-affected-test-plan-visibility-tasks.md) · [constitution](../../.doti/memory/constitution.md).

## Coverage
21 FR + 13 SC. Mapping: FR-001/002 → T006; FR-003/004/005 → T005/T006; FR-006 → T006/T011; FR-007/008/009 → T008/T009/T012; FR-010 → T011; FR-011 → T001/T013/T015; FR-012 → T008/T011; FR-013 → T003/T008; FR-014/015/016 → T010/T011; FR-017 → T001/T009; FR-018 → T003/T010; FR-019 → T002/T007/T010; FR-020 → T003/T004/T008; FR-021 → T008/T009. SC-001..013 → T003–T015 (SC-007 → T013; SC-008..013 → T010). Every FR/SC covered by ≥1 task.

## Cross-artifact consistency (PASS)
- **Mostly rendering of existing data:** verified — `GateProof.Steps`, `GateScope`, `AffectedPlan` already carry the outcome/scope/selection; the streamed `CliEvent` already carries step outcome + message. T011 surfaces what `CliRenderer.OnEvent`/`WriteSummary` discards; T002/T007 add only the genuinely-missing measures (duration, change/inventory). No double-source: human + JSON render the one `GateTrace` (FR-017 → T009).
- **Fail-closed preserved (the load-bearing invariant):** T013 pins it — the new review-context records (`GateTrace`/`ChangeSummary`) must never enter a `*ProofHasher` (008 FR-020), and the proof hash is byte-identical with/without the trace. The gate's pass/fail and validation are untouched (FR-011). No enforced→advisory downgrade.
- **Two-tier honored:** T008/T009 implement the clarify decision (basic change summary every gate; classes+inventory only at the implement code gate; docs-only → basic only). The implement-stage flag is resolved in the runner (T009), keeping `GateRunner` stage-agnostic — consistent with the plan's R4.
- **Test totals never build-all:** T005/T006 enforce the clarify decision (project-level total cheap; class/case best-effort + unknown; never enumerate all test projects). Consistent with FR-005.
- **Cited code exists:** `CliRenderer.OnEvent`/`WriteSummary`, `GateRunner.Run`, `RunnerCommands.GateRun`, `GateStep`, `GateScope`, `AffectedPlan`, `ChangeSetContext`, `AffectedTestProofHasher` — all present.
- **Ordering:** Phase 0 contracts precede the projectors/render; every `[test]` precedes its impl; T013 (boundary) before the final gate.

## Constitution alignment
PASS — Channel Independence (capture/projection in `*.Core`, render-only CLI in the kernel); Bootstrap Honesty (visibility-only, nothing downgraded; honest unknowns); Deterministic Ownership (trace derived from the proof's own data, never a proof input). No §2 convention bent.

## Findings
**CRITICAL 0 · HIGH 0.** Implement not blocked. (MEDIUM M1: the proof-hash boundary (T013) is the one place a careless add could leak review context into a deterministic proof — the arch-review security/contract lens must verify it. LOW: classes-touched is informational — it does not change selection, only the operator's read of effectiveness.)

## Verdict
Internally consistent, fully covered, fail-closed-preserving, summary-not-dump. Ready for `/06-doti-arch-review`.
