# 011 — README Accuracy + Constitution Strength

## Goal

Bring `README.md` up to date and make the Spec Kit comparison accurate while highlighting doti's strengths. The 009 constitution and the 010 single-sourcing left three gaps: a stale path to the deleted `.doti/core/memory/constitution.md`, a missing `hx doti constitution` in the CLI map, and a Spec Kit comparison that omits doti's constitution (implying doti lacks one when 009 added a more opinionated two-layer §1/§2 constitution). Docs-only; no code or behavior change.

## User Scenarios & Testing

A reader (coder or non-coder) skims the README and gets an accurate, current picture: the quickstart works, every CLI command listed exists, the customization paths point at real files, and the Spec Kit comparison fairly shows where doti is stronger — including its constitution.

## Scope

In: `README.md` only — fix the stale constitution path; add `hx doti constitution` to the CLI map; update the Spec Kit comparison to include doti's constitution and highlight the §1/§2 strength vs Spec Kit's SemVer-versioned flat constitution; surface the constitution in the workflow/capabilities narrative; refresh the "007/008" cycle reference to current. Out: no change to the quickstart commands or other already-accurate sections (verified accurate); no code/template/skill change.

## Functional Requirements

- `FR-001`: The README MUST NOT reference the deleted `.doti/core/memory/constitution.md`; the "Customizing Doti" repo-principles source MUST be `.doti/memory/constitution.md` (the single source after 010).
- `FR-002`: The CLI map MUST include `hx doti constitution` (emit the project constitution / §2 for fresh plan + arch-review context), alongside the other `hx doti` surfaces.
- `FR-003`: The Spec Kit comparison MUST be accurate AND highlight doti's constitution as a strength: doti has a **two-layer §1/§2** constitution (inherited invariants cited + project declarations the only fillable content), re-injected fresh at plan + arch-review, with **no SemVer doc-versioning / Sync Impact Report** — versus Spec Kit's single SemVer-versioned constitution. The comparison MUST NOT imply doti lacks a constitution.
- `FR-004`: The workflow/capabilities narrative MUST mention the constitution (the unnumbered `doti-constitution` skill consumed by plan + arch-review) so a reader sees it as a first-class doti capability.

## Success Criteria

- `SC-001`: `grep "core/memory/constitution" README.md` returns no match (except, if present, an explicit "removed the `.doti/core/memory` twin" historical note); the live path `.doti/memory/constitution.md` is used.
- `SC-002`: `hx doti constitution` appears in the README CLI map; every command in the CLI map still resolves in `hx describe`.
- `SC-003`: The Spec Kit comparison names doti's constitution and its §1/§2 / no-doc-versioning distinction; a reader can tell doti has a constitution and how it differs.
- `SC-004`: `gate run --profile normal` green; `render-skills --check` + `payload check` clean (docs-only change touches no managed asset).

## Key Entities

- **README.md** — the public landing doc; the only artifact this cycle edits.

## Architecture Impact

- None. Docs-only; no `*.Core`, no contract, no rule, no template/skill change.

## Assumptions

- Verified against ground truth this session: the `hx new` flags, the CLI command tree (`hx describe`), and Spec Kit's command set (`D:/temp/spec-kit`) — the quickstart + CLI map + Spec Kit command list are otherwise accurate.

## Acceptance

- Command-backed: `gate run`, `doti payload check`, `doti render-skills --check` — all gate this docs change (the README is not a managed payload asset, so parity is unaffected; the gate's hygiene/build/test still run).
