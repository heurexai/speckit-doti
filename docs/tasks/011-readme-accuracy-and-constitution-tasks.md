# Tasks: README Accuracy + Constitution Strength

Plan: `docs/plans/011-readme-accuracy-and-constitution-plan.md`. Spec: `docs/specs/011-readme-accuracy-and-constitution.md`. **Priority mode = docs/Doti-prose: truth-first** — fix the wrong claim, then surface the undersold strength. Phases sequential; T005 is the final gate.

## Phase 0 — Fix stale/wrong claims — Checkpoint: `gate run` green

- [x] T001 Fix the deleted-path reference: README "Customizing Doti" repo-principles source `.doti/core/memory/constitution.md` → `.doti/memory/constitution.md` — `README.md` — [covers FR-001, SC-001] <!-- doti-task-hash: 1fa089cc0f75c28d2785dd58308d8375410e815455359380358d15b178f03f01 -->
- [x] T002 Add `hx doti constitution` to the CLI map (emit the project constitution / §2 for fresh plan + arch-review context) — `README.md` — [covers FR-002, SC-002] <!-- doti-task-hash: 026cbcbd4b8a9fd2386bf8012ead342a91512f180e9a53091a840b3e3187252e -->

## Phase 1 — Highlight doti's strengths (Spec Kit comparison + narrative) — Checkpoint: `gate run` green

- [x] T003 Rework the Spec Kit comparison so it does NOT imply doti lacks a constitution: amend the "Workflow shape" row and add a dedicated **Constitution** row contrasting Spec Kit's single SemVer-versioned + Sync-Impact-Report constitution with doti's two-layer §1/§2 (cited inherited invariants + operator §2), re-injected fresh at plan + arch-review, no doc-versioning ritual — `README.md` — [covers FR-003, SC-003] <!-- doti-task-hash: 1d412c1a95c6a81f046f0dcb9e29f35e38a273f6d527b00bd1ba121b10aed9fa -->
- [x] T004 Surface the constitution in the workflow/capabilities narrative (the unnumbered `doti-constitution` skill consumed by plan + arch-review); refresh the stale "007/008" main-branch reference to current — `README.md` — [covers FR-004] <!-- doti-task-hash: e5acc716f0e2d4a38377f8549758b7c96cb18ce846aba15a4423fe46e63d4d1a -->

## Verification

- [x] T005 `grep "core/memory/constitution" README.md` shows no live path reference; every CLI-map command resolves in `hx describe`; `gate run --profile normal` green; `render-skills --check` + `payload check` clean — [verifies FR-001, FR-002, SC-001, SC-002, SC-004] <!-- doti-task-hash: 1be1cc6d25d881dff9ad3b4232a62c38d6077c8b54f0c17edb565adc2e696c53 -->
