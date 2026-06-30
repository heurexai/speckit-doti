# 028 — Tasks: self-describing, agent-gated cycle reconciliation

Priority: fail-closed safety + deterministic proof first. The in-`Stamp` fence + the verb land before the action-model migration; the render migration lands ONLY behind the byte-stable golden baseline. Tests pair with each change. (Boxes are checked + `doti-task-hash`-stamped during `/07-implement`.)

## Phase 1 — Contracts + the pure fence

- [x] T001 `Hx.Tooling.Contracts/CycleState.cs`: additive — `CycleStageOutcome.ReviewedNoImpactRebound`; `CycleReviewedRebindRecord(SchemaVersion, Dependent, ChangedUpstreams, BeforeHashes, AfterHashes, Verdict, Reason?, AtUtc)`; nullable trailing `IReadOnlyList<CycleReviewedRebindRecord>? ReviewedRebinds = null` on `CycleState`. No schema bump; old readers tolerate it. (FR-005, B3) <!-- doti-task-hash: 43ef80ff7091a239cb60d3877d4dddd25bd708f7704492789f050ff7ab5847c2 -->
- [x] T002 `Hx.Cycle.Core/ReviewRebindEligibility.cs`: the ONE pure `IsAttestable(StageFreshnessResult, CycleStage) => PrereqArtifactChanged && !review-kind && !requireChangeSetIdentity`. Unit test the full git-free matrix (the SC-002 blocked cases). (FR-004, H4) <!-- doti-task-hash: 5cde730f9b589afc156953453fec2937ad3e36aef00185452a6fb41f261a03d3 -->

## Phase 2 — Freshness + classification

- [x] T003 `FreshnessEvaluator.cs`: the decay arm — a `ReviewedNoImpactRebound` proof re-validates `PrerequisiteArtifactHashes`; match ⇒ Fresh, mismatch ⇒ `PrereqArtifactChanged`. Carry the changed-prereq-path SET on `StageFreshnessResult` (no git here). (FR-002, FR-005) <!-- doti-task-hash: 3724478bba51cc0689c04e2d14efbbeb0601aec2015011ba92d61a57a7e4bbaa -->
- [x] T004 `RestampSafetyClassifier.cs` + `CycleRecoveryPlanner.cs`: add the `RestampSafety.ReviewedNoImpact` agent-eligible tier (gated by `IsAttestable`); leave `Refresh --apply-safe` applying only `SafeReinterpret`/`ReBindContentEqual`. (FR-004, FR-006) <!-- doti-task-hash: 0aa060048cd0b6d35b9becffce193c058fef52b62f27890609647660f5cdb62d -->

## Phase 3 — In-`Stamp` fence + the verb

- [x] T005 `CycleService.Stamp.cs`: distinguish a real re-author (own-artifact hash changed) from an attestable prereq-only rebind; REFUSE the latter — throw `CycleInputException` → `Validation_CycleReviewRebindRequiresAttest`, routing to the verb. (FR-004, B1) Test: bare `Stamp` on an attestable stale throws. <!-- doti-task-hash: a571952f3276c74d5c0469dd242e9904e733051868cb215d3c8b09d947286bc6 -->
- [x] T006 `CycleService.ReviewRebind.cs` (new partial): evaluate the TARGET's own freshness via `FreshnessEvaluator.Evaluate(targetProof)` (not `Check`); run `IsAttestable`; content-rebind the target only (cascade SUPPRESSED — B7); write proof + `ReviewedRebinds` entry in ONE `CycleStateStore.Write`. (FR-003/005, B2/B7) <!-- doti-task-hash: 32deb03763394b2affe7d11624ff0ca894ca608e7810835af2fd9d084d5775d8 -->
- [x] T007 `errorcodes/registry.json`: `Validation_CycleReviewRebindRequiresAttest`/`Ineligible`/`NotStale` (→ `ErrorCodes.g.cs`); clears the append-only stability gate. <!-- doti-task-hash: 0f46e073f14d91c79ccb1f9cfa97c558f2a84279dca22a25de7b97aa5634f81e -->
- [x] T008 `Hx.Runner.Cli`: the `doti cycle review-rebind --target --attest --reason` verb (factory + `RunnerCommands.DotiCycle`) — parse → delegate to `CycleService.ReviewRebind` → render `CliResult`. <!-- doti-task-hash: 7e5eef735c6af8d2b07673a4729248379999e34fd2d93406a3a322ec36ac95f3 -->
- [x] T009 `Hx.Cycle.Core/CycleService.cs`: a constructor test seam (inject `CycleStateStore`/`StageModel`) so the verb + the one-write atomicity (SC-007) are testable without a real git repo. (H5) <!-- doti-task-hash: 7ee78d0c9b50bef16dcde4e9693624b17376d0fc664595d2d442117d4f31cd55 -->

