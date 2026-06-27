# Drift Review — Feature 011: README Accuracy + Constitution Strength

**Stage:** `/08-doti-drift-review`. **Date:** 2026-06-28. **Cycle base:** `3d3daaf` (specify). **Scope:** docs-only (`README.md` + `CHANGELOG.md`).

## Axis 1 — spec ↔ code (PASS)
- `gate run --profile normal`: **green**. The change is exactly the four FR regions: the stale path (FR-001/T001), the CLI-map command (FR-002/T002), the Spec Kit comparison (FR-003/T003), and the workflow/capabilities narrative + refreshed cycle reference (FR-004/T004). No code, template, or skill touched.
- **SC-001 verified:** `grep "core/memory/constitution" README.md` returns no live path (line 226 now reads `.doti/memory/constitution.md`).
- **SC-002 verified:** `hx doti constitution` resolves in `hx describe`; it is now in the CLI map.
- **SC-003 verified:** the comparison names doti's constitution, contrasts Spec Kit's single SemVer-versioned + Sync-Impact-Report constitution against doti's two-layer §1/§2 fresh-injected one, and no longer implies doti lacks a constitution. Claims stay factual (a structural contrast, not a subjective "better" — per the analyze/arch-review LOW note).

## Axis 2 — code ↔ docs (PASS)
- This cycle IS the doc fix; CHANGELOG + README carry the `011-readme-accuracy-and-constitution` note (added during implement). No code symbol changed, so no other doc can be stale from this change.

## Axis 3 — source ↔ installed (PASS)
- `doti render-skills --check` + `doti payload check` clean — `README.md`/`CHANGELOG.md` are not managed payload assets, so parity is unaffected (confirmed green in the gate ladder).

## Verdict
**No open drift.** The README is accurate against `hx describe`, the `new` flags, and the real Spec Kit, and the comparison now headlines doti's constitution as a strength. Ready for `/09-doti-release`.
