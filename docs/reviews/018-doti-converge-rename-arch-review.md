# Arch Review — 018 Rename `converge` → `doti-converge`

**Stage:** `/06-doti-arch-review`. **Change under review:** [spec](../specs/018-doti-converge-rename.md) / [plan](../plans/018-doti-converge-rename-plan.md) / [tasks](../tasks/018-doti-converge-rename-tasks.md). Changed files: `.doti/core/skills.json`, `.doti/core/templates/commands/{converge.md → doti-converge.md}`, `README.md`, `.doti/core/templates/agent-context-template.md`, `.doti/profiles/dotnet-cli/profile.json`, + the re-rendered skill assets.

## Triage

**Doti-prose / single-source rename + re-render.** No production `*.Core` code (the `hx doti converge` command is explicitly untouched), no contract, no generated-code template, no rule/Sentrux/ArchUnit/proof surface. The applicable lens is **fit-with-current-architecture** (the single-source + render convention) and **blast-radius** (the skill-vs-command boundary).

## Lens findings

### Fit-with-current-architecture (no blocker)

- **F1 (LOW → mitigated):** the rename follows the established single-source convention — edit `skills.json` + the `<name>.md` template, re-render; never hand-edit a rendered asset. The new name matches the `doti-*` sibling convention. `render-skills --check` confirms the rendered output matches the renamed source (T005).

### Blast-radius (no blocker — the load-bearing boundary)

- **F2 (MEDIUM → mitigated):** the one real risk is over-reaching the rename into the **command** `hx doti converge` (registered in `RunnerCommandFactory.Doti.cs`) or the `converge` capability mentions — that would break the command surface. Mitigation: T003 enumerates exactly the three **skill-reference** files and excludes the command/capability mentions; SC-002 + T005 verify the command still resolves. Evidence: the command lives in `*.Cli` code (untouched by this change set); the Spec Kit comparison + drift-candidates note + checklist/drift-review templates reference the command, not the slash-skill.
- **F3 (LOW → mitigated):** the obsolete rendered `converge/` dirs must be removed or they orphan; T004 `git rm`s them and `payload check` (T005) catches any mismatch.

## Verdict

**No open BLOCKER.** A single-source skill rename + re-render with a clearly-bounded skill-vs-command split (the command and capability references are left intact, verified by SC-002). Cleared for `/07-doti-implement`.
