# doti-tasks

Purpose: turn an approved plan into concrete, ordered, executable tasks.

## Behavior

1. Read `.doti/agent-context.md`, the plan, and the active spec.
2. Derive tasks from the plan's design and the spec's `FR-###`/`SC-###` — each requirement should map to at least one task, so `/05-doti-analyze` can confirm coverage.
3. **Make each task executable:** one concrete action, the exact file path(s) it touches, specific enough to complete without re-deriving the plan. Include tasks to update source assets, installed bootstrap files, and docs where the change requires it.
4. **Organise into gate-enforced phases, ordered by the spec's declared Priority Mode** (not always MVP-first): *code / generated-code* → MVP-first: Setup → Foundational (blocking) → User Story N (priority order; P1 = MVP) → Polish; *docs / Doti-prose* → truth-first: fix stale/wrong claims → source-of-truth / rendered-asset parity → readability → polish; *workflow / tooling* → safety-first: fail-closed safety + deterministic proof → ergonomics. Phases run **sequentially**; each ends at a **Checkpoint** (`gate run` green) before the next begins. Number tasks `T001…` in execution order; mark `[P]` parallelizable (different files, no same-phase dependency) and `[US#]` for story traceability (code mode); sequence prerequisites first (contracts/types before consumers) and place the test task before the implementation it covers. (doti keeps phases sequential + gated — it does not run user stories in parallel the way upstream Spec Kit does.)
5. **Verification tasks.** Add a task to run the relevant command-backed checks (`gate run --profile normal` for normal work). Add advisory-review tasks only where a command does not exist yet, clearly labelled advisory — never present a manual check as gate proof.

Expected output: a dependency-ordered task list with file paths, requirement coverage, and verification tasks — executable without hiding missing gates.

## Next

Run `/05-doti-analyze` for a cross-artifact consistency review.
