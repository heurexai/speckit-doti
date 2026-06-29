# 026 — Arch-review after plan (cycle stage reorder)

## Goal

Move the multi-lens architecture review so it runs **immediately after `plan` and before `tasks`/`analyze`**, instead of after `analyze`. The design is validated before the coverage and consistency work that depends on it, so an arch-review BLOCKER forces a `plan` revision *before* tasks have been broken down and analyzed against a flawed design — a tighter, cheaper iteration loop and a more logical authoring order (design → review the design → break the reviewed design into tasks).

Target stage order: `specify(01) → clarify(02) → plan(03) → arch-review(04) → tasks(05) → analyze(06) → implement(07) → drift-review(08) → release(09)`.

## User Scenarios & Testing

**Priority Mode: Workflow / tooling change** — fail-closed safety + deterministic proof before ergonomics. The cycle's prerequisite enforcement must stay correct and fail-closed under the new order; cosmetic renumbering polish ranks last.

### Work Item W1 — Correct, fail-closed stage model (Priority: P1)

The cycle stage model (`workflow.yml`) declares the new order and prerequisite graph, and `doti cycle stamp`/`check` enforce it fail-closed.

- **Why this priority:** the prerequisite graph is the cycle's safety mechanism. If `arch-review` no longer requires `plan`, or `tasks` no longer requires `arch-review`, the reorder is unsound — a cycle could stamp tasks against an unreviewed design, the exact failure this change exists to prevent.
- **Independent Test:** with the new model installed, `doti cycle stamp --stage tasks` is refused until `arch-review` is stamped + fresh; `doti cycle stamp --stage arch-review` is refused until `plan` is stamped + fresh. `doti cycle check` reports the transitive prerequisite chain in the new order.
- **Acceptance Scenarios:**
  1. **Given** a cycle with `plan` stamped + fresh but `arch-review` not yet stamped, **When** `doti cycle stamp --stage tasks` is attempted, **Then** it fails closed with a missing-prerequisite error naming `arch-review`.
  2. **Given** a cycle with only `specify`+`clarify` stamped, **When** `doti cycle stamp --stage arch-review` is attempted, **Then** it fails closed naming the missing `plan` prerequisite.
  3. **Given** the new model, **When** the transitive prerequisite closure of `implement` is computed, **Then** it is `specify, clarify, plan, arch-review, tasks, analyze` in that order.

### Work Item W2 — Consistent renumbering across every reference (Priority: P2)

`arch-review` becomes command `04-doti-arch-review`, `tasks` becomes `05-doti-tasks`, `analyze` becomes `06-doti-analyze`; `specify`/`clarify`/`plan`/`implement`/`drift-review`/`release` keep `01`/`02`/`03`/`07`/`08`/`09`. Every cross-reference (nextStage pointers, in-body "/0N-doti-…" mentions, the agent-context, templates, README, code) is updated to match.

- **Why this priority:** a half-renumbered workflow is a correctness-of-documentation defect — a skill that says "run `/06-doti-arch-review` next" after the rename points at a command that no longer exists, and an operator following it is dead-ended. Consistency across the single source is the whole point of single-sourcing.
- **Independent Test:** a repo-wide search for the OLD numbering (`06-doti-arch-review`, `04-doti-tasks`, `05-doti-analyze`) finds zero live references in source (CHANGELOG history excepted); each stage's `nextStage` points at the correct successor in the new order.
- **Acceptance Scenarios:**
  1. **Given** the renumbered model, **When** `plan`'s `nextStage` is read, **Then** it directs to `/04-doti-arch-review`.
  2. **Given** the renumbered model, **When** `arch-review`'s and `tasks`'s `nextStage` are read, **Then** they direct to `/05-doti-tasks` and `/06-doti-analyze` respectively.
  3. **Given** the arch-review skill body, **When** its self-description is read, **Then** it no longer claims to run "after analyze" / frames itself as reviewing the plan before tasks, and its "hard prereq of /07" reference is corrected to the new flow.

### Work Item W3 — Source↔installed parity stays green (Priority: P3)

The installed rendered skills, agent-context, and thin entrypoints are regenerated from the edited source so both parity authorities pass.

- **Why this priority:** drift between `.doti/core` source and the installed `.claude`/`.agents` skills is the class of defect the drift-review stage exists to catch; shipping a reorder that leaves installed skills on the old numbering would be self-contradicting in the very repo that enforces parity.
- **Independent Test:** `doti render-skills --check` and `doti payload check --repo .` both pass with zero drift after re-render.
- **Acceptance Scenarios:**
  1. **Given** the edited source and a re-render, **When** `doti render-skills --check` runs, **Then** it reports no drift.
  2. **Given** the re-render, **When** `doti payload check --repo .` runs, **Then** parity passes.

