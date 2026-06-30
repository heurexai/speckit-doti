# Analyze Report — 029-scaffold-setup-config

**Stage:** /06-doti-analyze · **Verdict:** CONSISTENT — every FR and SC is covered by ≥1 task; no contradiction across spec ↔ plan ↔ tasks; the 5 clarifications and the arch-review resolutions are reflected end to end. No CRITICAL. `/07-implement` proceeds.

## FR → task coverage

| FR | Tasks | |
|----|-------|--|
| FR-001 provenance config model | T001, T003 | ✓ |
| FR-002 `--config` (both commands) | T004, T008, T009, T012 | ✓ |
| FR-003 persist `.doti/setup.json` | T006 | ✓ |
| FR-004 `config show` + provenance | T010 | ✓ |
| FR-005 `--interactive` wizard | T014, T015, T016 | ✓ |
| FR-006 project-file projections | T005 | ✓ |
| FR-007 checklist, never executed | T017 | ✓ |
| FR-008 existing flags survive | T008, T011 | ✓ |
| FR-009 schema-validated rejection | T002 | ✓ |
| FR-010 host commands + lib placement | T004, T005, T007, T008, T010, T012 | ✓ |

## SC → verification coverage

| SC | Tasks | |
|----|-------|--|
| SC-001 `--config` repo matches | T019 | ✓ |
| SC-002 `config show --json` value/source/default | T019 | ✓ |
| SC-003 human render grouped + count | T006, T019 | ✓ |
| SC-004 interactive == `--config` | T016 | ✓ |
| SC-005 idempotent re-run | T013, T019 | ✓ |
| SC-006 invalid → fail closed, no partial repo | T002, T008, T019 | ✓ |
| SC-007 no-config byte-identical (both surfaces) | T011, T013 | ✓ |
| SC-008 operator-only steps printed, not executed | T017, T019 | ✓ |

## Consistency checks

- **Clarifications reflected:** C1 (defer git/CI to 030) → US4 reduced to the checklist (T017), git/CI automation in Out-of-scope; C2 (lighter wizard) → T015 neutral-named, not the Operator-Question Protocol; C3 (both commands) → T008 + T012 + the `AppliesTo` scoping (T001/T004/T013); C4 (persisted-only) → T010; C5 (gate rules = template substitution) → correctly absent from the task list (not config-driven).
- **Arch-review resolutions reflected:** the module re-homing (Contracts orchestration + injected writers, Doti.Core `.doti`-asset writers) → T004/T005/T006; validate-before-generate (B2/H7) → T002 + T008; security (H6 XML-encode, H3 path-containment) → T002/T005/T008; the no-op fence (M7/D10) → T004/T011/T013; both request carriers (H5/D8) → T007/T012.
- **No contradictions:** the spec's FR/SC, the plan's D1–D10, and the task phases agree on scope (operator subset, both commands, git/CI deferred), placement (Contracts/Doti.Core/Scaffold.Core/CLI), and the fail-closed/no-partial-repo contract.
- **Coverage gaps:** none. Every task traces to an FR or SC; every FR/SC has a task. The permanent documentation sweep (T021) is present as the final task.

## Notes for implement

- Land P1 in the plan's order (Contracts model → Doti.Core writers/store → `--config` on `hx new` validate-before-generate → `config show` → no-op fence + SC-007 locks → install path), so the deterministic core + its regression fences exist before the wizard (P2).
- Honor the two implementation fences the arch-review surfaced: validation runs in the CLI **before** the request record is built; CLI-resident wizard/IO types avoid the `cliSurfaceConfinement` forbidden suffixes and delegate to `*.Core`.
