# doti-implement

Purpose: implement a scoped scaffold-dotnet task.

Command-aware advisory behavior:

1. Read `.doti/agent-context.md` and the relevant command brief.
2. Make narrow edits aligned with the active spec/plan/tasks. When the change adds or changes a CLI command, register its declared error codes in `errorcodes/registry.json`, run `errorcodes check` (the append-only stability gate), and confirm `describe --json` reflects the new command/option surface — so the help/error system stays self-describing.
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

## Next

Run `/doti-drift-review` to check the diff against the approved design.
