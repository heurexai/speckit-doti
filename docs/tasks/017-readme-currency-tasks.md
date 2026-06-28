# Tasks — 017 README currency

**Plan:** [docs/plans/017-readme-currency-plan.md](../plans/017-readme-currency-plan.md). **Stage:** `/04-doti-tasks`. Docs-only.

## Phase 1 — README edits

- [ ] T001 In `README.md`, reword the "Proofs, gates, and recovery" intro from "the main branch now includes the 007–011 work" to **007–016** and name the added capabilities — gate & affected-test visibility (`gate run --stream`), the `/doti-auto` hands-off cycle driver, the ArchUnit/Sentrux structural-offender detail, and cross-platform tool provisioning — `README.md` — [covers FR-001, FR-003]
- [ ] T002 In the `README.md` CLI map, extend the `gate run` row with the live `--stream` per-step trace and the `sentrux verify/check` row with the structured offender detail surfaced on a structural failure — `README.md` — [covers FR-002, FR-003]

## Phase 2 — Verify

- [ ] T003 `gate run --profile normal` green over the docs-only change set (`render-skills --check` + `payload check` unaffected — README is not a rendered asset); stamp implement on green — [covers SC-002]

## Coverage

- FR-001 → T001 | FR-002 → T002 | FR-003 → T001, T002 | SC-001 → T001, T002 | SC-002 → T003.
