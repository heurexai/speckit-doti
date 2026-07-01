# 039 — Drift Review

Scope: the implementation diff for feature 039 (the analyze→implement transition `a8aa64f` + the cycle base). Three axes over what CHANGED this cycle.

## Axis 1 — spec↔code (every FR has a real enforcing mechanism, matching the arch-review-reshaped design)

- **FR-001** (transition stages its produces) → `CycleService.Transition.StageProducesArtifact` (scoped `git add`, never `-A`). Test `Transition_stagesAndCommitsTheLeavingStagesProducedDoc_evenWhenAuthoredUntracked`.
- **FR-002** (orphan-only fail-closed, not present-and-committed) → `EnsureProducesNotOrphaned`. Tests `Transition_doesNotFailClose_whenTheProducesIsPresentAndAlreadyCommittedUnchanged` + `Transition_failsClosed_whenTheLeavingStageDeclaresAProducesNeverAuthored`.
- **FR-010/011/013** (snapshot→all-or-revert, engine-owned, immediate) → `LocalReleaseService.Run` wraps the mutating stages in `ReleaseTransaction`; on any throw it `Rollback`s and returns `BuildRevertedResult` (no throw). Compensations for tag / release-root dir / cycle-state recorded BEFORE each mutation.
- **FR-012** (remove post-snapshot additions / restore) → the dir + cycle-state baselines (`CaptureDirBaseline`/`RestoreDirBaseline`, `CaptureCycleState`/`RestoreCycleState`). Tests in `ReleaseCompensationTests`.
- **FR-014** (retry-safe; tag inside the ledger) → tag creation moved inside the transaction with `delete created tag` compensation.
- **FR-030** (RollbackReport) → `RollbackReport`/`CompensationOutcome` in Contracts; `LocalReleaseResult.Rollback`; rendered by `ScaffoldCommands.Release` (`Integrity_ReleaseReverted` ITG0017).
- **FR-031** (coded, fail-closed, bounded; no whole-tree reset) → `ReleaseTransaction` compensates only its OWN recorded effects (never a repo reset — the arch-review BLOCKER-3 correction). Tests `ReleaseTransactionTests` (reverse order, best-effort-all, residual, commit-cleanups).
- **FR-032/033** (finalize the CI-publish wedge; no dead-end) → `CycleService.FinalizeReleasedCycle` + `hx doti cycle finalize-release` + `CanFinalizeReleasedCycle` surfaced in `doti cycle status`. Tests `FinalizeReleasedCycle_*`; verb smoke fails closed on a non-release repo.

No FR is downgraded enforced→advisory; no `*.Core` logic leaked into a `*.Cli`. The design matches plan v2 (compensation ledger in `Hx.Runner.Core/Git/`, not a whole-tree snapshot). WI3 (SC-005) was explicitly deferred to a fast-follow (recorded in the spec/tasks), not silently dropped.

## Axis 2 — code↔docs

- CHANGELOG `[Unreleased]` + README both carry a `039-git-transaction-envelope` entry describing WI1/WI2/WI4 and the `finalize-release` verb.
- New command `hx doti cycle finalize-release` carries its own `--help` (registered in `RunnerCommandFactory.Cycle.cs`); the reverted-release path surfaces via the standard `CliResult` envelope + the new error code.
- No public symbol was removed/renamed, so no stale doc reference to prune (grep of README/CHANGELOG for removed names = none).
- The `.doti/core` agent-context prose is high-level advisory and unchanged; the new command's behavior is documented in the CHANGELOG/README + its help. (Adding the transactional-release/finalize-release note to the single-sourced `.doti/core` advisory is a candidate polish, not a drift blocker — the command is fully self-describing.)

## Axis 3 — source↔installed

- No `.doti/core` source (skills.json, templates, workflows) changed this cycle → rendered skills / agent-context / entrypoints are unaffected. Confirmed by `doti render-skills --check` and `doti payload check --repo .` (see gate).
- New error code regenerated deterministically via `hx errorcodes render` (append-only; ITG0017).

## Gate

`gate run --profile normal` — see the persisted change-set-bound proof (green). Full solution suite (Release, uncapped) is 0-failure across all 12 projects; Runner 268 / Cycle 150 / Scaffold 131 include the +11 new 039 tests.

Verdict: no open drift in any applicable axis. Ready for `/09-release`.
