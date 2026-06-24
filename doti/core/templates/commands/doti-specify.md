# doti-specify

Purpose: create or refine a feature specification for scaffold-dotnet — the WHAT and WHY, captured precisely enough that plan, tasks, and analyze can build on it without re-deriving intent.

## Principles

- **WHAT/WHY, not HOW.** Capture the user-facing outcome, scope, and requirements. Defer mechanism (projects, APIs, code structure) to `/doti-plan`. A reader who only knows the domain should understand it.
- **Specs are numbered.** For new work, choose the next three-digit prefix from existing `docs/specs/NNN-*` files, name the spec `docs/specs/NNN-short-name.md`, and use the same full `NNN-short-name` slug for `doti cycle stamp --feature`. This keeps doti aligned with the original Spec Kit ordering model while still using doti's flat docs layout.
- **Upgrade-safe legacy handling.** In repos updated from v0.5 or another pre-numbering install, leave implemented/completed historical specs on their existing filenames. If an open, unimplemented spec still uses an unnumbered slug, migrate it before continuing: choose the next `NNN-` prefix, rename the matching spec/plan/tasks artifacts to the same numbered slug, then re-stamp `specify` with the new `NNN-short-name` feature. All subsequent new specs use numbered slugs.
- **Every requirement testable.** Phrase as `FR-001`, `FR-002`, … ("System MUST …" / "Users MUST be able to …"). The IDs are stable handles that `/doti-analyze` maps to tasks for coverage.
- **Success is measurable and technology-agnostic.** Phrase as `SC-001`, … with a number or observable outcome (e.g. "generates a working repo in under 60s"), not an implementation metric (no "API < 200ms").

## Behavior

1. Read `.doti/agent-context.md`; map the work to the self-hosting maturity.
2. Select the numbered feature slug. For new specs, inspect `docs/specs`, pick the next unused `NNN-` prefix, and use a lowercase kebab-case name (for example `001-numbered-specs`). For an existing implemented/completed legacy spec, keep the current slug. For an open, unimplemented legacy spec, migrate it to the new numbered slug before clarifying, planning, or implementing.
3. Produce / update the spec from the spec template. Capture: goal; scope (included **and** explicitly excluded); `FR-###` functional requirements; `SC-###` measurable success criteria; key entities/data (if any); deterministic surfaces (the .NET commands / config / generated files / JSON proof expected to own the behavior — mark planned-but-absent ones advisory); and architecture / Sentrux / hygiene impact.
4. **Mark genuine ambiguities** inline as `[NEEDS CLARIFICATION: <specific question>]` — only where the choice materially changes scope, security/privacy, or UX and no reasonable default exists; otherwise make an informed guess and record it under Assumptions. **Keep at most the 3 most critical markers**, prioritised scope > security/privacy > UX > technical. (`/doti-clarify` resolves them; `doti cycle check` already fails closed on any `[NEEDS CLARIFICATION:]` left at commit — so produce them honestly, never hide an unknown as a silent guess.)
5. **Spec-quality self-check before handoff** (treat the spec like code that gets unit-tested):
   - *Content quality* — no implementation leak; written for a domain reader; mandatory sections present (omit an inapplicable section entirely — no "N/A").
   - *Requirement completeness* — every `FR`/`SC` testable and unambiguous; success criteria measurable and technology-agnostic; scope bounded; assumptions and dependencies stated.
   - *Feature readiness* — each requirement has an acceptance signal; scenarios cover the primary flows.
   Fix what fails and re-check. If a gap cannot be closed without an operator decision, leave one `[NEEDS CLARIFICATION:]` rather than guess.
6. Never claim deterministic gate proof; mark planned-but-absent commands advisory.

Expected output: a numbered spec path (`docs/specs/NNN-short-name.md`), `FR-###`/`SC-###` IDs, at most 3 prioritised `[NEEDS CLARIFICATION:]` markers, a recorded Assumptions section, and command availability noted.

## Next

Run `/doti-clarify` to resolve ambiguities, then `/doti-plan`.
