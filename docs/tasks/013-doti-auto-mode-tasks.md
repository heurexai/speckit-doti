# Tasks — 013 Doti Auto Mode

**Plan:** [docs/plans/013-doti-auto-mode-plan.md](../plans/013-doti-auto-mode-plan.md). **Stage:** `/04-doti-tasks`.

Doti-prose-only change: a new unnumbered `doti-auto` skill, single-sourced in `.doti/core/skills.json` + a command template, rendered. No code, no contract. Tasks are sequential (each input feeds the next).

## Phase 0 — Author the single source

- [x] T001 Add the `doti-auto` skill entry to `.doti/core/skills.json` (`skills[]`): name `doti-auto`, a description framing it as an unnumbered utility that auto-advances `/01`–`/09` to a target and stops at operator-decision points, an argumentHint of `[--until <stage>]`, a highlights block enumerating the loop + the stop conditions + the never-weaken/never-publish rules + the 95% auto-fix bar + the release-train `--until drift-review` usage, and a nextStage; mirror the shape of the existing `doti-constitution`/`doti-amend` entries — `.doti/core/skills.json` — [covers FR-001, FR-007] <!-- doti-task-hash: a1c2ef2687f0a8ae2957f39360fae1f20da27b7e290feafd5144d86a99a84010 -->
- [x] T002 Author the command template encoding the orchestration behavior: resolve current stage + target (`--until`, default `release`); loop stage→stage invoking each `/0N` skill, gating + stamping; the enumerated stop conditions (clarify ambiguity/`[NEEDS CLARIFICATION]`, arch-review BLOCKER or missing applicable lens, unrecoverable gate failure, publish boundary, genuine blocker); the 95%-confidence auto-fix boundary (spec↔code gap fixed in CODE, never the spec); never weaken a gate / never publish; honest boundary reporting; release-train `--until drift-review` usage — `.doti/core/templates/commands/doti-auto.md` — [covers FR-002, FR-003, FR-004, FR-005, FR-006, FR-007, FR-008] <!-- doti-task-hash: 385d5d33f0ceb3194b8f0d9bc7f139d4d3f3f98e24778f3e6252eec9d1a8482e -->

## Phase 1 — Render

- [x] T003 Run `doti render-skills` to regenerate the installed skill (`.claude`/`.agents`) + agent-context from the updated source; do NOT hand-edit the rendered output — [covers FR-001] <!-- doti-task-hash: 6ffa36065b398ad1da141c3bc47fa8dd687ef5d267c2ad9db29a9da60af8b44d -->

## Phase 2 — Verify parity (the deterministic proof)

- [x] T004 `doti render-skills --check` clean (rendered output matches source — no drift) — [covers SC-006] <!-- doti-task-hash: 97d1fe01b5f684b64ab94b5832cd36f280e5f53d4a6540a2074d1606dbaa49f8 -->
- [x] T005 `doti payload check --repo .` clean (installed `.doti` payload parity, static + rendered) — [covers SC-006] <!-- doti-task-hash: 45c846c29b1073bc07f5494da7d6460ddd008ab7f33a02276ddfef4272d60f5c -->
- [x] T006 `gate run --profile normal` green over the change set (the skill-drift + payload-parity steps are the enforcement point for this prose change); stamp implement on green — [covers SC-006] <!-- doti-task-hash: e9e2ba2a1dbfcbe31bc0a15f8a720cddd6f12603bddfa17c51f874be11eee9f6 -->

## Coverage

- FR-001 → T001, T003, T004 | FR-002/003/004/005/006/008 → T002 | FR-007 → T001, T002 | SC-006 → T004, T005, T006.
