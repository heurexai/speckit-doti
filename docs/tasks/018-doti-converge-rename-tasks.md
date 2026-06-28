# Tasks — 018 Rename `converge` skill to `doti-converge`

**Plan:** [docs/plans/018-doti-converge-rename-plan.md](../plans/018-doti-converge-rename-plan.md). **Stage:** `/04-doti-tasks`. Single-source rename + re-render.

## Phase 1 — Rename the single source

- [ ] T001 In `.doti/core/skills.json`, rename the `converge` skill entry's `name` to `doti-converge` (content otherwise unchanged) — `.doti/core/skills.json` — [covers FR-001]
- [ ] T002 `git mv .doti/core/templates/commands/converge.md .doti/core/templates/commands/doti-converge.md` (the skill body the renderer resolves by name; content unchanged) — `.doti/core/templates/commands/` — [covers FR-001]

## Phase 2 — Update the skill references (not the command)

- [ ] T003 Update the `/converge` **skill** references to `/doti-converge` in `README.md` (the utility-skill table row), `.doti/core/templates/agent-context-template.md` (the unnumbered-utility list), and `.doti/profiles/dotnet-cli/profile.json` (`rootMaturityNote`); LEAVE the `hx doti converge` command + the `converge` capability mentions (Spec Kit comparison, drift-candidates note, checklist/drift-review templates) unchanged — `README.md`, `.doti/core/templates/agent-context-template.md`, `.doti/profiles/dotnet-cli/profile.json` — [covers FR-003]

## Phase 3 — Re-render + clean up

- [ ] T004 Run `doti render-skills` (produces the `doti-converge` skill assets); `git rm -r` the obsolete `.claude/skills/converge` + `.agents/skills/converge` dirs; rebuild Release so the bundled payload matches — [covers FR-002]

## Phase 4 — Verify

- [ ] T005 `doti render-skills --check` + `doti payload check` clean; `gate run --profile normal` green over the change set; the `hx doti converge` command still resolves (unchanged); stamp implement on green — [covers FR-002, SC-002, SC-003]

## Coverage

- FR-001 → T001, T002 | FR-002 → T004, T005 | FR-003 → T003 | SC-001 → T004 | SC-002 → T005 | SC-003 → T003, T005.
