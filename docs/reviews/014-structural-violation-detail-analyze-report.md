# Analyze Report — 014 Structural-Engine Violation Detail

**Stage:** `/05-doti-analyze`. Cross-artifact consistency across [spec](../specs/014-structural-violation-detail.md) ↔ [plan](../plans/014-structural-violation-detail-plan.md) ↔ [tasks](../tasks/014-structural-violation-detail-tasks.md).

## Coverage — every FR/SC maps to a task

| Requirement | Tasks | Status |
|---|---|---|
| FR-001 (ArchUnit violating types + rule desc) | T003, T004, T005 | covered |
| FR-002 (`ArchitectureTestCase` additive detail) | T002 | covered |
| FR-003 (`SentruxCheckResult` structured offender) | T002, T006, T007 | covered |
| FR-004 (detail in gate ladder, 012 WI-5) | T008, T009, T010, T011 | covered |
| FR-005 (unknown-with-reason, never fabricated/zero) | T003, T004, T006, T007 | covered |
| FR-006 (deterministic order + "+N more"; full set in JSON) | T004, T010, T011 | covered |
| FR-007 (never weaken; outcomes + proof unchanged) | T001, T005, T008, T012, T013 | covered |
| SC-001/002 (forced violation names family + offender) | T003, T006 (capture) + T010/T011 (render) | covered |
| SC-003 (detail in human ladder = JSON) | T010, T011 | covered |
| SC-004 (unknown-with-reason; clean pass) | T003, T006, T010 | covered |
| SC-005 (deterministic repeat output) | T004, T010 | covered |
| SC-006 (proof unchanged) | T001, T012, T013 | covered |

No FR/SC is orphaned; no task lacks a requirement.

## Consistency checks

- **Spec ↔ plan:** the plan's three additive render-only envelopes (`ArchitectureTestCase.Violations`, `SentruxCheckResult.RuleViolationDetails`, `GateTrace.StructuralViolations`) match the spec's "additively extend the contracts" + "render via 012 WI-5". The capture-then-render split matches "not just unrendered — not captured".
- **Plan ↔ tasks:** contracts (T002) precede capture (T003–T007) precede render (T008–T011) precede the boundary proof (T012–T013) — the dependency order the plan implies. No task introduces design the plan did not call for.
- **The load-bearing invariant is consistently stated** across all three: FR-007/SC-006 (proof byte-unchanged) ↔ plan's "Trace envelope only, never `GateProof.Steps.Evidence`, because `gateProofDigest` covers the whole persisted proof" ↔ T001/T012/T013 (digest-equality + ArchUnit boundary + gate-green proof). This is the 012 M1 analogue and is the highest-risk item.
- **No enforced→advisory downgrade:** the architecture/Sentrux outcomes stay authoritative (FR-007); the tests still FAIL on violation, they merely also emit the description (T005).

## Ambiguities / conflicts

- **None blocking.** The spec's `## Clarifications` verified (with evidence) that both engines already compute the offender data (Sentrux `FormatObjectViolation`; ArchUnit `Evaluate`), and flagged the one FR-005 path (summary-style `max_cc` may lack per-function location → `UnknownReason`, confirmed at implement). No `[NEEDS CLARIFICATION]` remains.

## Verdict

**Consistent and fully covered.** Proceed to `/06-doti-arch-review` (a code change — the code lenses apply, especially the proof-hash-boundary and the generated-template lens).