## Phase 4 — Single-sourced graph

- [x] T010 `StageModel.cs` (`+ CycleStage.Next` + `StageEntry.Next` binder) + `workflow.yml` (`next:` on all 9 stages); de-dup the `CheckHelpers.cs` transitive-prereq DFS into `StageModel.TransitivePrereqStages`. Additive within schemaVersion 2. (FR-007, D7) <!-- doti-task-hash: df2f4d8cec6e1caf143dd216c7508f90cd0a4cedf15f3c2f3417d4b9ef15cf24 -->

## Phase 5 — Diff surfacing (CLI/recovery seam)

- [x] T011 The changed-path set on `StageRecoveryStep`; the line-level `git diff <StampedAtCommit>..HEAD -- <paths>` at the CLI/recovery seam (`RunnerCommands` via `Hx.Runner.Core.Process`), lazy, with a worktree fallback when `StampedAtCommit` is null. (FR-002, H2/H3) <!-- doti-task-hash: 93a5b27a2f261744cd5cac000c28026e46fbe0e2915d5a6ee5a6c185e0bc3ef1 -->

## Phase 6 — The action model (workflow-affordance scope)

- [x] T012 `Hx.Cycle.Core/Actions/`: `CommandDescriptor`/`CommandKind`; `Applicability` (closed named-condition vocabulary `StageCurrent`/`CheckPassed`/`RecoveryTier`/`GateFailed`/`TrainValid`/`OutsideCycle`/`BugPhase` + `All`/`Any`) with `Evaluate(ctx)` AND `Describe()`; `CommandContext` (assembled once/turn from existing evaluators). (FR-010, D5/D6) <!-- doti-task-hash: f27c277bc2868f1c4d8c5ec442dd128159bed8ea36a9ede3de3dc49375459f30 -->
- [x] T013 `DotiActionModel` registry (stage-advance from `StageModel`; recovery descriptors WRAP `CycleRecoveryPlanner.Project` — tag each `StageRecoveryStep` with its descriptor id, never a 2nd evaluator; attest verb; publish-STOP `Command=null`; train-loop; bug-phase; the 7 `Utility` skills) + the pure `DotiActionProjector.Project`. (FR-010, D6/H6/B6) <!-- doti-task-hash: 16e36b89261c906c49288bd562f44d95f6da59d1d019f465adf77c0ec4e58aa7 -->
- [x] T014 `Hx.Runner.Cli/CliActionRendering.cs`: descriptor→`CliNextAction` mapping (kernel/`CliResults` stay domain-agnostic); route the WORKFLOW `nextActions` (cycle/gate/recovery) through it. Payload-templated sites (`ImpactCommands`/`Scaffold`) stay local (allow-listed). (FR-010, B4/B5) <!-- doti-task-hash: b3c477b15f05b1241eb7a3da23c72e9498d7e88fafa9a555af66aeedc07e5ae5 -->
- [x] T015 Registry static-invariant tests (one advance/stage; no overlap; no empty decision point) + the affordance-scoped "no hand-authored `CliNextAction`" guard; `Describe()==Evaluate` no-drift test. (SC-011) <!-- doti-task-hash: c8aa3ecee83c1acb817bd0bf6a0c58669f0cc07f187fdf0d25e65bfb70bc44db -->

