# 039 — Implementation Plan: Git Transaction Envelope

Spec: [docs/specs/039-git-transaction-envelope.md](../specs/039-git-transaction-envelope.md). Priority mode: workflow/tooling (fail-closed safety + deterministic proof first).

## Design summary

Introduce one coded **git-transaction primitive** that both the cycle-transition path and the release path use to make their git-state mutations **all-or-nothing**, and fix the transition commit to **capture the produced artifact itself** instead of relying on pre-staging. The engine — never the agent — snapshots, commits-or-reverts, and reports.

Three consumers, one primitive:
- **Transition commit** (WI1) — stage the stage's own produced artifact, commit it, fail closed instead of empty-committing.
- **Release** (WI2/WI3) — run inside a transaction envelope with worktree isolation + a compensation ledger; auto-revert on any failure; pre-flight the dirty tree.
- **Lifecycle** (WI4) — finalize a released cycle and give a `release`-wedged state a coded recovery.

## Architecture assessment (existing patterns to fit)

- **Scoped sanctioned commit** already exists: [`SanctionedGitCommit`](../../tools/Hx.Doti.Core/SanctionedGitCommit.cs) stages an explicit pathspec (`AtOrUnder`, never `git add -A`) and skips unstageable candidates. The transition-staging fix and the pre-flight "our own paths vs operator changes" split reuse this discipline — do not reinvent it.
- **Git primitives** live in the runner layer: `GitRefs` (`TryHeadSha`/`ResolveBaseRef`), `ProcessRunner`, `CommitScopeInspector`. The new primitive belongs beside them so BOTH `Hx.Cycle.Core` (transition) and `Hx.Scaffold.Core` (release) can reference it without a new cycle. Candidate module: the runner/git-ops layer that already backs both (verified: `CycleService` uses `GitRefs`/`ProcessRunner`; `LocalReleaseService` uses `ProcessRunner`/`EnsureReleaseTag`). Arch-review confirms the exact placement + the ArchUnit boundary.
- **Release mutating stages (verified order)** — `LocalReleaseService.Run`: (1) `EnsureReleaseTag` (line 65) → local annotated tag; (2) `dotnet pack` → `tempRoot`; (3) source-free install smoke → temp tool-path; (4) copy to `releaseRoot/<version>` + `latest` + `release.identity.json` (lines 196-198); (5) `cycle.MarkReleaseTrainReleased()` (line 201). Only (1) the tag and (4) the release-root dir are durable/shared and poison retries; (2)(3) are temp.
- **Transition commit (verified)** — `CommitStageTransition` ([`CycleService.Transition.cs:96`](../../tools/Hx.Cycle.Core/CycleService.Transition.cs)) → `RunGitCommit(msg, allowEmpty: !readiness.CommitReadiness.Scope.HasStaged)`; the engine never stages the stage's `produces` path, so an untracked produced doc → empty commit + orphan.

## The primitive

`GitTransaction` (new `*.Core` type in the git-ops layer). Not inlined; a small single-responsibility type composed into both consumers.

- **Begin(repoRoot, options)** → captures a `RepoSnapshot`:
  - `HeadSha`, the set of local **tag refs** (name→object), the **index tree-id**, and a capture of the **working tree** (tracked + untracked) sufficient to restore — via a reserved snapshot ref/tree (e.g. write-tree of a temporary index that includes untracked), not `git stash` (stash mutates the tree; a reserved objects-only snapshot does not).
  - Optional `ReleaseRootBaseline` (the `DOTI_RELEASE_ROOT/<product>/<version>` dir: did it pre-exist? its identity) so revert can remove only a dir this run created.
- **Record(sideEffect, compensation)** — append to an ordered ledger (create-tag → delete-tag; create-dir → remove-dir; write-cycle-state → restore-prior; add-worktree → remove-worktree; temp → delete).
- **Rollback(reason)** — run compensations in **reverse**, then hard-restore the working tree + index + refs to the snapshot (removing post-snapshot untracked additions, including agent debug files; restoring anything changed/removed). Bounded: never resets past the snapshot, never rewrites pushed history, never force-pushes. Returns a `RollbackReport`.
- **Commit()** — drop the snapshot + temp holders, keep the durable results (tag, release-root artifacts).

