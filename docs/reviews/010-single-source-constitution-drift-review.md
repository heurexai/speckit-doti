# Drift Review — Feature 010: Single-Source the Constitution

**Stage:** `/08-doti-drift-review`. **Date:** 2026-06-28. **Cycle base:** `405b391`. **Implement commit:** `df22441`.

## Axis 1 — spec ↔ code (PASS)
- `gate run --profile normal`: **green**. The only change is the asset removal + a dead csproj-exclusion segment + one test — exactly the FR-001/FR-002 scope. `git ls-files` confirms `.doti/core/memory/` carries no file and `.doti/memory/constitution.md` is intact (SC-001).
- `Constitution_is_single_sourced_with_no_core_memory_twin` (T001) pins the invariant; `hx doti constitution` still emits §2.

## Axis 2 — code ↔ docs (PASS)
- CHANGELOG + README carry the `010-single-source-constitution` note (added during implement, per the 009 lesson). No symbol renamed/removed in code (the change is a Doti asset + a glob segment), so no stale doc reference.

## Axis 3 — source ↔ installed (PASS)
- `doti render-skills --check` + `doti payload check` clean with the twin removed (the parity checker iterates the now-empty source `.doti/core/memory/` → nothing to compare; the active constitution still reconciles to itself).

## Verdict
**No open drift.** A minimal, verified asset-hygiene change: the constitution is single-sourced at `.doti/memory/constitution.md`, matching every doti-installed repo. Ready for `/09-doti-release`.
