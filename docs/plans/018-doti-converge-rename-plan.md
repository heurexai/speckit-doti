# Plan — 018 Rename `converge` skill to `doti-converge`

**Spec:** [docs/specs/018-doti-converge-rename.md](../specs/018-doti-converge-rename.md). **Stage:** `/03-doti-plan`. Single-source rename + re-render; no `*.Core` code.

## Existing-architecture assessment (verified)

- Skills are single-sourced in `.doti/core/skills.json` (the `converge` entry) + a per-skill command template `.doti/core/templates/commands/<name>.md` (`converge.md`), rendered by `DotiRenderer` to `{.claude,.agents}/skills/<name>/SKILL.md`. The unnumbered-skill convention is `<name>.md` (`doti-bug.md`, `doti-amend.md`, …).
- The `/converge` **skill** is referenced in `README.md` (line ~158 utility table), `.doti/core/templates/agent-context-template.md` (line ~84 utility list), and `.doti/profiles/dotnet-cli/profile.json` `rootMaturityNote`.
- The `hx doti converge` **command** is registered in `tools/Hx.Runner.Cli/RunnerCommandFactory.Doti.cs` (`new("converge", …)`, `"doti converge"`) and referenced as a command/capability in the README Spec Kit comparison, the drift-candidates "not wired into converge" note, and the checklist/drift-review templates. **All command/capability references stay** — only the slash-skill is renamed.

## Design

**Decision:** rename the skill in `skills.json` (`converge` → `doti-converge`), rename the template file `converge.md` → `doti-converge.md`, update the three `/converge` skill references to `/doti-converge`, re-render, and `git rm` the obsolete `converge/` rendered dirs. Then rebuild Release so the bundled payload matches.

**Rationale:** the rename is purely the single source (name + template file) + re-render; matching the `doti-*` convention restores discoverability with no behavior change. Leaving the command + capability mentions untouched keeps `hx doti converge` working and avoids mislabeling the command as a skill.

**Alternatives rejected:** rename the command too — REJECTED, it would break `hx doti converge` and the command verb is correct as-is; the bug is only the skill's name.

## Architecture delta

- No ArchUnit/Sentrux/contract/proof change. Deterministic surfaces touched: `skills.json` + the renamed template (render inputs) + the three doc references. Parity (`render-skills --check`, `payload check`) is the proof; the obsolete-dir removal keeps the rendered set clean.

## Constitution Check

- §1/§2: **PASS** — prose/skill rename, nothing weakened; the command surface is unchanged.

## Risk

- **Low.** The one watch-item is the obsolete `converge/` rendered dirs — must be removed (a rename, not an add) or they linger as orphans; the payload check catches a mismatch. No code/test references `converge` as a skill name (verified).

## Next

`/04-doti-tasks`.
