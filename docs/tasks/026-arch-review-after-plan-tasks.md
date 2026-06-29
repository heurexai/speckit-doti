# 026 — Tasks: Arch-review after plan (cycle stage reorder)

Priority Mode: workflow/tooling — fail-closed safety + deterministic proof first. Tasks are ordered so the two structural stage-model sources change together (never a window where they disagree), then prose, then re-render, then proof.

## Phase 1 — Structural stage model (both sources, together) [W1]

- [x] T001 Edit `.doti/core/workflows/doti/workflow.yml`: reorder stage entries to `specify, clarify, plan, arch-review, tasks, analyze, implement, drift-review, release`; set `command:` to `04-doti-arch-review` / `05-doti-tasks` / `06-doti-analyze`; set `prereqs:` to `arch-review:[plan]`, `tasks:[arch-review]`, `analyze:[tasks]`, `implement:[analyze]`. (FR-001) <!-- doti-task-hash: 6fbdc8065759c5c7908dbd5eb264ee593053803da44848ff2eb756fa1cba59e5 -->
- [x] T002 Edit `tools/Hx.Doti.Core/Workflow/DotiWorkflowRegistry.cs`: reorder the `Stage(...)` table and set ordinals arch-review=4, tasks=5, analyze=6; fix `NextStageIds` (`plan->[arch-review]`, `arch-review->[tasks]`, `tasks->[analyze]`, `analyze->[implement]`); rewrite each affected `nextStep` string to the new `/0N-doti-*` target. (FR-003, FR-004) <!-- doti-task-hash: 2f834ecc7a124e5881a464d9ab52d7c0ea95e0a342cdb879c35a9480330679d4 -->
- [x] T003 Confirm the existing `Workflow_assets_match_registry` test asserts `workflow.yml` <-> `DotiWorkflowRegistry` consistency across both source and installed copies (it does); keep them consistent. (FR-001, Risk: source desync) <!-- doti-task-hash: c883b4c15a4dc582805637214022037ecd17c31a4c95db7769de3ebeacad1156 -->

## Phase 2 — Prose cross-references [W2]

- [x] T004 Edit `.doti/core/skills.json`: reorder is registry-driven; fix every `nextStage` flow pointer; fix in-body `/0N-doti-*` cross-references; reword the `doti-arch-review` description so it states it runs after plan / before tasks+analyze and corrects the "hard prereq of /07" framing. (FR-005, FR-006) <!-- doti-task-hash: bbfd5691f1ebe3004b1ae5a779453645132ca704718f6359d01a44bc2acf11ee -->
- [x] T005 Edit `.doti/core/templates/commands/{doti-implement,doti-plan,doti-analyze,doti-arch-review,doti-tasks,doti-constitution,doti-specify,doti-auto}.md`: update all `/0N-doti-*` references, the `## Next` pointers, and arch-review positioning prose. (FR-005, FR-006) <!-- doti-task-hash: 1fe492a9625ff6149d85d2854b6ca35dc0630106d356eee15120a082bdb4468d -->
- [x] T006 Edit `.doti/core/templates/{agent-context-template,constitution-template,spec-template}.md`: update `/0N-doti-*` references and order narration. (FR-005) <!-- doti-task-hash: d4c63b8dd269cbfe16e4a35bf853711885bffee944e951fae6f7fd1d67f5a5c5 -->
- [x] T007 Edit `README.md`: update the cycle description to the new order/numbers. (FR-005) <!-- doti-task-hash: 398b4b9c26339d1a6e9b3fb165add201bccff1db4d993dace984205b8b75dec5 -->
- [x] T008 Update code references and tests: `tools/Hx.Impact.Cli/ImpactCommands.cs`, `tools/Hx.Cycle.Core/SentruxOptimizationTracker.cs`, `tools/Hx.Cycle.Core/SentruxRebaselinePolicy.cs`, `tools/Hx.Tooling.Contracts/AffectedPlan.cs`, the `errorcodes/registry.json` (-> regenerated `ErrorCodes.g.cs`), and the number-coupled tests (`DotiWorkflowRegistryTests` x2, `ImpactCommandsTests`, `RefreshTests`, `SentruxSourceScopeTests`). (FR-005) <!-- doti-task-hash: 2ecf06ed611d29853ea7cb12b590a0c1b8309a1afad8edc6fc8638b1817fe3a8 -->

## Phase 3 — Re-render installed assets [W3]

- [x] T009 Run `hx doti render-skills` to regenerate `.claude/`+`.agents/` skills, `.doti/agent-context.md`, `CLAUDE.md`/`AGENTS.md`; sync the installed `.doti/workflows/doti/workflow.yml`; and remove the three orphaned old-numbered skill directories. (FR-007) <!-- doti-task-hash: 6314d8f244ff55f54ad98955a9506e8f6ae21d6064a75a0fa76cb19cae40d476 -->

## Phase 4 — Proof [W1/W2/W3]

- [x] T010 Verify, all green: `hx doti cycle check`; `hx doti render-skills --check`; `hx doti payload check --repo .`; a repo-wide grep finding zero live (non-CHANGELOG) `06-doti-arch-review` / `04-doti-tasks` / `05-doti-analyze` (SC-004); `dotnet build -c Release` + `dotnet test`; and `hx gate run --profile normal`. (SC-001..SC-005) <!-- doti-task-hash: 867856610c70bdb39e650ce799eb8d98b3ed2ba0fbd425b26465d28bc62da73d -->
- [x] T011 Add a `CHANGELOG.md` entry under `[Unreleased]` describing the reorder. <!-- doti-task-hash: ec7e91a5a0d0cc7dfd8efa89c925cdc0530b4ba0f8b29d747edb8d317543dac3 -->

## Acceptance mapping

- FR-001/002 -> T001, T003, T010 (cycle check + consistency test)
- FR-003/004 -> T002, T010
- FR-005 -> T004-T008, T010 (grep)
- FR-006 -> T004, T005
- FR-007 -> T009, T010 (render-skills --check, payload check)
- SC-001..005 -> T010
