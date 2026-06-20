# Specification Template

> WHAT and WHY only — defer mechanism (projects, APIs, code) to the plan. Requirements use stable `FR-###` / `SC-###` IDs so `/doti-analyze` can map them to tasks. Omit an inapplicable section entirely (no "N/A"). Flag genuine unknowns inline with a `[NEEDS CLARIFICATION]` marker (state the specific question after the colon) — at most 3, prioritised scope > security/privacy > UX > technical.

## Goal

State the user-facing outcome and why it matters.

## Scope

Included behavior, and explicitly excluded behavior.

## Functional Requirements

- `FR-001`: System MUST … (testable, unambiguous)
- `FR-002`: Users MUST be able to …

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
