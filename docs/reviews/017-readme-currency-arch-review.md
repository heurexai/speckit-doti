# Arch Review — 017 README currency

**Stage:** `/06-doti-arch-review`. **Change under review:** [spec](../specs/017-readme-currency.md) / [plan](../plans/017-readme-currency-plan.md) / [tasks](../tasks/017-readme-currency-tasks.md). Changed file: `README.md`.

## Triage

**Docs-only change** (`README.md`, a human overview — not a rendered/installed asset, not a deterministic surface). All code lenses EXIT *not applicable* (no `*.cs`, no contract, no generated-code template, no rule/Sentrux/ArchUnit/proof surface). The one applicable lens is **docs accuracy**.

## Lens — docs accuracy (no blocker)

- **F1 (LOW → mitigated):** the README must describe only **implemented** behavior, as implemented (the project's own honesty rule). The capabilities added (gate `--stream` visibility, `/doti-auto`, the structural-offender detail, cross-platform provisioning) are all shipped on `main` (012–016, released locally v0.12.0–v0.12.2; in the CHANGELOG). T001/T002 describe them as shipped. No planned-as-shipped claim.
- **F2 (LOW → mitigated):** the README must stay an overview, not a second source of truth (FR-003) — the edits add concise capability mentions + keep the "see CHANGELOG.md" link, never duplicating the skill bodies or the CHANGELOG.

## Verdict

**No open BLOCKER.** A two-spot docs currency pass describing already-shipped capability, kept to overview depth. Cleared for `/07-doti-implement`.
