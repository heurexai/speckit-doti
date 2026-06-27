# 011 — README Accuracy + Constitution Strength — Plan

## Summary

A docs-only correction of `README.md`: fix the stale `.doti/core/memory/constitution.md` path (deleted in 010), add `hx doti constitution` to the CLI map, and rework the Spec Kit comparison so it is accurate and highlights doti's two-layer constitution. The simplest correct change: edit the four affected README regions; no code, no template, no skill.

## Constitution Check (gate)

Verdict against `.doti/memory/constitution.md` §1/§2 (via `hx doti constitution`): **PASS**. Public Hygiene + Bootstrap Honesty are the live judgments — the README must be accurate and must not overclaim; this cycle removes a stale path and an undersold strength, improving honesty. No §2 convention bent (docs-only; no `*.Core`, no CLI surface change — the CLI map only *documents* an existing command).

## Research (resolve unknowns)

- **Decision:** correct in place; do not restructure the README. **Rationale:** the quickstart, CLI map, and Spec Kit command list are verified accurate against `hx describe`, the `new` flags, and `D:/temp/spec-kit` — only four regions are wrong/undersold. **Alternatives rejected:** a full rewrite (unjustified — most of the doc is accurate and the user asked to validate/correct, not redesign).

## Design

Edit four README regions:
1. **Customizing Doti table** — repo-principles source `.doti/core/memory/constitution.md` → `.doti/memory/constitution.md` (FR-001).
2. **CLI map** — add `hx doti constitution` near `review-context`/`drift-candidates` (FR-002).
3. **Spec Kit comparison** — the "Workflow shape" row stops implying doti has no constitution; add a dedicated **Constitution** row contrasting Spec Kit's single SemVer-versioned + Sync-Impact-Report constitution with doti's two-layer **§1 inherited invariants** (cited, gate-enforced) + **§2 project declarations** (operator-authored), re-injected fresh at plan + arch-review, no doc-versioning ritual (FR-003).
4. **Workflow / capabilities narrative** — name the unnumbered `doti-constitution` skill consumed by plan + arch-review (FR-004); refresh the stale "007/008" main-branch reference to current.

**Architecture delta:** none. No project/namespace/layer/rule/template change. Pure README accuracy.

## Command Availability

| Area | Command | Status |
| --- | --- | --- |
| Gate | `hx gate run --profile normal` | implemented |
| Parity | `doti payload check`, `render-skills --check` | implemented (README is not a managed asset; parity unaffected) |

## Risks

- Low. The README is not a managed payload asset, so `payload check` is unaffected; the risk is a remaining inaccuracy — mitigated by re-validating every CLI-map command against `hx describe` and re-grepping for the dead path (SC-001/002).
