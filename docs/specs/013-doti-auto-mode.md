# 013 — Doti Auto Mode (auto-advance the cycle to a target)

## Goal

Let an operator drive a whole Doti cycle hands-off when nothing needs their judgment. A new unnumbered `doti-auto` skill, when invoked, advances the numbered cycle (`/01`–`/09`) automatically: at the end of each stage, if there is **no operator-blocking condition**, the next stage kicks in without the operator typing the next `/0N` command. The operator MAY bound the run to a target stage (`--until <stage>`); by default it runs all the way to the **local release**. It stops — and asks — only at genuine operator-decision points, and it never weakens a gate, skips a stamp, or publishes without explicit confirmation. This codifies the autonomous drive an agent can already do by hand into a repeatable, safe, bounded skill.

## User Scenarios & Testing

**Priority Mode** — workflow / tooling change: fail-closed safety + deterministic proof before ergonomics. Auto mode is *orchestration over the existing enforced stages*, never a bypass of them.

### Work Item 1 — Auto-advance through clean stages (Priority: P1)

An operator invokes `doti-auto` and the cycle proceeds stage by stage with no further input as long as each stage completes cleanly.

- **Why this priority:** the core value — remove the manual `/0N` typing between stages when nothing is ambiguous, while keeping every gate and stamp intact.
- **Independent Test:** invoke `doti-auto` on a feature whose stages produce no operator question and no gate failure; verify it stamps specify→…→release and runs `hx release` (local) without operator input.
- **Acceptance Scenarios:**
  1. **Given** a feature with no blocking ambiguity, **When** the operator invokes `doti-auto`, **Then** each stage is worked, gated, and stamped, and the run advances to the next stage automatically through the local release.
  2. **Given** `doti-auto --until arch-review`, **When** it runs, **Then** it stops after `arch-review` is stamped and does NOT enter `implement`.

### Work Item 2 — Stop at operator-decision points (Priority: P1)

Auto mode halts and surfaces the decision whenever the workflow genuinely needs the operator, instead of guessing.

- **Why this priority:** an auto driver that guesses at ambiguities or design trade-offs is worse than manual; the value depends on it stopping at the right places.
- **Independent Test:** force a stage to surface an operator-blocking condition (an open clarify ambiguity, an arch-review BLOCKER, an unrecoverable gate failure) and verify auto mode stops at that stage and presents the decision in the Operator-Question format.
- **Acceptance Scenarios:**
  1. **Given** `/02-clarify` would raise a blocking `[NEEDS CLARIFICATION]`, **When** auto mode reaches clarify, **Then** it stops and asks the question (Operator-Question Protocol), and does not invent an answer.
  2. **Given** `/06-arch-review` finds an open BLOCKER, **When** auto mode reaches arch-review, **Then** it stops before `implement` and presents the finding.
  3. **Given** a gate failure auto mode cannot resolve by a mechanical in-cycle fix, **When** it occurs, **Then** auto mode stops with the failing step, its evidence, and what it tried.

### Work Item 3 — Safe by construction (Priority: P1)

Auto mode preserves every workflow guarantee and never publishes on its own.

- **Why this priority:** the feature must not become a way to skip checks or push releases unattended; safety is the precondition for trusting it.
- **Independent Test:** run auto mode "to release" and verify it ends at the **local** `hx release` (tag + pack + local copy) and surfaces the remote push as a separate operator step, having passed (not skipped) every gate and stamped every diff-bound proof.
- **Acceptance Scenarios:**
  1. **Given** a "to release" run, **When** it reaches `/09`, **Then** it performs the local release and STOPS, presenting the remote tag/branch push as the explicit next operator step — it never pushes a `v*` tag or publishes.
  2. **Given** a spec↔code gap detected at drift-review, **When** auto mode handles it, **Then** it corrects the CODE in-cycle (never downgrades/defers the spec) and re-verifies, rather than asking the operator to relax the spec.

### Edge Cases

