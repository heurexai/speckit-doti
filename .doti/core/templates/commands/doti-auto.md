# doti-auto

Purpose: drive the numbered Doti cycle (`/01`–`/09`) **automatically** to a target, advancing stage-to-stage with no operator input as long as nothing needs the operator's judgment. A **utility** skill — orchestration over the existing enforced stages, never a bypass of them. By default it runs to the **local release**; `--until <stage>` bounds it. Runs anytime from any point in a cycle.

## Behavior

1. Read `.doti/agent-context.md` and `hx doti cycle status --repo .`. Resolve the **current stage** and the **target** — the `--until <stage>` argument, default `release`. If already at/past the target, report "already at/past `<stage>`" and stop.
2. **Loop from the current stage to the target.** For each stage in order (specify → clarify → plan → arch-review → tasks → analyze → implement → drift-review → release): run that stage's `/0N` skill behavior, produce its artifact, pass its gate where one applies (`gate run` at `/07` and `/09`), and stamp it with `hx doti cycle stamp`. Then evaluate the **stop conditions** (step 3). If none trip and the target is not reached, advance to the next stage **automatically** — do NOT wait for the operator to type the next command.
3. **Stop conditions — halt and surface the decision in the Operator-Question Protocol (do not guess past these):**
   - **Operator question / ambiguity:** `/02-clarify` would raise a blocking question, or any stage hits a genuine `[NEEDS CLARIFICATION]` or a design decision you cannot make at ≥95% confidence.
   - **Arch-review BLOCKER:** `/04` finds an open BLOCKER in an applicable lens (or a missing applicable lens).
   - **Unrecoverable gate failure:** a `gate run` failure you cannot resolve by a mechanical in-cycle fix (see step 4).
   - **Publish:** reaching `release` performs only the **local** `hx release`; the remote `v*` tag/branch push is ALWAYS a separate explicit operator step — surface it, never perform it.
   - **Genuine blocker:** missing access, an unverifiable premise, or anything the workflow requires the operator to decide.
4. **Auto-fix what you are sure of (the 95%-confidence bar).** A gate/stage failure you can RCA and fix mechanically — a function over the Sentrux size limit (extract), a stale doc reference (update), a missing test (add), a code↔docs drift — fix it in-cycle, re-verify, and continue. A spec↔code gap is fixed in the **CODE**, never by relaxing the spec. Only stop (step 3) when the fix needs an operator decision or you cannot reach ≥95% confidence.
5. **Never weaken the workflow.** Run and pass each stage's gates (never skip a check or downgrade enforced→advisory), stamp every diff-bound proof, and honor the chokepoints (`doti cycle check`, the gate, the pre-commit hook). Commits stay owned by the coded transitions — never hand-commit. Auto mode adds NO numbered stage, no reorder of `/01`–`/09`, and replaces no chokepoint; it chains the existing stage skills between operator-decision points.
6. **Report at every boundary** (each stop, and the end): the stages completed, the current stage, why you stopped, and — for a blocker — what you tried and the decision the operator must make.

Expected output: the cycle advanced to the target stage (or to the first operator-decision point), every stage gated + stamped, with an honest boundary report.

## Release-train usage

For a release train (several features released together), invoke per member with `--until drift-review`: the member completes to drift-review (completed-unreleased), then the operator starts the next member and runs the final aggregated `/09-doti-release`. The default `release` target is for a single-cycle release.

## Next

When auto mode stops at a blocker, resolve it and re-invoke `/doti-auto` to resume from the current stage; when it reaches the target, the cycle is at that stage (the local release, or your `--until` bound).
