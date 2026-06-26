# doti-implement

Purpose: implement a scoped scaffold-dotnet task.

Command-aware advisory behavior:

1. Read `.doti/agent-context.md` and the relevant command brief.
2. Make narrow edits aligned with the active spec/plan/tasks, **built modularly — extract each distinct concern into a named, separately-testable method/type (logic in a `*.Core` type; the CLI stays parse→delegate→render), composed rather than inlined; keep methods within the Sentrux function-size limit (don't over-split either).** **Implement reinforces the approved design; it is NOT where design happens.** If implementation reveals the plan/arch-review is materially wrong — a better architecture, or a design that won't satisfy an FR/SC — **STOP: do not silently code a different design (that is drift `/08` + the gate cannot fully catch), and do not force-fit a plan you've found wrong.** Report the mismatch and re-run `/03-doti-plan` (then `/04-doti-tasks` / `/06-doti-arch-review` as needed) to update the approved design before coding through it. When the change adds or changes a CLI command, register its declared error codes in `errorcodes/registry.json`, run `errorcodes check` (the append-only stability gate), and confirm `describe --json` reflects the new command/option surface — so the help/error system stays self-describing.
3. Update README and the docs when behavior changes.
4. Run the command-backed build, then `gate run --profile normal --repo .` (the aggregated, fail-closed gate: hygiene, tool verification, affected-test planning + hashed proof, prebuilt test execution, architecture, skill-drift, Sentrux) and treat any failure as blocking. Direct `dotnet test` runs are useful diagnostics only; they do not authorize commit.
5. Run advisory checks only for planned commands that do not exist yet.
6. Report advisory gaps explicitly.
7. On completion, stamp the stage (`doti cycle stamp --stage implement`) so the diff-bound proof the commit chokepoint verifies is recorded. The stamp itself fails closed if transitive prerequisites are missing, stale, or invalid.

## Engineering discipline (required)

Implement to a 95%-confidence bar. Do not rush, do not take shortcuts, and do not skip the hard parts.

1. **Root-cause, don't patch symptoms.** On any failure or surprise, do a real RCA — read the code/output, reproduce it, find the underlying cause — before changing anything. Fix the root cause, not the surface.
2. **Validate assumptions — verify, never assume.** Confirm every premise by reading the code, running the command, or observing the output. If you state a cause or behavior, prove it (reproduce/RCA) and cite the evidence.
3. **No shortcuts.** Do not silence a check, hard-code around a problem, stub past a failure, or declare done without proof. If the idiomatic/correct solution is harder, do that one.
4. **95% confidence or keep going.** If you are not ≥95% confident the solution and code are correct, keep finding better approaches and validating until you are. Prove it works (build/test/run); never claim it.
5. **If truly blocked, say so.** When confidence is unreachable or you are genuinely blocked (missing access, an operator decision, an unverifiable premise), surface it with what you tried and what is needed — do not fabricate or guess.
6. **Report honestly.** State what was validated and how, what remains, and any scope refinement or deferral with its rationale. Never overclaim.

Expected output: completed edits, verification results, and missing gate notes.

## Context budget (large features)

A feature too large for one context is implemented scoped + resumable, not in one heroic run: work the lowest-numbered unchecked task, stamp its diff-bound completion marker, and let a fresh context resume at the next unchecked task — the ledger + markers are the durable state, and the ordered-task gate (T003) keeps a resumed or parallel run from skipping or reordering. Delegate `[P]` tasks (different files, no in-phase dependency) to parallel sub-agents; decompose a feature too large even for that into sub-features ("spec of specs"). See `docs/concepts/complex-features.md`.

## Next

Run `/08-doti-drift-review` to check the diff against the approved design.
