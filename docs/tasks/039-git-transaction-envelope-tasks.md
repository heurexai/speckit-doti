# 039 — Tasks: Git Transaction Envelope

Ordered by workflow/tooling priority (fail-closed safety + deterministic proof first). Maps to [spec 039](../specs/039-git-transaction-envelope.md) FR/SC and [plan v2](../plans/039-git-transaction-envelope-plan.md). WI3 (pre-flight richness, SC-005) is a fast-follow, not in this cycle.

## WI1 — transition-commit integrity (smallest, most urgent; no primitive)

- [x] T001 [P1] `CycleService.Transition.StageProducesArtifact`: before `RunGitCommit`, stage the leaving stage's declared `produces` path (scoped `git add`, never `-A`); skip when no `produces` or the file is absent. (FR-001) <!-- doti-task-hash: 14e9db793228cfb037e38b92fde80d36620af6b873c3389534ea7c05b5b1fe95 -->
- [x] T002 [P1] `EnsureProducesNotOrphaned`: fail closed (a `CycleInputException` naming the path) ONLY when the declared `produces` is neither staged now nor already tracked — a present-and-committed produces transitions normally (no fail-close on `clarify→plan`). (FR-002) <!-- doti-task-hash: 2fc4886a189c662e2507f8a2c9348dc384af9f3e663c0b0df36a19ab42814f91 -->
- [x] T003 [P1] Tests (SC-001): authored-untracked produces committed by the transition; same-produces re-declaration succeeds; genuine orphan fails closed with the named path. (SC-001) <!-- doti-task-hash: 4e8987240594d81c167e09757126dc05a3a9399de3103ec6a959639e78d9b280 -->

## WI2 — release transaction envelope (compensation ledger)

- [x] T010 [P2] New `Hx.Runner.Core/Git/ReleaseTransaction.cs`: ordered compensation ledger — `Record(action, undo, cleanup?)`, `Rollback` (reverse, best-effort-all, fail-closed `RollbackReport`), `Commit` (runs cleanups). Placement verified cycle-free (Runner.Core leaf). (FR-031) <!-- doti-task-hash: 06d7b89e6ac3048c4aaa98672610289c98a71664d30ad7b4996680d6771c6689 -->
- [x] T011 [P2] Unit tests for `ReleaseTransaction` on no git/dotnet: reverse-order compensations, best-effort-all with residual flag, commit runs cleanups not undos. (SC-002, SC-006) <!-- doti-task-hash: 3c5855d0881bcbdaad33f49a692387094917b2bf09053e1fc57fa725e806207a -->
- [x] T012 [P2] Refactor `LocalReleaseService.Run` into a `ReleaseStage` pipeline wrapped by `ReleaseTransaction`; tag creation inside the ledger; compensations for tag / release-root dir (restore-to-baseline) / cycle-state bytes recorded BEFORE each mutation; on any throw `Rollback` + return `BuildRevertedResult` (no throw). (FR-010, FR-011, FR-013) <!-- doti-task-hash: 241446ceb6fdc3d206daf5a3f691f15f3ba5d3e1e3435aa3c857a587fcc2078e -->
- [x] T013 [P2] Release-root baseline capture/restore (cross-volume via copy+delete); tag `Created`-guarded `git tag -d` compensation; cycle-state.json a first-class ledgered side effect (BLOCKER-4). (FR-014, SC-004) <!-- doti-task-hash: 4b1402e1b33744a9413847464578bf9dd2840720b26b86af8aa59203e3305a25 -->
- [x] T014 [P2] `[ThreadStatic]` `FaultHook` seam + byte-identical compensation-restore tests (`ReleaseCompensationTests`): dir removed/restored, cycle-state restored/deleted. (FR-012, SC-002, SC-003) <!-- doti-task-hash: 09ef4af1abf5a4c7f078c6c49ebb0fd274059e0da2694649e2b8c57d2a4ad1d9 -->
- [x] T015 [P2] `RollbackReport` in Contracts + `LocalReleaseResult.Rollback`; rendered by `ScaffoldCommands.Release` with new `Integrity_ReleaseReverted` (ITG0017). (FR-030) <!-- doti-task-hash: e7a24838e5203b34b6baaf457f5b051cf3ce2cfe87361570c509da27dbe841c7 -->

## WI4 — lifecycle finalize (small; reuse existing recognition)

- [x] T020 [P3] `CycleService.FinalizeReleasedCycle`: move the released feature into `ReleasedCycles` WITHOUT re-validating the shipped train; fail-closed unless at release-stage with a release tag; idempotent. (FR-032) <!-- doti-task-hash: 89db84be87744a751e61b054fb69c5967dac98c8fd42681dbc1989294fd6e719 -->
- [x] T021 [P3] `hx doti cycle finalize-release` verb + `CanFinalizeReleasedCycle` surfaced in `doti cycle status` nextActions. (FR-033) <!-- doti-task-hash: 31a7c3441c0393fd65d856314f4ff05cd6d4e398c5d9a7332f25aa274719602a -->
- [x] T022 [P3] Tests: fail-closed off release-stage; no-op with no cycle-state; verb registered + smoke. (SC-007) <!-- doti-task-hash: 5222753e8920a90242fe36fe0aaa21f6dc1bc7fc1a4ca2ac5f5fc2a0ba83a408 -->

## Cross-cutting

- [x] T030 [P4] Error-code registry entry `integrity/release-reverted` (ITG0017) regenerated via `hx errorcodes render`. <!-- doti-task-hash: 5a6759ee72f63ed59a2c747a68658e2e54161b4a3fccf287c3674037b78eb7f2 -->
- [x] T031 [P4] CHANGELOG `[Unreleased]` + README blockquote for `039-git-transaction-envelope`. <!-- doti-task-hash: be58ae641933acf266b267af286e9ee82b7e578a4d55831990c6bf1f7df882d2 -->
- [x] T032 [P4] Full solution suite (Release, uncapped) green (0 failures, 12 projects); the gate is run at drift-review. <!-- doti-task-hash: 6848c90c6f7893d526c05e04ece6ed0071dc926202f443ac6393949b7d3350df -->
