# doti-tasks

Purpose: turn an approved plan into concrete, ordered, executable tasks.

## Behavior

1. Read `.doti/agent-context.md`, the plan, and the active spec.
2. Derive tasks from the plan's design and the spec's `FR-###`/`SC-###` — each requirement should map to at least one task, so `/06-doti-analyze` can confirm coverage.
3. **Make each task executable:** one concrete action, the exact file path(s) it touches, specific enough to complete without re-deriving the plan. Include tasks to update source assets, installed bootstrap files, and docs where the change requires it.
4. **Organise into gate-enforced phases, ordered by the spec's declared Priority Mode** (not always MVP-first): *code / generated-code* → MVP-first: Setup → Foundational (blocking) → User Story N (priority order; P1 = MVP) → Polish; *docs / Doti-prose* → truth-first: fix stale/wrong claims → source-of-truth / rendered-asset parity → readability → polish; *workflow / tooling* → safety-first: fail-closed safety + deterministic proof → ergonomics. Phases run **sequentially**; each ends at a **Checkpoint** (`gate run` green) before the next begins. Number tasks `T001…` in execution order; mark `[P]` parallelizable (different files, no same-phase dependency) and `[US#]` for story traceability (code mode); sequence prerequisites first (contracts/types before consumers) and place the test task before the implementation it covers. (doti keeps phases sequential + gated — it does not run user stories in parallel the way upstream Spec Kit does.)
5. **Verification tasks.** Add a task to run the relevant command-backed checks (`gate run --profile normal` for normal work). Add advisory-review tasks only where a command does not exist yet, clearly labelled advisory — never present a manual check as gate proof.
6. **End with the mandatory documentation sweep — ALWAYS the LAST task, in every cycle.** Append, as the final task (permanent and unconditional — never dropped as "no behavior change", never made optional), a documentation sweep that updates EVERY doc describing the change so the docs match the code: `README.md` (including any stage/command/order lists and tables), `CHANGELOG.md`, the agent context (`CLAUDE.md` / `AGENTS.md` / `.doti/agent-context.md`), each affected rendered skill, and the `hx describe` / `--help` surface — then re-render installed doti assets. The `/08` code↔docs drift axis and the `release-documentation` gate (every included feature slug must appear in **both** `README.md` **and** `CHANGELOG.md`) enforce it.

Expected output: a dependency-ordered task list with file paths, requirement coverage, verification tasks, and a mandatory documentation-sweep final task — executable without hiding missing gates.

## Reconciling after an upstream change

If you are re-running this stage because an upstream artifact changed, the engine has already done the bookkeeping: the stale stage carries the changed upstream paths + the line-level diff. Read it and assess impact before acting — re-author here if the change affects this stage's artifact; if you review the diff and it provably does not, record `hx doti cycle review-rebind --target <this-stage> --attest no-impact --reason "<why>"` instead. Clearing the stale flag without assessing the diff is a rubber-stamp and is forbidden — a bare `hx doti cycle stamp` of an upstream-changed stage refuses and routes you to the verb.

## Next

Run `/06-doti-analyze` for a cross-artifact consistency review.
