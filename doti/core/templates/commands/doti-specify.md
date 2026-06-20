# doti-specify

Purpose: create or refine a feature specification for scaffold-dotnet — the WHAT and WHY, captured precisely enough that plan, tasks, and analyze can build on it without re-deriving intent.

## Principles

- **WHAT/WHY, not HOW.** Capture the user-facing outcome, scope, and requirements. Defer mechanism (projects, APIs, code structure) to `/doti-plan`. A reader who only knows the domain should understand it.
- **Every requirement testable.** Phrase as `FR-001`, `FR-002`, … ("System MUST …" / "Users MUST be able to …"). The IDs are stable handles that `/doti-analyze` maps to tasks for coverage.
- **Success is measurable and technology-agnostic.** Phrase as `SC-001`, … with a number or observable outcome (e.g. "generates a working repo in under 60s"), not an implementation metric (no "API < 200ms").

## Behavior

1. Read `.doti/agent-context.md`; map the work to the self-hosting maturity.
2. Produce / update the spec from the spec template. Capture: goal; scope (included **and** explicitly excluded); `FR-###` functional requirements; `SC-###` measurable success criteria; key entities/data (if any); deterministic surfaces (the .NET commands / config / generated files / JSON proof expected to own the behavior — mark planned-but-absent ones advisory); and architecture / Sentrux / hygiene impact.
3. **Mark genuine ambiguities** inline as `[NEEDS CLARIFICATION: <specific question>]` — only where the choice materially changes scope, security/privacy, or UX and no reasonable default exists; otherwise make an informed guess and record it under Assumptions. **Keep at most the 3 most critical markers**, prioritised scope > security/privacy > UX > technical. (`/doti-clarify` resolves them; `doti cycle check` already fails closed on any `[NEEDS CLARIFICATION:]` left at commit — so produce them honestly, never hide an unknown as a silent guess.)
4. **Spec-quality self-check before handoff** (treat the spec like code that gets unit-tested):
   - *Content quality* — no implementation leak; written for a domain reader; mandatory sections present (omit an inapplicable section entirely — no "N/A").
   - *Requirement completeness* — every `FR`/`SC` testable and unambiguous; success criteria measurable and technology-agnostic; scope bounded; assumptions and dependencies stated.
   - *Feature readiness* — each requirement has an acceptance signal; scenarios cover the primary flows.
   Fix what fails and re-check. If a gap cannot be closed without an operator decision, leave one `[NEEDS CLARIFICATION:]` rather than guess.
5. Never claim deterministic gate proof; mark planned-but-absent commands advisory.

Expected output: a spec (or spec delta) with `FR-###`/`SC-###` IDs, at most 3 prioritised `[NEEDS CLARIFICATION:]` markers, a recorded Assumptions section, and command availability noted.

## Next

Run `/doti-clarify` to resolve ambiguities, then `/doti-plan`.
