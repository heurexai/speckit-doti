# Drift Review — Feature 015: Release accuracy

**Stage:** `/08-doti-drift-review`. **Date:** 2026-06-28. Scoped from the implement change set: `README.md` (the unnumbered-skill documentation) + `.github/workflows/release.yml` (the host-RID tool-fetch step). **Docs + CI-config diff — no `*.cs`, no contract, no rule/Sentrux/ArchUnit/proof surface.**

## Axis 1 — spec ↔ code (PASS)

- **FR-001** (README names all seven utility skills): the new *Unnumbered utility skills* subsection names + explains `doti-constitution`, `doti-auto`, `doti-bug`, `doti-amend`, `doti-drift-fix`, `converge`, `doti-upgrade` (constitution in prose, the rest in a table).
- **FR-002** (constitution fuller treatment): the `/doti-constitution` block covers the §1 inherited/codified vs §2 operator-authored (domain, tech stack, coding style, security, performance) split, the fresh §2 re-read at `/03-doti-plan` + `/06-doti-arch-review` via `hx doti constitution`, and the author/amend path (`/doti-constitution` → `.doti/memory/constitution.md`, tracked by the cycle + git history, no SemVer doc-version; generated repos get their own).
- **FR-003** (overview, not a 2nd source): the README summarizes (name + one-line purpose) and states the skills are single-sourced in `.doti/core/skills.json` — no verbatim duplication of the rendered bodies. `doti render-skills --check` + `doti payload check` confirm no rendered asset changed.
- **FR-004/FR-005** (release CI provisions host-RID tools, hash-verified + fail-closed): `release.yml` `pack-and-smoke` now derives the installed payload root from `hx version --json` and runs `hx tools fetch --repo "$payload" --tool all --json` before `hx new`, using the existing manifest-pinned, hash-verified, fail-closed fetch. The `publish` job + its OIDC/Trusted-Publishing/`production`-environment gating are untouched (the change is confined to the `contents: read` pack-and-smoke job). Matches the approved arch-review (F5 security posture preserved).

Matches the plan: docs + CI-config only; the rejected alternatives (duplicate skill text; windows-only smoke; product-level self-provision) were not taken.

## Axis 2 — code ↔ docs (PASS)

- The change **is** the documentation (README) + a self-commented CI step (release.yml). No code symbol was added/removed/renamed, so there is no stale doc/agent-context/skill claim to update. The agent-context describes the gate/skill substrate generically; nothing there is contradicted. `release.yml` is valid YAML (parsed).

## Axis 3 — source ↔ installed (PASS)

- `doti render-skills --check` — no drift; `doti payload check` — 93 managed files match. The README is a human overview that touches no rendered/installed asset; the skills stay single-sourced.

## Gate

`gate run --profile normal` green over the change set (docs/CI-config; the affected-test planner sees no `*.cs` change). No proof, rule, or limit changed.

## Note — the one CI-observed item

WI-2's effect (the Linux first-smoke resolving the fetched binaries) is only observable on the next pushed tag (v0.12.1) — the store-populate path is not locally reproducible. The fork fetch itself is verified (the linux Sentrux download + hash-verify succeeded locally); a single CI iteration is budgeted in the plan. This is a known confirm-at-CI item, not open drift.

## Verdict

**No open drift** in any applicable axis. The README is an accurate, single-sourced overview; the CI step is additive, hash-verified, fail-closed, and preserves the publish job's security posture. Ready for `/09-doti-release` (v0.12.1).
