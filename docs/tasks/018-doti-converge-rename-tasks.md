# Tasks — 018 Rename `converge` skill to `doti-converge`

**Plan:** [docs/plans/018-doti-converge-rename-plan.md](../plans/018-doti-converge-rename-plan.md). **Stage:** `/04-doti-tasks`. Single-source rename + re-render.

## Phase 1 — Rename the single source

- [x] T001 In `.doti/core/skills.json`, rename the `converge` skill entry's `name` to `doti-converge` (content otherwise unchanged) — `.doti/core/skills.json` — [covers FR-001] <!-- doti-task-hash: fdee15a18194c74e4c33778a9d3bf72c370e8f8cd651e9f506e520f32a5cebbf -->
- [x] T002 `git mv .doti/core/templates/commands/converge.md .doti/core/templates/commands/doti-converge.md` (the skill body the renderer resolves by name; content unchanged) — `.doti/core/templates/commands/` — [covers FR-001] <!-- doti-task-hash: bed358c50b38559c1e14cd17fe1a84ca87f6f1ea3700f5c8d6f5238239437db0 -->

## Phase 2 — Update the skill references (not the command)

- [x] T003 Update the `/converge` **skill** references to `/doti-converge` in `README.md` (the utility-skill table row), `.doti/core/templates/agent-context-template.md` (the unnumbered-utility list), and `.doti/profiles/dotnet-cli/profile.json` (`rootMaturityNote`); LEAVE the `hx doti converge` command + the `converge` capability mentions (Spec Kit comparison, drift-candidates note, checklist/drift-review templates) unchanged — `README.md`, `.doti/core/templates/agent-context-template.md`, `.doti/profiles/dotnet-cli/profile.json` — [covers FR-003] <!-- doti-task-hash: f205afc7feb0d07019fb2d360b96b83d9009484d614e7ab8b82f95252afe88ed -->

## Phase 3 — Re-render + clean up

- [x] T004 Run `doti render-skills` (produces the `doti-converge` skill assets); `git rm -r` the obsolete `.claude/skills/converge` + `.agents/skills/converge` dirs; rebuild Release so the bundled payload matches — [covers FR-002] <!-- doti-task-hash: a52dfc5982e92c34fef27c06b92b59012cfcba923f2e7b6f0a3411d777571cbb -->

## Phase 4 — Verify

- [x] T005 `doti render-skills --check` + `doti payload check` clean; `gate run --profile normal` green over the change set; the `hx doti converge` command still resolves (unchanged); stamp implement on green — [covers FR-002, SC-002, SC-003] <!-- doti-task-hash: a0a8e8098eb35f8265288a9792cfa398d79ff0d755644c7d66aa19ab7cc3f560 -->

## Coverage

- FR-001 → T001, T002 | FR-002 → T004, T005 | FR-003 → T003 | SC-001 → T004 | SC-002 → T005 | SC-003 → T003, T005.
