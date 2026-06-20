# doti-tasks

Purpose: turn an approved plan into concrete, ordered, executable tasks.

## Behavior

1. Read `.doti/agent-context.md`, the plan, and the active spec.
2. Derive tasks from the plan's design and the spec's `FR-###`/`SC-###` — each requirement should map to at least one task, so `/doti-analyze` can confirm coverage.
3. **Make each task executable:** one concrete action, the exact file path(s) it touches, specific enough to complete without re-deriving the plan. Include tasks to update source assets, installed bootstrap files, and docs where the change requires it.
4. **Order by dependency.** Sequence so prerequisites come first (e.g. contracts/types before the code that consumes them); call out tasks that can proceed independently. Where the change includes tests, place the test task before the implementation it covers.
5. **Verification tasks.** Add a task to run the relevant command-backed checks (`gate run --profile normal` for normal work). Add advisory-review tasks only where a command does not exist yet, clearly labelled advisory — never present a manual check as gate proof.

Expected output: a dependency-ordered task list with file paths, requirement coverage, and verification tasks — executable without hiding missing gates.

## Next

Run `/doti-analyze` for a cross-artifact consistency review.
