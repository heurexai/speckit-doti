# 022 — Doti repo version lifecycle — Analyze (cross-artifact consistency)

Cross-checks spec ↔ plan ↔ tasks for coverage, consistency, and contradictions before arch-review.

## Coverage — every FR/SC maps to ≥1 task

| Req | Tasks | | Req | Tasks |
| --- | --- | --- | --- | --- |
| FR-001 | T011, T012, T021, T022 | | FR-012 | T042, T043 |
| FR-002 | T013, T014, T021 | | FR-013 | T040, T041, T044 |
| FR-003 | T023, T024 | | FR-014 | T002, T040, T041, T045 |
| FR-004 | T002, T020, T021 | | FR-015 | T043, T044, T045 |
| FR-005 | T030, T031 | | FR-016 | T050, T051, T052 |
| FR-006 | T030, T032 | | FR-017 | T050, T051 |
| FR-007 | T030, T031 | | FR-018 | T050, T051 |
| FR-008 | T042, T043, T045 | | FR-019 | T022, T032, T045, T052, T064 |
| FR-009 | T042, T043 | | FR-020 | T012, T014, T024 |
| FR-010 | T042, T043 | | | |
| FR-011 | T042, T043 | | | |

| SC | Covered by | | SC | Covered by |
| --- | --- | --- | --- | --- |
| SC-001 | T021, T022 | | SC-005 | T042, T043 |
| SC-002 | T043, T045, T060 | | SC-006 | T040–T044, T013/T014 |
| SC-003 | T023, T024 | | SC-007 | T050, T051, T052 |
| SC-004 | T030, T031, T032 | | SC-008 | T022/T032/T045/T052 + T070 |

**No uncovered FR/SC; no orphan task** (every task traces to a requirement or the docs/gate deliverable). The docs deliverable (FR-019 + SC-002) is carried explicitly by T060–T062.

## Consistency

- **Relation vocabulary** is consistent: the new commands use `current/outdated/ahead/unknown` (the `DotiVersionRelation` enum, T010); `hx version --repo` keeps its legacy `unknown/behind/equal/newer` contract (T024) — both mapped from the **single** `DotiVersionRelationCalculator` (FR-020). The spec/plan/tasks agree on this split (spec Research §1, plan Design, T014/T024).
- **"Managed (tool-owned)" vs "operator-owned"** is used consistently across spec (Scope/Assumptions), plan (Design), and tasks (T043 reuses `DotiInstaller`'s existing classification + `.new` sidecar behaviour) — no second customization scheme (FR-010/020 honoured).
- **Worktree + git-required** consistent: spec FR-013/014, plan Research §3, tasks T040/T041/T044 — `GitWorktree` in `Hx.Runner.Core`, `--dry-run` preview, apply-back, fail-hard-no-git.
- **MVP-first ordering** matches the spec's declared priority mode: read-only US1/US2 before mutating US3/US4; tests precede impl in every phase; contracts/types (Phase 2) precede consumers.
- No requirement contradicts another or the scope; no task implies behaviour the spec excludes (the excluded items — tool self-update, MSIX/release changes, remote scanning — appear nowhere in the tasks).

## Gaps / Risks (carried forward, not blocking)

- The two plan **Risks** (worktree apply-back captures tracked assets only; the new within-core edge must stay acyclic) are design concerns for `/06-arch-review` to confirm, not coverage gaps.
- All four commands are **planned/advisory** until their phase lands (T070 release gate is the proof); no manual check is presented as gate proof.

## Verdict

**PASS** — coverage complete, artifacts consistent, no contradictions. Ready for `/06-arch-review`.