Idempotent + fail-closed: a `Begin` on an already-dirty tree defers to the WI3 pre-flight (below) before capturing.

## Per-work-item approach

### WI1 — transition-commit integrity
- In `CommitStageTransition`, before committing, stage the transitioning stage's declared produced artifact via the `SanctionedGitCommit` explicit-pathspec discipline: resolve `current.Produces` (+ the stage's owned paths) and stage exactly those (never `-A`), so the coded transition captures the produced doc the agent authored.
- Replace `allowEmpty: !HasStaged` with fail-closed logic: a stage that **declares a `produces` artifact** MUST have a non-empty commit that includes it — if the artifact is absent/empty, throw a clear `CycleInputException` naming it (routed to a VAL error code) rather than silently empty-committing. A stage that declares **no** produces artifact (only bookkeeping) may still commit its scoped changes; a truly-empty transition for such a stage is the only case `allowEmpty` remains, and it is narrowed to exactly that.
- Wrap the transition in `GitTransaction` so a failed transition (e.g. the git commit fails midway) reverts the partial cycle-state write + any partial stage, instead of the current best-effort `RecoverStateIfNeeded`.

### WI2 — release transaction envelope (+ worktree isolation)
- `LocalReleaseService.Run` opens a `GitTransaction` before `EnsureReleaseTag`. It runs pack + smoke inside a **throwaway worktree** at the release commit (isolating all file ops from the operator's main tree — a concurrent edit is never at risk; this is the operator's suggested mechanism). Each durable side effect (tag, release-root `<version>` dir + `latest`, `release.identity.json`, `MarkReleaseTrainReleased`) is `Record`ed with its compensation.
- On **any** stage failure the engine calls `Rollback` immediately and returns a failed `LocalReleaseResult` carrying the `RollbackReport` — no half-released state is ever handed back (FR-013). On success, `Commit()` keeps the tag + artifacts and tears down the worktree/temp.
- Retry safety (FR-014): because a failed run now reverts its tag + release-root dir, the ergon-v0.1.2 leftovers cannot recur; additionally, a pre-existing leftover from an *interrupted* earlier run is detected at `Begin` and offered for reconciliation rather than a hard wall.
- The push stays outside (`Commit` leaves the tag; the `/09` operator step pushes). A push is never part of the transaction.

### WI3 — pre-flight working-tree decision
- Before `Begin` captures, classify the working tree with a `WorkingTreeDelta` (reuse `CommitScopeInspector` + the `AtOrUnder` owned-path split): separate the operation's own paths from unrelated uncommitted/untracked changes. If unrelated changes exist, **fail closed** with a `CliResult` that names them by path and states the decision (commit into the release / set aside / abort) — never silently ignore or sweep them. (Default policy per spec Assumptions; a richer interactive include-flow is a possible follow-up, not built here.)

### WI4 — lifecycle finalize + no dead-end
- Make the released-cycle finalization reachable off the CI publish path (or make the local `release`-stage state finalizable): after a release tag exists and the train is marked released, the cycle records completion so the next `specify` starts cleanly (fixing the 031 wedge class).
- Give a `release`-wedged state a coded recovery surfaced by `doti cycle status`/`refresh-plan` (e.g. a finalize/complete transition), so no state is a dead-end (FR-033). Bounded scope: the recovery finalizes a genuinely-released cycle; it does not fabricate history.

## Decision / Rationale / Alternatives rejected