- The cycle is mid-flight (e.g., already at `plan`): `doti-auto` advances from the **current** stage, not from `specify`.
- `--until <stage>` names a stage already passed: auto mode reports "already at/past <stage>" and does nothing further.
- A stage is worked but its gate then goes stale (a later edit): auto mode re-runs the gate (the cheap lane for docs) before transitioning, never transitioning on a stale proof.
- Two agents/operators on the same repo: auto mode relies on the single cycle-state chokepoints; it does not add concurrency control beyond them and should not be run alongside another active driver.
- An operator interrupt mid-run: auto mode stops at the next safe boundary (after the current stamp), leaving a clean, resumable cycle.

## Scope

Included:

- A new **unnumbered** `doti-auto` utility skill (rendered from `.doti/core/skills.json` + a command template), invokable to auto-advance the cycle.
- A `--until <stage>` bound (default `release`).
- A precise, enumerated set of operator-blocking stop conditions and the safe in-cycle fixes auto mode may perform between them.
- An honest stop/continue report at each boundary and at the end.

Excluded:

- No new numbered cycle stage; no reordering of `/01`–`/09`; no change to the enforcing chokepoints (`doti cycle check`, the gate, the pre-commit hook).
- No coded "run the whole cycle" command that bypasses agent judgment per stage — auto mode is agent orchestration over the existing stage skills.
- No remote publish / `git push` of tags or branches (always an explicit operator step).
- No suppression or downgrade of any gate or proof.

## Functional Requirements

