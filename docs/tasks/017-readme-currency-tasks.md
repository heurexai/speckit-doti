# Tasks — 017 README currency

**Plan:** [docs/plans/017-readme-currency-plan.md](../plans/017-readme-currency-plan.md). **Stage:** `/04-doti-tasks`. Docs-only.

## Phase 1 — README edits

- [x] T001 In `README.md`, reword the "Proofs, gates, and recovery" intro from "the main branch now includes the 007–011 work" to **007–016** and name the added capabilities — gate & affected-test visibility (`gate run --stream`), the `/doti-auto` hands-off cycle driver, the ArchUnit/Sentrux structural-offender detail, and cross-platform tool provisioning — `README.md` — [covers FR-001, FR-003] <!-- doti-task-hash: b813b5cec044a1147e54beaadfd215cb5d8b2e4fb2c75aada4ba4e3d4f326e74 -->
- [x] T002 In the `README.md` CLI map, extend the `gate run` row with the live `--stream` per-step trace and the `sentrux verify/check` row with the structured offender detail surfaced on a structural failure — `README.md` — [covers FR-002, FR-003] <!-- doti-task-hash: 467556f54a453fab1080816b3b59c172bc89a7ea5263b78dda6c2a03bcd09ec7 -->

## Phase 2 — Verify

- [x] T003 `gate run --profile normal` green over the docs-only change set (`render-skills --check` + `payload check` unaffected — README is not a rendered asset); stamp implement on green — [covers SC-002] <!-- doti-task-hash: 4d3d90712dd207757ec559b7d8f091661978dbc521f8fe3b1e3e2d463e82034c -->

## Coverage

- FR-001 → T001 | FR-002 → T002 | FR-003 → T001, T002 | SC-001 → T001, T002 | SC-002 → T003.