### Edge Cases

- **A cycle already mid-flight under the old order.** Stage **IDs** (`plan`, `tasks`, `analyze`, `arch-review`, …) are unchanged — only command **numbers**, declaration order, and `prereqs` change. `cycle-state.json` keys by stage ID, so existing stamps are not orphaned; but a cycle that stamped `analyze` before `arch-review` under the old graph may read as having an unsatisfied prerequisite under the new graph (expected — the new graph is the new truth; such a cycle re-runs the now-out-of-order stage).
- **The cycle implementing this change.** Cycle 026 itself runs under the model active at each stamp; the new order takes effect for cycles started after the change is installed.
- **Muscle-memory `/06-doti-arch-review`.** After the rename that slash-command no longer resolves; this is intended (renumber is the goal), not a regression.
- **Stage whose `produces` path is keyed by stage, not number.** `produces` patterns (`docs/reviews/{feature}-arch-review.md`, etc.) are unchanged, so review artifacts keep their filenames.

## Scope

**Included:** reorder the 9 cycle stages to `specify, clarify, plan, arch-review, tasks, analyze, implement, drift-review, release`; renumber the three affected commands (arch-review→04, tasks→05, analyze→06); update the prerequisite graph (`arch-review`←`plan`, `tasks`←`arch-review`, `analyze`←`tasks`, `implement`←`analyze`); update every source cross-reference to the renumbered commands; regenerate installed skills/agent-context/entrypoints; keep `doti cycle check`, `render-skills --check`, `payload check`, and `gate run` green.

