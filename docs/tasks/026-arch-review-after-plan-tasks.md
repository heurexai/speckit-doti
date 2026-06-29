# 026 — Tasks: Arch-review after plan (cycle stage reorder)

Priority Mode: workflow/tooling — fail-closed safety + deterministic proof first. Tasks are ordered so the two structural stage-model sources change together (never a window where they disagree), then prose, then re-render, then proof.

## Phase 1 — Structural stage model (both sources, together) [W1]

- **T001** Edit `.doti/core/workflows/doti/workflow.yml`: reorder stage entries to `specify, clarify, plan, arch-review, tasks, analyze, implement, drift-review, release`; set `command:` to `04-doti-arch-review` / `05-doti-tasks` / `06-doti-analyze`; set `prereqs:` to `arch-review:[plan]`, `tasks:[arch-review]`, `analyze:[tasks]`, `implement:[analyze]`. (FR-001)
- **T002** Edit `tools/Hx.Doti.Core/Workflow/DotiWorkflowRegistry.cs`: reorder the `Stage(...)` table and set ordinals arch-review=4, tasks=5, analyze=6; fix `NextStageIds` (`plan→[arch-review]`, `arch-review→[tasks]`, `tasks→[analyze]`, `analyze→[implement]`); rewrite each affected `nextStep` string to the new `/0N-doti-*` target. (FR-003, FR-004)
- **T003** Find any existing test asserting `workflow.yml` ↔ `DotiWorkflowRegistry` consistency; if none exists, add one (ordinals + command names + order must match the YAML). Confirms T001/T002 cannot silently diverge. (FR-001, Risk: source desync)

## Phase 2 — Prose cross-references [W2]

- **T004** Edit `.doti/core/skills.json`: reorder entries; fix every `nextStage`; fix in-body `/0N-doti-*` cross-references; reword the `doti-arch-review` description so it states it runs after plan / before tasks+analyze and corrects the "hard prereq of /07" framing. (FR-005, FR-006)
- **T005** Edit `.doti/core/templates/commands/{doti-implement,doti-plan,doti-analyze,doti-arch-review,doti-tasks,doti-constitution,doti-specify}.md`: update all `/0N-doti-*` references and arch-review positioning prose. (FR-005, FR-006)
- **T006** Edit `.doti/core/templates/{agent-context-template,constitution-template,spec-template}.md`: update `/0N-doti-*` references and any "arch-review after analyze" wording. (FR-005)
- **T007** Edit `README.md`: update the cycle description to the new order/numbers. (FR-005)
- **T008** Triage the code references in `tools/Hx.Impact.Cli/ImpactCommands.cs`, `tools/Hx.Cycle.Core/SentruxOptimizationTracker.cs`, `tools/Hx.Cycle.Core/SentruxRebaselinePolicy.cs`, `tools/Hx.Tooling.Contracts/AffectedPlan.cs`: update any live `/0N`-by-number reference (comment or string) to the new numbering; `ErrorCodes.g.cs` is generated — regenerate, do not hand-edit. (FR-005)

## Phase 3 — Re-render installed assets [W3]

- **T009** Run `hx doti render-skills` to regenerate `.claude/`+`.agents/` skills, `.doti/agent-context.md`, `CLAUDE.md`/`AGENTS.md`, and the installed `.doti/workflows/doti/workflow.yml`; confirm the three renamed skill directories (`04-doti-arch-review`, `05-doti-tasks`, `06-doti-analyze`) replace the old-numbered ones with no orphans. (FR-007)

## Phase 4 — Proof [W1/W2/W3]

- **T010** Verify, all green: `hx doti cycle check`; `hx doti render-skills --check`; `hx doti payload check --repo .`; a repo-wide grep that finds zero live (non-CHANGELOG) `06-doti-arch-review` / `04-doti-tasks` / `05-doti-analyze` (SC-004); `dotnet build -c Release` + `dotnet test`; and `hx gate run --profile normal`. (SC-001..SC-005)
- **T011** Add a `CHANGELOG.md` entry under `[Unreleased]` describing the reorder.

## Acceptance mapping

- FR-001/002 → T001, T002, T003, T010 (cycle check + consistency test)
- FR-003/004 → T002, T010
- FR-005 → T004–T008, T010 (grep)
- FR-006 → T004, T005
- FR-007 → T009, T010 (render-skills --check, payload check)
- SC-001..005 → T010
