# Plan — 017 README currency

**Spec:** [docs/specs/017-readme-currency.md](../specs/017-readme-currency.md). **Stage:** `/03-doti-plan`. Docs-only.

## Summary

Edit `README.md` in two spots so it describes the 012–016 work: the "Proofs, gates, and recovery" intro (the stale "007–011 work" line) and the `gate run` / `sentrux` CLI-map rows (no `--stream`/offender mention). No code, no skill/template source, no proof change.

## Existing-architecture assessment

- `README.md` is a human overview; the per-cycle list (line ~268) is already current to 016, and the skill docs (015) are present. The lag is isolated to the proofs/gates intro (line ~165, "007–011 work") and two CLI-map rows (`gate run` line ~213, `sentrux verify/check` line ~215). The README is not a rendered asset — `render-skills --check` / `payload check` are unaffected.

## Design

**Decision:** minimal, surgical edits — (1) reword the proofs/gates intro to "007–016 work" + name the gate-visibility (`gate run --stream`), `/doti-auto`, and structural-offender capabilities; (2) extend the `gate run` row with the live `--stream` trace and the `sentrux` row with the offender detail. Keep it an overview (FR-003) — describe + link, don't duplicate the CHANGELOG or skill bodies.

**Alternatives rejected:** a broader README rewrite — unnecessary; the rest is current (011 + 015 kept it so). Touching the rendered entrypoints — out of scope (016 already refreshed them); this is the human README only.

## Architecture delta

- None. `README.md` is `*.md` (Sentrux-excluded), not a deterministic surface. The gate runs a docs-only lane.

## Constitution Check

- §1/§2: **PASS** — docs only, nothing weakened.

## Risk

- **Minimal.** Prose currency; the gate confirms the docs-only change set is green.

## Next

`/04-doti-tasks`.
