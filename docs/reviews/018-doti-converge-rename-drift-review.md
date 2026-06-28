# Drift Review — Feature 018: Rename `converge` → `doti-converge`

**Stage:** `/08-doti-drift-review`. **Date:** 2026-06-28. Change set: the `converge`→`doti-converge` skill rename (skills.json name + the `git mv`'d template + the three `/converge` skill-reference docs + the re-rendered assets, with the obsolete `converge` rendered dirs removed) + the release-doc slug/CHANGELOG. **Doti-prose / single-source rename — no `*.Core` code, no contract, no proof.**

## Axis 1 — spec ↔ code (PASS)

- **FR-001:** `.doti/core/skills.json` skill `name` is now `doti-converge`; the template is `.doti/core/templates/commands/doti-converge.md` (`git mv` from `converge.md`, content unchanged). The renderer resolves it (the new SKILL.md rendered).
- **FR-002:** re-render produced `.claude/skills/doti-converge/SKILL.md` + `.agents/skills/doti-converge/SKILL.md`; the obsolete `converge/` dirs were `git rm`'d; `render-skills --check` + `payload check` (93 files) clean after the Release rebuild. The `doti-converge` skill is registered (grouped with the `doti-*` family).
- **FR-003:** the `/converge` **skill** references became `/doti-converge` in `README.md` (utility table), the agent-context template, and the profile `rootMaturityNote`. The `hx doti converge` **command** (`RunnerCommandFactory.Doti.cs`) and the `converge` capability mentions (Spec Kit comparison, the drift-candidates "not wired into `converge`" note, the checklist/drift-review templates) are untouched — verified: `hx doti converge` still resolves.

Matches the plan: skill-only rename, command/capability boundary preserved (the rejected alternative — renaming the command — was not taken).

## Axis 2 — code ↔ docs (PASS)

- The change **is** the skill + its docs; the README + agent-context now name `/doti-converge`, consistent with the rendered skill. No symbol-removal stale-name remains for the skill (the old `converge` skill dirs are deleted); the `converge` *command* references are intentionally retained because the command is unchanged.

## Axis 3 — source ↔ installed (PASS)

- `doti render-skills --check` — no drift; `doti payload check` — 93 managed files match (the rename is net-zero on count: one skill renamed, old dirs removed, new dirs added). No hand-edited rendered asset; the rename was made in `skills.json` + the template source and rendered.

## Gate

`gate run --profile normal` green over the change set. No code, rule, limit, manifest, or proof change.

## Verdict

**No open drift.** A clean single-source skill rename with the command/capability boundary preserved; parity clean, the command still works. Ready for `/09-doti-release` (v0.12.4).
