# 018 — Rename the `converge` skill to `doti-converge`

## Goal

Fix a cosmetic discoverability bug: `converge` is the only unnumbered utility skill **without the `doti-` prefix** (`doti-auto`, `doti-bug`, `doti-amend`, `doti-drift-fix`, `doti-constitution`, `doti-upgrade` all have it). Because the rendered skills sort alphabetically, `converge` lands far from the `doti-*` family and is hard to find. Rename the **skill** to `doti-converge` so it sits with its siblings; update the README + agent context to match.

Important boundary: only the **skill** is renamed. The **CLI command `hx doti converge`** (`RunnerCommandFactory.Doti.cs`, and the `hx doti converge` mentions in templates/docs) is unchanged — it is the command verb the skill invokes.

## User Scenarios & Testing

**Priority Mode** — cosmetic/docs + a single-source rename: truth-first (consistency + discoverability).

### Work Item 1 — `doti-converge` skill, prefixed like its siblings (Priority: P1)

The brownfield-reconciliation skill is `/doti-converge`, rendered alongside the other `/doti-*` utility skills.

- **Why this priority:** an operator scanning the skill list for the doti utilities skips right past `converge`; the inconsistent name is a real findability cost.
- **Independent Test:** after re-render, `.claude/skills/doti-converge/SKILL.md` + `.agents/skills/doti-converge/SKILL.md` exist (and the old `converge/` dirs are gone); the README + agent context refer to `/doti-converge`; `render-skills --check` + `payload check` clean.
- **Acceptance Scenarios:**
  1. **Given** the rendered skills, **When** an operator lists them, **Then** the reconciliation skill appears as `doti-converge`, grouped with the other `doti-*` utilities, and no `converge` skill remains.
  2. **Given** the `hx doti converge` command, **When** it is invoked, **Then** it is unchanged — the rename touches only the skill name, not the command.

### Edge Cases

- The skill's command template (`converge.md`) is renamed to `doti-converge.md` so the renderer resolves it; the old rendered `converge/` dirs must be removed (a rename, not an add).
- The `hx doti converge` command and the `converge` capability mentions (the Spec Kit comparison, the drift-candidates "not wired into converge" note, the checklist/drift-review templates) stay as-is — they reference the command/capability, not the slash-skill.

## Scope

Included: rename the skill in `.doti/core/skills.json` (`converge` → `doti-converge`); rename `.doti/core/templates/commands/converge.md` → `doti-converge.md`; update the `/converge` **skill** references in `README.md`, `.doti/core/templates/agent-context-template.md`, and `.doti/profiles/dotnet-cli/profile.json`; re-render; remove the obsolete `converge/` rendered dirs.

Excluded: no change to the `hx doti converge` command, its registration, or behavior; no change to the converge skill's instruction content beyond its name; no other skill.

## Functional Requirements

- `FR-001`: The skill MUST be named `doti-converge` in `.doti/core/skills.json`, and its command template MUST be `.doti/core/templates/commands/doti-converge.md`. `[WI1]`
- `FR-002`: Re-rendering MUST produce `doti-converge` skill assets (`.claude`/`.agents`) and the obsolete `converge` rendered dirs MUST be removed; `doti render-skills --check` + `doti payload check` stay clean. `[WI1]`
- `FR-003`: The `/converge` **skill** references in `README.md` + the agent-context template + the profile `rootMaturityNote` MUST become `/doti-converge`; the `hx doti converge` **command** and `converge` capability mentions MUST be left unchanged. `[WI1]`

## Success Criteria

- `SC-001`: The reconciliation skill renders as `doti-converge`, grouped with the `doti-*` utilities; no `converge` skill remains.
- `SC-002`: The `hx doti converge` command is unchanged and still works.
- `SC-003`: `render-skills --check` + `payload check` clean; the README + agent context refer to `/doti-converge`.

## Key Entities

- **The `doti-converge` skill** — the renamed brownfield-reconciliation utility skill (was `converge`).
- **The `hx doti converge` command** — the unchanged CLI verb the skill invokes.

## Deterministic Surfaces

- `.doti/core/skills.json` (skill name) + `.doti/core/templates/commands/doti-converge.md` (renamed template) — the single source.
- `README.md`, `.doti/core/templates/agent-context-template.md`, `.doti/profiles/dotnet-cli/profile.json` — the `/converge`→`/doti-converge` skill references.
- `doti render-skills`/`--check`, `doti payload check` — the parity proofs.

## Architecture Impact

- Doti-prose / single-source rename + re-render. No `*.Core` code change (the command is untouched), no contract, no rule/Sentrux/ArchUnit/proof surface.

## Sentrux And Hygiene Impact

- None — prose/skill rename. Sentrux-excluded.

## Assumptions

- The renderer resolves an unnumbered skill's body from `<commandTemplateDir>/<skill-name>.md` (verified by the existing `doti-bug.md`/`doti-amend.md`/… convention); renaming the template file to `doti-converge.md` keeps the skill renderable.
- No test or code references `converge` as a **skill** name (verified — only the command registration references it).

## Acceptance

- Command-backed: `doti render-skills --check`, `doti payload check`, `gate run`.
