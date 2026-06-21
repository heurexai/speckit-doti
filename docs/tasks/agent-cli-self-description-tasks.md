# Tasks: Agent-first CLI self-description in the doti workflow

> Plan: `docs/plans/agent-cli-self-description-plan.md`. Source edits under `doti/core` + a re-render. Each task notes the requirement(s) it satisfies.

- `T001` (FR-001, FR-002) — In `doti/core/templates/agent-context-template.md`, add a Workflow-Rules directive: (a) run `describe --json` to learn a scaffold CLI's command/option/exit-class/error-code surface before driving it; (b) on a non-Success `CliResult`, act on the diagnostics `code` / `hint` / `nextActions` rather than guessing or blind-retrying.
- `T002` (FR-003) — In `doti/core/templates/plan-template.md`, add a `## CLI surface & error contract` section (emitted codes → `errorcodes/registry.json`, exit class, `describe` entry, `CliResult` envelope; omit when no CLI surface).
- `T003` (FR-004) — In `doti/core/templates/commands/doti-plan.md`, add a Behavior step that produces the error-contract section.
- `T004` (FR-005) — In `doti/core/templates/commands/doti-implement.md`, add an execution step: register declared codes in `errorcodes/registry.json`, run `errorcodes check`, confirm `describe` reflects the new surface.
- `T005` (FR-006, SC-002, SC-003) — Re-render with `doti render-skills --repo . --agents codex,claude`, then `--check` to confirm **no drift**. (Editing only `doti/core` is what makes the change propagate to consumers on their next install — SC-003.)
- `T006` (SC-004) — Confirm build + test stay green (no regression); enforced by the PR's CI.

## Coverage

| Requirement | Task(s) |
| --- | --- |
| FR-001, FR-002 | T001 |
| FR-003 | T002 |
| FR-004 | T003 |
| FR-005 | T004 |
| FR-006 | T005 |
| SC-001 (plan section present for a CLI feature) | T002 (section exists) |
| SC-002 (no drift) | T005 |
| SC-003 (propagation) | T001–T004 (source-only edits) |
| SC-004 (gates green) | T006 |

No `[NEEDS CLARIFICATION]` remains; no task is blocked.
