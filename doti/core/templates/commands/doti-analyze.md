# doti-analyze

Purpose: a read-only cross-artifact consistency and coverage review before architecture review and implementation — do the spec, plan, and tasks agree, is every requirement covered, and does anything violate the constitution?

Read-only: report findings and propose remediation; do not modify files in this stage.

## Behavior

1. Read `.doti/agent-context.md`, the active spec/plan/tasks, and `.doti/memory/constitution.md`.
2. **Requirements inventory + coverage map.** List the spec's `FR-###`/`SC-###` IDs. Map each to the task(s) that implement it. Flag: requirements with **zero tasks** (coverage gap); tasks that map to **no requirement** (orphan / scope creep); and buildable success criteria not reflected in tasks. Report a coverage count (e.g. "11/12 requirements covered").
3. **Detection passes:** duplication (same requirement stated twice, divergently); ambiguity (vague adjectives — "fast / robust / secure" — lacking a measurable criterion; unresolved `[NEEDS CLARIFICATION:]` / TODO markers); underspecification; **constitution alignment**; inconsistency (terminology or entity drift across artifacts; contradictory or mis-ordered tasks).
4. **Severity.** CRITICAL = a constitution violation, a missing core artifact, or a requirement with zero coverage that blocks baseline behavior; then HIGH / MEDIUM / LOW. **A constitution conflict is automatically CRITICAL** — resolve it by adjusting the spec/plan/tasks, never by diluting the principle.
5. **doti drift pass (scaffold-specific).** Compare source assets under `doti/` with the installed bootstrap files; look for drift, overclaiming, and missing command-availability notes. `Hx.Runner.Cli doti render-skills --check` is the authority for skill / agent-context drift.
6. Record which findings are proven by files and which are advisory judgments.

Next-action rule: if any CRITICAL finding exists, recommend resolving it before `/doti-arch-review` and `/doti-implement`.

Expected output: findings ordered by severity, the requirement-coverage map with a coverage count, constitution-alignment issues, and file references where possible.

## Next

Run `/doti-arch-review` to review architecture impacts and rule coverage.