- `FR-001`: An **unnumbered** `doti-auto` utility skill MUST exist (rendered like `doti-bug`/`doti-amend`/`doti-constitution`), invokable at any point in a cycle to auto-advance the numbered stages. `[WI1]`
- `FR-002`: With no target, `doti-auto` MUST advance from the current stage through the **local release** (`/09`'s `hx release`), invoking each next stage automatically when the current stage completes with no operator-blocking condition. `[WI1]`
- `FR-003`: `doti-auto` MUST accept a `--until <stage>` bound and stop after that stage; the default target is `release`. `[WI1]`
- `FR-004`: `doti-auto` MUST STOP and surface the decision in the **Operator-Question Protocol** at any operator-blocking condition: an open clarify ambiguity / `[NEEDS CLARIFICATION]`; an arch-review **BLOCKER** or a missing applicable lens; a gate failure it cannot resolve by a mechanical in-cycle fix; or any genuine blocker (missing access, an unverifiable premise, a design decision it cannot make at ≥95% confidence). It MUST NOT guess past these. `[WI2]`
- `FR-005`: `doti-auto` MUST NOT weaken the workflow — it MUST run and pass each stage's gates (never skip a check or downgrade enforced→advisory), stamp every diff-bound proof, and fix a spec↔code gap by correcting the **CODE** in-cycle (never the spec). `[WI3]`
- `FR-006`: `doti-auto` MUST NOT publish without explicit operator confirmation: a run "to release" ends at the **local** `hx release`; the remote tag/branch push is always a separate explicit operator step that auto mode surfaces but never performs. `[WI3]`
- `FR-007`: At every stop (target reached or blocker) `doti-auto` MUST report honestly: stages completed, current stage, the stop reason, and — for a blocker — what it tried and the decision the operator must make. `[WI1, WI2]`
- `FR-008`: `doti-auto` MUST be advisory orchestration only — it MUST NOT add a numbered stage, reorder `/01`–`/09`, or replace the chokepoints; it chains the existing stage skills between operator-decision points. `[WI3]`

## Success Criteria

- `SC-001`: Invoking `doti-auto` on a feature with no blocking ambiguities drives specify→…→local release with no operator input, stamping each stage and passing each gate.
- `SC-002`: `doti-auto --until arch-review` stops after arch-review and does not enter implement.
- `SC-003`: When a stage surfaces an operator question (clarify ambiguity, arch-review BLOCKER), auto mode stops at that stage and presents the decision in Operator-Question format rather than guessing.
- `SC-004`: A "to release" run ends at the local `hx release`; auto mode never pushes a `v*` tag or publishes, and surfaces the remote push as the next operator step.
- `SC-005`: A run that hits an unrecoverable gate failure stops with the failing step, the evidence, and what was attempted — not a silent skip.
- `SC-006`: `doti render-skills --check` + `doti payload check` remain clean after the new skill is added; the skill renders to `.claude` + `.agents` like the other utility skills.

## Key Entities

- **doti-auto skill** — the unnumbered orchestration skill: drives the numbered stages, applies the stop conditions, honors `--until`, and reports.
- **Target stage** — the `--until` bound (default `release`); auto mode stops after it.
- **Operator-blocking condition** — the enumerated set of points where the workflow requires the operator (FR-004); the boundary between auto-advance and stop.

## Deterministic Surfaces

- `.doti/core/skills.json` — the new unnumbered `doti-auto` skill (NOT `workflow.yml`); rendered to `.claude`/`.agents`.
- `.doti/core/templates/commands/doti-auto.md` — the command template (the orchestration behavior + stop conditions).
- The existing stage skills (`/01`–`/09`) + chokepoints (`doti cycle check`, `gate run`, `hx release`) — auto mode invokes/honors these unchanged; it adds no new enforcement surface.
- `doti render-skills`/`--check`, `doti payload check` — the parity proofs that gate the new skill.

## Architecture Impact

- **Doti-prose only** — a new skill + command template, single-sourced in `.doti/core` and rendered (mirrors the 009 `doti-constitution` skill addition). No `*.Core` code, no contract, no rule/Sentrux change. (012's telemetry rendering is complementary — it makes auto mode's per-stage scope/ladder visible — but 013 does not depend on 012.)

## Sentrux And Hygiene Impact

- No Sentrux baseline change; no code. The skill text is Doti prose (Sentrux source-excluded).
- Hygiene: the skill must not encourage skipping checks or pushing unattended (FR-005/006); it is the opposite of a bypass.

## Assumptions

- An agent can already drive the full cycle autonomously between operator-decision points (demonstrated this session for cycles 009–011); auto mode codifies that into a bounded, safe, repeatable skill rather than adding new automation machinery.
- "Complete to release" means the local `hx release` (tag + pack + local copy), consistent with `hx release` not pushing tags; publishing remains a deliberate operator action.
- The stop conditions (FR-004) are sufficient to keep auto mode from making operator-only decisions; clarify questions, arch-review BLOCKERs, and unrecoverable gate failures are the principal halts.

## Acceptance

- Command-backed today: the existing stage skills + `doti cycle check`/`stamp`, `gate run`, `hx release`, `doti render-skills --check`, `doti payload check` — auto mode orchestrates these; it adds no new command, only a skill.
- Advisory until built: the `doti-auto` skill itself and its rendered output.

## Clarifications

### Pre-emptive clarify 2026-06-28 (no stamp — resolved ahead of the cycle)

- **Q: What is the auto-fix-vs-stop boundary (when does auto mode fix-and-continue vs stop for the operator)?** **A (assumption):** auto mode applies the engineering-discipline 95%-confidence bar — it RCA's and fixes a gate/stage failure it is ≥95% confident about (e.g. a function over the Sentrux size limit → extract; a stale doc reference → update; a missing test → add; the kind of mechanical fixes done by hand in cycles 009–011), re-verifies, and continues. It STOPS only when a fix requires an operator decision, a design re-plan (an arch-review BLOCKER), or it cannot reach ≥95% confidence. A spec↔code gap is fixed in the CODE in-cycle, never by relaxing the spec.
- **Q: How does auto mode fit a multi-feature release train?** **A (assumption):** invoked per train member with `--until drift-review` (FR-003); the member completes to drift-review and the operator/orchestrator starts the next member and runs the final aggregated release. The default `release` target is for a single-cycle release. (See the open scope decision below for whether `doti-auto` should itself chain the train.)
- **OPEN (operator scope decision, recommended A pending confirmation): per-cycle vs train-capable.** `doti-auto` currently drives ONE cycle to `--until`. **A (Recommended):** keep it per-cycle — a release train is composed of per-cycle `--until drift-review` runs the operator starts explicitly (simplest-correct, bounded, auditable; the train composition stays visible). **B:** a `--train` mode that chains features (drive member → drift-review → start next → … → release all) in one invocation — powerful but a much larger blast radius and more autonomy to bound safely. Resolved to **A** unless the operator chooses B.