## Phase 7 — Render migration (BEHIND the golden baseline)

- [x] T016 **Gating:** capture a byte-stable golden baseline of the CURRENT rendered skills + agent-context before any migration. (H7) <!-- doti-task-hash: d8c7684a12d7b72d2ca05bdd454ad08467c15a282f3daa8ee517bdd1f34e08cc -->
- [x] T017 `Hx.Doti.Core` (`+ Hx.Cycle.Core` ProjectReference — explicit, reviewed, acyclic): re-point `SkillMarkdownRenderer.ResolveSkillIdentity` + "Next stage:" + stage status/title, `DotiRenderer {commandAvailability}`, `DotiWorkflowDescribe` at the projector; delete `DotiWorkflowRegistry` (rehome its presentation); utility skills as descriptors; delete `skills.json nextStage`; agent-context `## Workflow Rules` prose → `{commandAvailability}`. Assert render-from-model == baseline per surface; `render-skills --check` + `payload check` green. (FR-010, B4/H1/H8) <!-- doti-task-hash: d3c81c72376f4f572eaa2a05d04c040e057186d49d849c26bd7c4d2638ee4d3a -->

## Phase 8 — Constitution + prompts

- [x] T018 FR-001: the §1 `Self-describing automation` invariant in `constitution-template.md` + this repo's constitution; re-render. <!-- doti-task-hash: f93a9470195c4de0bcdcede85658a56ad36f87d4ff02071b385d05f202d51ab0 -->
- [x] T019 FR-008: the anti-rubber-stamp reminder in the `/0N` + `doti-amend`/`doti-drift-fix` command templates; re-render. <!-- doti-task-hash: ebf0e678e3ae36115e0da5efda39e5aa6ad1940139744450e3dbed08fbea3c9f -->

## Phase 9 — Test matrix + proof

- [x] T020 Full matrix (SC-001..011): decay (own + across-rebase); bare-`Stamp`-throws + `Refresh`-no-bypass (SC-002); the `ReviewedRebinds` record + one-write (SC-007); the projector per-state + descriptor-id-matches-planner; the FR-009 cells (change-set-bound doc-only edit; spec+plan union). <!-- doti-task-hash: 3e6de8420e4689a72e9808a025d29f18c7689a79583d605b348f26acacb3b7e9 -->
- [x] T021 Verify green: `dotnet build -c Release` + `dotnet test`; `hx doti cycle check`; `hx doti payload check --repo .`; `hx gate run --profile normal`; confirm the gate-proof digest byte-unchanged for an unchanged diff. (SC-008) <!-- doti-task-hash: a941875cb01e81d08d260eb2acafa017aeb7ffd0e9a9e993218fd2dd86c4f563 -->

## Phase 10 — Mandatory documentation sweep (permanent final task)

- [x] T022 Update every doc the change touches so docs match code — `README.md`, `CHANGELOG.md`, the agent context (`CLAUDE.md`/`AGENTS.md`/`.doti/agent-context.md`), the affected rendered skills + `doti-amend`/`doti-drift-fix`, and `hx describe`/`--help` — then re-render installed assets. <!-- doti-task-hash: 88ed512580b7d3d0d202483d07152116a7565676891786e15a664ba2490a81c0 -->

## Acceptance mapping

FR-001→T018 · FR-002→T003,T011 · FR-003→T006,T008 · FR-004→T002,T005 · FR-005→T001,T006 · FR-006→T004,T006 · FR-007→T010 · FR-008→T019 · FR-009→T020 · FR-010→T012-T017 · SC-001..011→T002,T005,T015,T020,T021.
