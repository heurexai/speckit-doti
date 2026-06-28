# Tasks — 013 Doti Auto Mode

**Plan:** [docs/plans/013-doti-auto-mode-plan.md](../plans/013-doti-auto-mode-plan.md). **Stage:** `/04-doti-tasks`.

Doti-prose-only change: a new unnumbered `doti-auto` skill, single-sourced in `.doti/core/skills.json` + a command template, rendered. No code, no contract. Tasks are sequential (each input feeds the next).

## Phase 0 — Author the single source

- [ ] **T001** — Add the `doti-auto` skill entry to `.doti/core/skills.json` (`skills[]`): `name: doti-auto`, a `description` framing it as an unnumbered utility that auto-advances `/01`–`/09` to a target and stops at operator-decision points, an `argumentHint` of `[--until <stage>]`, a `highlights[]` block enumerating the loop + the stop conditions + the never-weaken/never-publish rules + the 95% auto-fix bar + the release-train `--until drift-review` usage, and a `nextStage`. Mirror the shape of the existing `doti-constitution`/`doti-amend` entries. `[FR-001, FR-007]`
- [ ] **T002** — Author `.doti/core/templates/commands/doti-auto.md` — the command template encoding the orchestration behavior: resolve current stage + target (`--until`, default `release`); loop stage→stage invoking each `/0N` skill, gating + stamping; the enumerated stop conditions (clarify ambiguity/`[NEEDS CLARIFICATION]`, arch-review BLOCKER or missing applicable lens, unrecoverable gate failure, publish boundary, genuine blocker); the 95%-confidence auto-fix boundary (spec↔code gap fixed in CODE, never the spec); never weaken a gate / never publish; honest boundary reporting; release-train usage (`--until drift-review` per member). `[FR-002, FR-003, FR-004, FR-005, FR-006, FR-007, FR-008]`

## Phase 1 — Render

- [ ] **T003** — Run `doti render-skills` to regenerate the installed skill (`.claude`/`.agents`) + agent-context from the updated source. Do NOT hand-edit the rendered output. `[FR-001]`

## Phase 2 — Verify parity (the deterministic proof)

- [ ] **T004** — `doti render-skills --check` clean (rendered output matches source — no drift). `[SC-006]`
- [ ] **T005** — `doti payload check --repo .` clean (installed `.doti` payload parity, static + rendered). `[SC-006]`
- [ ] **T006** — `gate run --profile normal` green over the change set (the skill-drift + payload-parity steps are the enforcement point for this prose change). Stamp implement on green. `[SC-006]`

## Coverage

- FR-001 → T001, T003, T004 | FR-002/003/004/005/006/008 → T002 | FR-007 → T001, T002 | SC-006 → T004, T005, T006.
