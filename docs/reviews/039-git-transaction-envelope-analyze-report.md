# 039 — Analyze Report (coverage + consistency)

Cross-check of [spec 039](../specs/039-git-transaction-envelope.md) FR/SC ↔ [tasks 039](../tasks/039-git-transaction-envelope-tasks.md), post the arch-review reshaping ([review](039-git-transaction-envelope-arch-review.md)).

## Coverage — every in-scope FR/SC has a task
- FR-001→T001; FR-002→T002; FR-010/011/013→T012; FR-012→T014; FR-014→T013; FR-030→T015; FR-031→T010; FR-032/033→T020/T021.
- SC-001→T003; SC-002/003→T014; SC-004→T013; SC-006→T011; SC-007→T022; SC-002 unit slice→T011.
- **No orphan FR/SC** in the WI1/WI2/WI4 scope. SC-005 (unrelated-dirty-tree fail-closed) maps to WI3 → explicitly deferred to a fast-follow (recorded, not silently dropped).

## Consistency
- The tasks reflect the arch-review-reshaped design (compensation ledger, not whole-tree snapshot; `Hx.Runner.Core/Git/` placement; orphan-only fail-closed; reuse existing `GitWorktree`/`CommitScopeInspector`; WI4 reuses `MarkReleaseTrainReleased`). No task re-introduces a resolved BLOCKER.
- Priority order (safety/proof first) holds: WI1 (transition integrity) before WI2 (release ledger) before WI4 (lifecycle), tests paired with each.

## Ambiguities / gaps
- None blocking. The FR-002 correction (orphan-only) lives in the plan v2 + the working-tree spec edit; a homeless-edit artifact of the doc-dance (the very bug WI1 fixes) — to be committed with the implement diff.
- Deterministic-proof risk retired by the plan-v2 test seams (T014 fault-hook, T011 bare-repo ledger tests, T015 RollbackReport contract).

Verdict: coverage complete for the WI1+WI2+WI4 scope; ready for `/07-implement`.
