# 028 — Analyze report: spec ↔ plan ↔ tasks consistency + coverage

Cross-check of the revised spec, plan, and tasks (post-arch-review). Scope: FR/SC coverage, internal consistency, ambiguity, and that every resolved arch-review BLOCKER is reflected in all three artifacts.

## FR → task coverage (complete)

| FR | Tasks | OK |
|---|---|---|
| FR-001 §1 invariant | T018 | ✓ |
| FR-002 diff surfacing (CLI seam, null-safe) | T003 (path set), T011 (diff) | ✓ |
| FR-003 verb + evaluates target directly | T006, T008 | ✓ |
| FR-004 in-`Stamp` fence (one pure predicate) | T002, T005 | ✓ |
| FR-005 `ReviewedRebinds` field + one write + cascade suppressed | T001, T006 | ✓ |
| FR-006 propagation, target-only rebind | T004, T006 | ✓ |
| FR-007 `next:` ×9 + binder + DFS de-dup | T010 | ✓ |
| FR-008 anti-rubber-stamp prompts | T019 | ✓ |
| FR-009 matrix locks | T020 | ✓ |
| FR-010 action model (workflow-affordance scope) | T012–T017 | ✓ |

No FR is unmapped; no task lacks an FR/SC anchor.

## SC → verification coverage (complete)

SC-001 decay → T020; SC-002 (pure `IsAttestable` matrix + bare-`Stamp`-throws + `Refresh`-no-bypass) → T002/T005/T020; SC-003 decay-across-rebase → T020; SC-004 diff seam → T011; SC-005 §1 rendered → T018/T021; SC-006 prompts → T019; SC-007 one-write → T006/T009/T020; SC-008 gate → T021; SC-009 affordances generated → T014/T015; SC-010 zero hand-authored *workflow* command info → T015/T017; SC-011 `Describe`==`Evaluate` + static-invariants → T015.

## Arch-review BLOCKER reflection (all 7 present in spec + plan + tasks)

B1 fence-in-`Stamp` → FR-004/D2/**T005**; B2 verb-evaluates-target → FR-003/D1/**T006**; B3 `ReviewedRebinds` field → FR-005/D3/**T001**; B4 `Doti.Core→Cycle.Core` edge + correct assessment → D6/delta/**T017**; B5 workflow-affordance scope → FR-010/D6/**T012-T014**; B6 utility descriptors → FR-010/**T013**; B7 cascade suppressed → FR-005/D5/**T006**. HIGHs: H1 rehome→T017; H2/H3 diff seam→T011; H4 pure predicate→T002; H5 test seam→T009; H6 wrap single projection→T013; H7 golden baseline gating→**T016 (before T017)**.

## Consistency + ambiguity

- **Ordering is dependency-sound:** contracts/fence (P1) → freshness/classifier (P2) → `Stamp`/verb (P3) → graph (P4) → diff (P5) → action model (P6) → render migration **behind** the golden baseline (P7, T016 gates T017) → constitution/prompts (P8) → tests/proof (P9) → docs (P10). No task depends on a later one.
- **No contradiction** between spec, plan, and tasks: the fence location (inside `Stamp`), the record field (`ReviewedRebinds`), the cascade (suppressed), and the model scope (workflow-affordance) are stated identically across all three.
- **No `[NEEDS CLARIFICATION]` remains.** The two deliberately-deferred items are explicit and out-of-scope: the TOCTOU CAS token; folding payload-derived next-actions into the model. `--reason` is optional by decision.
- **Determinism/fail-closed preserved:** nothing downgrades enforced→advisory; the fence is fail-closed inside `Stamp`; `ReviewedNoImpact` is excluded from auto-`Refresh`; the render migration is gated by the byte-stable baseline (the gate's render-check stays authoritative).

## Verdict

**Consistent and complete.** FR/SC coverage full, ordering sound, all arch-review blockers reflected, no open ambiguity. Ready for `/07-implement`.
