# Tasks: README Accuracy + Constitution Strength

Plan: `docs/plans/011-readme-accuracy-and-constitution-plan.md`. Spec: `docs/specs/011-readme-accuracy-and-constitution.md`. **Priority mode = docs/Doti-prose: truth-first** — fix the wrong claim, then surface the undersold strength. Phases sequential; T005 is the final gate.

## Phase 0 — Fix stale/wrong claims — Checkpoint: `gate run` green

- [ ] T001 Fix the deleted-path reference: README "Customizing Doti" repo-principles source `.doti/core/memory/constitution.md` → `.doti/memory/constitution.md` — `README.md` — [covers FR-001, SC-001]
- [ ] T002 Add `hx doti constitution` to the CLI map (emit the project constitution / §2 for fresh plan + arch-review context) — `README.md` — [covers FR-002, SC-002]

## Phase 1 — Highlight doti's strengths (Spec Kit comparison + narrative) — Checkpoint: `gate run` green

- [ ] T003 Rework the Spec Kit comparison so it does NOT imply doti lacks a constitution: amend the "Workflow shape" row and add a dedicated **Constitution** row contrasting Spec Kit's single SemVer-versioned + Sync-Impact-Report constitution with doti's two-layer §1/§2 (cited inherited invariants + operator §2), re-injected fresh at plan + arch-review, no doc-versioning ritual — `README.md` — [covers FR-003, SC-003]
- [ ] T004 Surface the constitution in the workflow/capabilities narrative (the unnumbered `doti-constitution` skill consumed by plan + arch-review); refresh the stale "007/008" main-branch reference to current — `README.md` — [covers FR-004]

## Verification

- [ ] T005 `grep "core/memory/constitution" README.md` shows no live path reference; every CLI-map command resolves in `hx describe`; `gate run --profile normal` green; `render-skills --check` + `payload check` clean — [verifies FR-001, FR-002, SC-001, SC-002, SC-004]