- **Decision:** one `GitTransaction` primitive in the git-ops layer, consumed by transition + release; worktree isolation for the release; scoped staging (reuse `SanctionedGitCommit`) for the transition; fail-closed instead of silent-empty everywhere.
- **Rationale:** a single primitive keeps the two consumers from drifting (the 035/036 lesson: two commit paths drifted until unified). Worktree isolation is the cleanest guarantee the operator's main tree is never touched. Reusing `SanctionedGitCommit` keeps the "only our own paths, never `-A`" invariant in one place.
- **Alternative rejected — `git stash` for the snapshot:** stash mutates the working tree and interacts badly with untracked/ignored files and concurrent ops; an objects-only reserved-ref snapshot restores exactly without mutating mid-capture.
- **Alternative rejected — per-consumer bespoke rollback (no shared primitive):** reproduces the drift the 035/036 history warns against; every future release/transition mutation would re-implement compensation.
- **Alternative rejected — reverting on the remote (push inside the envelope):** a pushed tag is the irreversible boundary; auto-deleting a remote tag is unsafe and out of scope. Push stays an operator step.
- **Alternative rejected — leave the transition relying on the agent to stage (just fail-closed on empty):** violates the operator directive "do not rely on the agent"; the engine must stage the produced artifact.

## Architecture / Sentrux / ArchUnit delta

- New `GitTransaction` + `RepoSnapshot` + `RollbackReport` records in the git-ops `*.Core` layer; `WorkingTreeDelta` beside `CommitScopeInspector`. Small, single-responsibility; composed into `CycleService` and `LocalReleaseService`, not inlined; functions within the Sentrux size limit.
- No new cross-layer cycle: the primitive sits below both consumers (same layer as `GitRefs`/`ProcessRunner`). ArchUnit `cliDelegation`/`cliSurfaceConfinement` unaffected (CLI stays parse→delegate→render). Sentrux boundaries: the new type is production code under the measured surface.
- No enforced check is downgraded; the gate proof stays change-set-bound; the pre-commit insurance hook and `doti cycle check` chokepoints are untouched (the transition still commits via the sanctioned sentinel).

## Testing approach (proof per SC)

- SC-001: a transition test where the produced doc is authored-but-untracked → assert a non-empty commit containing it + zero orphans (the exact 006/live bug).
- SC-002/003: an injected-failure harness that forces each release stage to throw → assert the repo (tree-hash + ref list + release-root) is byte-identical to the snapshot and that a post-snapshot untracked file is removed while a pre-snapshot one is preserved.
- SC-004: release → forced fail → retry-succeeds with no manual cleanup (the ergon scenario as a fixture).
- SC-005: release with an unrelated uncommitted change → fail-closed with the path surfaced.
- SC-006: assert no test reaches cleanliness via an agent git command.
- SC-007: after a released cycle, `specify` for the next feature stamps; `status`/`refresh-plan` surface a recovery for a wedged state.

## Risks

- Compensation ordering: a compensation may itself fail (a `git tag -d` lock race, a dir removal); the ledger runs best-effort-all in reverse and reports residuals fail-closed (never a clean-revert claim with a leftover).
- Scope discipline: the revert compensates ONLY the operation's own recorded side effects — never a blanket tree reset — so unrelated gitignored build outputs and concurrent operator edits are untouched by construction.

---

## Revised design (v2, post-arch-review — AUTHORITATIVE, supersedes the sections above where they conflict)

The `/04` review ([039 arch-review](../reviews/039-git-transaction-envelope-arch-review.md)) found the v1 mechanism over-engineered and, in two places, unsafe. Corrected design (operator-concurred: drop the worktree/whole-tree snapshot; keep engine-owned code rollback on a failed release):

- **No whole-repo snapshot, no worktree-for-isolation.** Verified: the release makes ZERO tracked-working-tree mutations (packs to a temp dir `LocalReleaseService.cs:175`; release-root required outside the repo `:820-829`; the tag is a shared `.git` ref). A blanket snapshot/reset would clobber concurrent operator edits and cannot preserve the exec-bit on Windows (`core.fileMode=false`) — both unacceptable, both unnecessary.

