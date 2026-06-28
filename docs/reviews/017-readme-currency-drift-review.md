# Drift Review — Feature 017: README currency

**Stage:** `/08-doti-drift-review`. **Date:** 2026-06-28. Change set: `README.md` (currency edits + the release-doc slug/CHANGELOG entry). **Docs-only diff.**

## Axis 1 — spec ↔ code (PASS)

- **FR-001:** the "Proofs, gates, and recovery" intro now reads "007–016 work" and names the gate & affected-test visibility (`gate run --stream`), the `/doti-auto` driver, the ArchUnit/Sentrux structural-offender detail (Sentrux production-scoped), and cross-platform tool provisioning.
- **FR-002:** the CLI-map `gate run` row mentions the live `--stream` per-step trace; the `sentrux verify/check` row mentions the offending-file/function detail (production-only).
- **FR-003:** the edits stay overview-depth — concise capability mentions + the existing "see CHANGELOG.md" link; no skill body or CHANGELOG content duplicated; every named capability is shipped (012–016).

Matches the plan: two surgical edits, no broader rewrite, no rendered-entrypoint touch.

## Axis 2 — code ↔ docs (PASS)

- The change **is** documentation; no code symbol exists to drift from. The README now agrees with the CHANGELOG + the per-cycle list (both already current).

## Axis 3 — source ↔ installed (PASS)

- `README.md` is a human overview, not a rendered/installed asset — `doti render-skills --check` + `doti payload check` are unaffected (the gate confirms). No `.doti/core` or profile source touched.

## Gate

`gate run --profile normal` green over the docs-only change set. No code, rule, limit, or proof changed.

## Verdict

**No open drift.** A docs-only currency pass describing already-shipped capability. Ready for `/09-doti-release` (v0.12.3).
