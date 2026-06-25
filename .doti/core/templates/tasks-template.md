# Tasks Template

> Dependency-ordered, file-pathed, executable tasks numbered `T001…` **in execution order**. Each `FR-###`/`SC-###` maps to ≥1 task (coverage); every task names exact file path(s); a test task precedes the implementation it covers. Organise by **MVP-first phases** (Setup → Foundational → User Story N → Polish) so each user story is an independently-testable increment.

## Format: `[ID] [P?] [US?] Description — path(s) — [covers FR-/SC-]`

- **[P]** — parallelizable: different files, no dependency on another unchecked task in the same phase.
- **[US#]** — the user story this task serves (traceability back to the spec).
- Every task names the exact file path(s) and the `FR`/`SC` it covers.

## Execution order — phases are SEQUENTIAL + gate-enforced (doti)

Phases run **in order, top to bottom. Work only the lowest-numbered incomplete task; do not jump phases or pick by topic** (`/doti-implement` + the ordered-task gate enforce this — out-of-order completion fails closed). A phase is "done" — its **Checkpoint** — only when its tasks are checked **and** `gate run` is green; the next phase starts only then. Within a phase, `[P]` tasks may run in parallel (different files / sub-agents). (doti keeps phases *sequential and gated* — it does not run user stories in parallel the way upstream Spec Kit does.)

## Phase 1: Setup (shared infrastructure)

Project/structure initialization everything else depends on.

- [ ] T001 — <concrete action> — `path/to/file`

## Phase 2: Foundational (blocking prerequisites)

Core that MUST exist before any user story. **No user-story work begins until this Checkpoint is green.**

- [ ] T00X [P] — <action> — `path` — [covers FR-00X]

**Checkpoint:** foundation ready + `gate run` green.

## Phase 3: User Story 1 — [title] (Priority: P1) 🎯 MVP

**Goal:** [what this story delivers]  ·  **Independent Test:** [how to verify it works on its own]

- [ ] T0XX [P] [US1] — Test for [acceptance scenario] (write first; must FAIL before impl) — `test/...` — [covers FR-00X]
- [ ] T0XX [US1] — Implement <thing> — `path` — [covers FR-00X]

**Checkpoint:** User Story 1 independently functional + gate green (MVP ready — stop & validate).

## Phase N: User Story 2 … (P2, P3, …)

[Same shape, in priority order; each story ends at a Checkpoint and must not break earlier stories.]

## Phase: Polish & Cross-cutting

Docs, cleanup, hardening across stories; the final `gate run --profile release`.

- [ ] TXXX — Update README / docs when behavior changes
- [ ] TXXX — Update installed bootstrap files / re-render when the change touches doti assets

## Dependencies & Execution Order

Phase order (above), and within a phase: tests before the impl they cover; contracts/types before consumers; call out `[P]` parallel opportunities and any independently-executable early tasks.

## Implementation Strategy

- **MVP first:** Setup → Foundational → User Story 1 → **stop & validate independently** → demo.
- **Incremental:** add each next-priority story, validating it independently, without breaking earlier ones.
- **Parallel (within a phase):** `[P]` tasks (different files) may run concurrently / be delegated to sub-agents.

## Gate Notes

Manual review is not deterministic gate proof. `gate run` is the per-phase **Checkpoint** proof (`--profile normal` between phases, `--profile release` before release). Mark every unavailable planned command as an advisory gap until the command exists.