**Excluded:** no change to **what** any stage does (arch-review's lenses, tasks' breakdown format, analyze's cross-artifact checks are byte-for-byte the same content, only repositioned/renumbered); no new or removed stage; no change to stage **IDs**; no change to the bug mini-cycle (`/doti-bug`) or the unnumbered utility skills' own behavior beyond their cross-references; no change to the release/publish flow; no change to `cycle-state.json` schema.

## Functional Requirements

- `FR-001`: The cycle stage model MUST declare stages in the order `specify, clarify, plan, arch-review, tasks, analyze, implement, drift-review, release`, with `arch-review.prereqs = [plan]`, `tasks.prereqs = [arch-review]`, `analyze.prereqs = [tasks]`, and `implement.prereqs = [analyze]`. `[W1]`
- `FR-002`: `doti cycle stamp` MUST fail closed when a stage is stamped before its new transitive prerequisites are all stamped + fresh — specifically `tasks` before `arch-review`, and `arch-review` before `plan`. `[W1]`
- `FR-003`: The arch-review command MUST be numbered `04-doti-arch-review`, tasks `05-doti-tasks`, and analyze `06-doti-analyze`; `specify`/`clarify`/`plan`/`implement`/`drift-review`/`release` MUST remain `01`/`02`/`03`/`07`/`08`/`09`. `[W2]`
- `FR-004`: Each stage's `nextStage` pointer MUST direct to its successor in the new order (`plan`→`/04-doti-arch-review`, `arch-review`→`/05-doti-tasks`, `tasks`→`/06-doti-analyze`, `analyze`→`/07-doti-implement`). `[W2]`
- `FR-005`: Every live source reference to the renumbered slash-commands (`skills.json`, command templates, agent-context template, constitution/spec templates, README, and any code that names them) MUST be updated to the new numbering; no live reference to the OLD numbering (`06-doti-arch-review`, `04-doti-tasks`, `05-doti-analyze`) MAY remain outside historical CHANGELOG entries. `[W2]`
- `FR-006`: The arch-review skill body MUST be reworded so it no longer describes itself as running after analyze; it MUST frame itself as reviewing the plan's design before tasks/analyze, and its internal stage references (e.g. "hard prereq of /07") MUST be corrected to the new flow. `[W2]`
- `FR-007`: Installed rendered skills, the agent-context, and the thin `CLAUDE.md`/`AGENTS.md` entrypoints MUST be regenerated from the edited source so `doti render-skills --check` reports no drift. `[W3]`
- `FR-008`: The change MUST NOT alter stage IDs, `produces` artifact path patterns, or the `cycle-state.json` schema, so existing recorded proofs remain interpretable. `[W1]`

## Success Criteria

- `SC-001`: The installed stage model reports its 9 stages in the order `specify, clarify, plan, arch-review, tasks, analyze, implement, drift-review, release`.
- `SC-002`: Stamping `tasks` with `arch-review` unstamped fails closed (non-zero exit, prerequisite error naming `arch-review`); stamping `arch-review` with `plan` unstamped fails closed naming `plan`.
- `SC-003`: `doti render-skills --check` and `doti payload check --repo .` both pass with zero drift.
- `SC-004`: A repo-wide search returns zero live (non-CHANGELOG) references to `06-doti-arch-review`, `04-doti-tasks`, or `05-doti-analyze`.
- `SC-005`: `gate run --profile normal` passes on the change set.

## Deterministic Surfaces

- `.doti/core/workflows/doti/workflow.yml` — the single source of the stage order + prerequisite graph + per-stage command (loaded by `StageModel`, schemaVersion 2).
- `.doti/core/skills.json` — skill ordering, numbering, `nextStage` pointers, and in-body cross-references (the render source for installed skills + agent-context + entrypoints).
- `doti cycle stamp` / `doti cycle check` — fail-closed prerequisite enforcement (proof of `FR-001`/`FR-002`).
- `doti render-skills --check` and `doti payload check --repo .` — source↔installed parity (proof of `FR-007`).
- `gate run --profile normal` — the aggregate gate (`SC-005`).

## Architecture Impact

- **Workflow data:** `.doti/core/workflows/doti/workflow.yml` (and the re-rendered `.doti/workflows/doti/workflow.yml`). No `StageModel.cs` logic change expected — the model is data-driven; this is a data edit. To be confirmed by `/03-doti-plan`.
- **Render source:** `.doti/core/skills.json`; command templates `.doti/core/templates/commands/{doti-plan,doti-tasks,doti-analyze,doti-arch-review,doti-implement,doti-constitution,doti-specify}.md`; `.doti/core/templates/{agent-context-template,constitution-template,spec-template}.md`.
- **Rendered/installed:** `.claude/skills/*`, `.agents/skills/*`, `.doti/agent-context.md`, `CLAUDE.md`, `AGENTS.md` (regenerated, not hand-edited).
- **Code referencing the numbers (to be triaged in plan — functional vs comment/example):** `tools/Hx.Doti.Core/Workflow/DotiWorkflowRegistry.cs`, `tools/Hx.Impact.Cli/ImpactCommands.cs`, `tools/Hx.Cycle.Core/SentruxOptimizationTracker.cs`, `tools/Hx.Cycle.Core/SentruxRebaselinePolicy.cs`, `tools/Hx.Tooling.Contracts/AffectedPlan.cs`. (`tools/Hx.Cli.Kernel/ErrorCodes.g.cs` is generated — regenerated, not hand-edited.)
- **Docs:** `README.md` cycle description; `CHANGELOG.md` entry.

## Sentrux And Hygiene Impact

Predominantly data (YAML/JSON) and prose (MD) edits plus file renames of three rendered skill directories; little or no production-logic change is expected (the stage model is declarative). If any code reference (e.g. `DotiWorkflowRegistry`) hard-codes a command name/number functionally, the edit is a small string change with no new control flow, so no Sentrux structural-growth or baseline impact is anticipated. Hygiene risk is a **stale reference** to the old numbering surviving in a doc or comment — `FR-005`/`SC-004` exist to close exactly that.

## Assumptions

- arch-review lands at `04` (immediately after `plan`); `tasks`→`05`, `analyze`→`06`. This is the only ordering consistent with arch-review reviewing the plan's design, and with the operator's "after plan" intent. (The operator said "should be 3"; plan occupies 03 and must precede arch-review, so 04 is the adjacent slot.)
- Stage **IDs** are stable; only command numbers, declaration order, and `prereqs` change — preserving `cycle-state.json` keying and existing proofs.
- `analyze` continues to depend on `tasks` (it reviews the task breakdown), now positioned after `arch-review`.
- Version intent is a feature cycle (workflow change) → defaults to minor at `/09`; the operator may override.

## Acceptance

Command-backed today: `doti cycle stamp`/`check` (FR-001/002/008), `doti render-skills --check` + `doti payload check` (FR-007/SC-003), `gate run` (SC-005), and a repo-wide grep (SC-004). No advisory/planned-but-absent commands are relied on.

## Clarifications

(Populated by `/02-doti-clarify` if any blocking ambiguity is raised.)
