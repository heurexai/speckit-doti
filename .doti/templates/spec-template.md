# Specification Template

> WHAT and WHY only — defer mechanism (projects, APIs, code) to the plan. Name new and open/unimplemented legacy specs with a Spec Kit-style numbered feature slug (`docs/specs/NNN-short-name.md`, e.g. `docs/specs/001-numbered-specs.md`) so specs sort in workflow order; use that same full slug for the cycle `--feature`. Leave implemented/completed legacy specs on their existing historical filenames. Requirements use stable `FR-###` / `SC-###` IDs so `/doti-analyze` can map them to tasks; user stories (`US1`, `US2`, …) give them MVP-first traceability. Omit an inapplicable section entirely (no "N/A"). Flag genuine unknowns inline with a `[NEEDS CLARIFICATION]` marker (state the specific question after the colon) — at most 7, prioritised scope > security/privacy > UX > technical; if more than 7 genuinely block, record the overflow as a scope risk and recommend splitting the feature.

## Goal

State the user-facing outcome and why it matters.

## User Scenarios & Testing

**Priority Mode** — declare which fits this change (`/04-doti-tasks` orders the build by it): *code / generated-code* → independently testable user-value slices (the user stories below); *docs / Doti-prose* → authoritative-truth correctness → source-of-truth / rendered-asset drift prevention → operator readability → polish; *workflow / tooling* → fail-closed safety + deterministic proof before ergonomics; *mixed* → declare the **dominant** mode + note exceptions. For a non-code mode, replace the user-story scaffold below with prioritised work items in that mode's order.

Prioritised user stories (code mode) — journeys ordered by importance. **Each story MUST be INDEPENDENTLY TESTABLE**: implementing just one yields a viable MVP slice that delivers value. Assign priorities `P1` (most critical), `P2`, `P3`, …; the tasks build order follows these priorities (P1 = MVP).

### User Story 1 — [title] (Priority: P1)

[The journey in plain language.]

- **Why this priority:** [the value and why it ranks here]
- **Independent Test:** [how this story is verified on its own and the specific value it delivers]
- **Acceptance Scenarios:**
  1. **Given** [initial state], **When** [action], **Then** [expected outcome]
  2. **Given** [initial state], **When** [action], **Then** [expected outcome]

### User Story 2 — [title] (Priority: P2)

[Same shape; add P3+ stories as needed, each independently testable.]

### Edge Cases

- What happens when [boundary condition]?
- How does the system handle [error / failure / partial / re-run scenario]?

## Scope

Included behavior, and explicitly excluded behavior.

## Functional Requirements

Testable, unambiguous; map each to the user story it serves so coverage is traceable.

- `FR-001`: System MUST … `[US1]`
- `FR-002`: Users MUST be able to … `[US1]`

## Success Criteria

Measurable and technology-agnostic outcomes:

- `SC-001`: … (a number or observable outcome — e.g. "generates a working repo in under 60s" — not an implementation metric like "API < 200ms")

## Key Entities

(Include only if the feature involves data.) What each represents and its relationships — without implementation detail.

## Deterministic Surfaces

Name the .NET commands, config files, generated files, and JSON proof expected to own the behavior. If a command is planned but unavailable, mark it advisory.

## Architecture Impact

Affected projects, namespaces, layers, architecture rules, and docs.

## Sentrux And Hygiene Impact

Expected Sentrux, baseline, untracked-file, and public-hygiene implications.

## Assumptions

Reasonable defaults chosen where the description was silent (recorded here instead of left as a `[NEEDS CLARIFICATION:]` marker).

## Acceptance

Checks that are command-backed today vs. those that remain advisory in bootstrap mode.

## Clarifications

(Populated by `/doti-clarify` — dated session subheading, one `- Q: … → A: …` decision per line.)
