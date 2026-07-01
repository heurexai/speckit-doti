# 039 — Tasks: Git Transaction Envelope

Ordered by workflow/tooling priority (fail-closed safety + deterministic proof first). Maps to [spec 039](../specs/039-git-transaction-envelope.md) FR/SC and [plan v2](../plans/039-git-transaction-envelope-plan.md). WI3 (pre-flight richness) is a fast-follow, not in this cycle.

## WI1 — transition-commit integrity (smallest, most urgent; no primitive)

- **T001** [FR-001] In `CommitStageTransition` (`CycleService.Transition.cs`), before `RunGitCommit`, stage the leaving stage's `current.Produces` (resolved for the feature) via the `SanctionedGitCommit` explicit-pathspec discipline (only that path; never `-A`). Skip when the stage declares no `produces`.
- **T002** [FR-002] Replace `allowEmpty: !HasStaged` with an orphan-only predicate: fail closed (a `CycleInputException` naming the resolved path) ONLY when the leaving stage declares a `produces` path that is absent-from-disk OR present-but-untracked-and-unstaged after T001 staging; a present-and-already-committed produces (unchanged, e.g. `clarify` re-declaring `specify`'s spec) commits normally (no empty-commit smell, no fail-close). Keep `RecoverStateIfNeeded` for the commit-failed path.
- **T003** [SC-001] Tests: (a) produced doc authored-but-untracked → non-empty transition commit containing it, zero orphans; (b) the `clarify→plan` unchanged-produces case → succeeds, no fail-close (guards the 031/#42 doc-dance); (c) a declared-produces that is genuinely absent → fail closed with the named path.

## WI2 — release transaction envelope (compensation ledger)

- **T010** [FR-030/FR-031, plan v2] New `Hx.Runner.Core/Git/ReleaseTransaction.cs`: an ordered compensation ledger — `Record(action, compensation)`, `Rollback(reason)` (best-effort-all, reverse order, `GitWithLockRetry` for git ops), `Commit()`. Plus `RollbackReport { FailedStage, Reason, IReadOnlyList<CompensationOutcome{Action,Succeeded,Detail}> }` and `CompensationOutcome`. Placement verified cycle-free (Runner.Core is a leaf both Cycle.Core + Scaffold.Core reference).
- **T011** [SC-002/SC-006, FR-031] Unit tests for `ReleaseTransaction` on a bare temp git repo (no dotnet/network): Record a create-tag + create-dir, force a failure, assert Rollback deletes the tag, removes the dir, runs compensations in reverse, and the RollbackReport lists outcomes; a compensation that itself fails is reported as a residual (not a clean revert).
- **T012** [FR-010/FR-011/FR-013, plan v2] Refactor `LocalReleaseService.Run` into an ordered `enum ReleaseStage { Validation, Tag, Pack, Smoke, Copy, Record }` pipeline wrapped by `ReleaseTransaction`; move `EnsureReleaseTag` inside the ledger and `Record(create-tag → git tag -d)` immediately; `Record(create-version-dir → restore-to-baseline)`; `Record(MarkReleaseTrainReleased → restore-prior cycle-state bytes)`. On any stage throw, `Rollback` + return a discriminated `LocalReleaseResult` carrying the `RollbackReport` + `Blockers` (no throw).
- **T013** [FR-014, SC-004] Tag-collision reconciliation in the ledger: same-source-commit = idempotent reuse; self-authored unreachable leftover (Doti trailers) = delete+recreate under the ledger; reachable/pushed = fail closed. Release-root baseline capture (existed? identity?) → restore-to-baseline, cross-volume via copy+delete (never `Directory.Move`).
- **T014** [FR-012, SC-002/SC-003] `InternalsVisibleTo` fault-hook (`Action<ReleaseStage>?`) so a test forces a throw at a chosen stage; failure-injection tests assert byte-identical revert (tracked `git write-tree` + tag/ref set + release-root `<version>` set, excluding gitignored) at each stage, on a temp git repo without dotnet; a post-snapshot file removed, a pre-snapshot preserved.
- **T015** [FR-030] Wire `RollbackReport` through `ScaffoldCommands.Release`/the CLI into the `CliResult` envelope (stage + compensation summary), new error code(s) for the reverted path.

## WI4 — lifecycle finalize (small; reuse existing recognition)

- **T020** [FR-032/FR-033, plan v2] A coded `finalize-release` (in `CycleService` + a `doti cycle finalize-release` verb): verify the release tag exists/reachable, then invoke the SAME `MarkReleaseTrainReleased` (no post-tag re-validation — mirror the 0.18.5 no-op guard); do NOT rebuild the released-feature recognition (`Stamp.cs:143-193`).
- **T021** [FR-033] Surface it via `CycleRecoveryPlanner`/`DotiActionProjector` so `doti cycle status`/`refresh-plan` offer it for a `release`-stage state where the tag exists but `ReleasedCycles` is empty (the CI-published wedge).
- **T022** [SC-007] Tests: a release-wedged `CycleState` fixture → `finalize-release` → the next `specify` stamps; `status`/`refresh-plan` surface the recovery.

## Cross-cutting

- **T030** Error-code registry entries (`errorcodes/registry.json` → regenerate `ErrorCodes.g.cs`) for the new VAL/ITG codes (transition orphan fail-close; release reverted).
- **T031** CHANGELOG `[Unreleased]` + README blockquote for `039-git-transaction-envelope` (the release-documentation gate needs the slug).
- **T032** `gate run --profile release` green (the release-lane proof) before drift-review.

## Coverage map
FR-001→T001; FR-002→T002; FR-010/011/013→T012; FR-012→T014; FR-014→T013; FR-030→T015; FR-031→T010; FR-032/033→T020/T021. SC-001→T003; SC-002/003→T014; SC-004→T013; SC-006→T011; SC-007→T022. (SC-005 is WI3 → fast-follow.)