- **The primitive is a small `ReleaseTransaction`/compensation ledger in `Hx.Runner.Core/Git/`** (the true leaf git-ops layer, beside the existing `GitWorktree`; both `Hx.Cycle.Core` and `Hx.Scaffold.Core` already reference `Hx.Runner.Core`, so no project cycle — the v1 "`Hx.Doti.Core`" placement would have cycled, and `GitRefs`/`CommitScopeInspector` are actually in `Hx.Cycle.Core`). It holds an ordered ledger of `{ sideEffect, compensation }`; `Rollback` runs compensations in reverse **best-effort-all** and returns a fail-closed `RollbackReport`; `Commit` keeps the durable results.

- **WI2 (release), the ledger's exact entries:** `created-tag → git tag -d` (with `GitWithLockRetry`); `created-version-dir → restore-to-baseline` (capture the `<version>` dir's pre-run identity/bytes; restore, or remove only if the baseline was absent; cross-volume via copy+delete, never `Directory.Move`); `MarkReleaseTrainReleased → restore-prior cycle-state bytes` (cycle-state.json is a FIRST-CLASS ledgered side effect — the release mutates it mid-run, so excluding it would leave a released-with-no-tag state worse than the 031 wedge). Tag creation moves INSIDE the ledger (v1 had `EnsureReleaseTag` at `Run:65`, outside any envelope); its collision path becomes concrete: same-source-commit = idempotent reuse; a self-authored unreachable leftover = delete+recreate under the ledger; a reachable/pushed tag = fail closed.

- **WI2 testability seams (design-level, needed before tasks):** model the release as an ordered `enum ReleaseStage { Validation, Tag, Pack, Smoke, Copy, Record }`; each step is wrapped by the ledger; an `InternalsVisibleTo` fault-hook lets a test force a throw at a chosen stage on a plain temp git repo (no dotnet/network), asserting byte-identical revert. `Run` returns a discriminated `LocalReleaseResult` carrying a nullable `RollbackReport { FailedStage, Reason, IReadOnlyList<CompensationOutcome{Action,Succeeded,Detail}> }` + `Blockers` on the reverted path (no throw), so the CLI renders structure, not a flattened exception string.

- **WI1 (transition) stays OUT of the ledger** (blast radius: don't put an unproven primitive in every cycle stamp). Only two changes: (1) stage the leaving stage's `current.Produces` via the `SanctionedGitCommit` explicit pathspec before `RunGitCommit`; (2) narrow `allowEmpty: !HasStaged` to fail closed ONLY when the declared produces path is absent/orphaned (untracked-and-unstaged or missing) — NEVER when present-and-already-committed (the `clarify→plan` case, where clarify re-declares specify's committed spec). Keep `RecoverStateIfNeeded` for the commit-failed path.

- **WI3 (pre-flight)** reuses `CommitScopeInspector.Inspect` + `FeatureArtifactScope.OwnedPaths` (the own-vs-foreign split already exists) to fail-closed-surface unrelated dirty paths — no new classifier. (Spun to a fast-follow per scope; the shared helper factoring is noted.)

- **WI4 (lifecycle)** is small: the released-feature recognition ALREADY exists (`ResolveExistingForStamp`/`Stamp.cs:143-193`); the only gap is that dev→main→CI never calls `MarkReleaseTrainReleased` (so `ReleasedCycles` stays empty → 031 wedged). Add a coded `finalize-release` that verifies the release tag exists/reachable then invokes the SAME `MarkReleaseTrainReleased` (no post-tag re-validation — mirror the 0.18.5 no-op guard), surfaced via `CycleRecoveryPlanner` so `status`/`refresh-plan` offer it. Do NOT rebuild the recognition.

- **SC oracles pinned:** byte-identical = tracked `git write-tree` (clean index) + the tag/ref set + the release-root `<version>` dir set, EXPLICITLY excluding gitignored paths; exec-bit/mode fidelity checked on the 3-OS matrix. SC-006 reframed as "Rollback leaves the repo clean, asserted via a read-only `CommitScopeInspector` with no subsequent git mutation" (deterministic).
