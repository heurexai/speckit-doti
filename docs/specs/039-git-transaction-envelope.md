# 039 — Git Transaction Envelope: engine-owned git-state integrity

## Goal

The doti engine must **own the integrity of every git-state mutation it performs** — the coded stage transitions and `hx release` — so that each operation either **completes fully, capturing exactly its intended change**, or **reverts to the exact pre-operation snapshot**, leaving the repository byte-identical to before. The engine never leaves an empty commit, an orphaned produced artifact, a leftover tag, a stale release directory, or a dirty tree; and it never relies on the agent to stage, clean up, or roll back. When something fails, the engine restores the snapshot **automatically and immediately** — before an agent can compound the mess by adding debug files — and then reports what happened.

This closes three observed manifestations of one root disease — *doti's coded git operations are incomplete and silent*:

1. **Empty transition commit / orphaned produced artifact.** A stage transition commits only what is already staged and silently allows an empty commit, so a produced spec/doc the agent authored but did not stage is left untracked and un-committed. Evidence: [`CycleService.Transition.cs:96`](../../tools/Hx.Cycle.Core/CycleService.Transition.cs) — `RunGitCommit(pending.FullMessage, allowEmpty: !readiness.CommitReadiness.Scope.HasStaged)`; the engine never `git add`s the transitioning stage's `produces` path. Observed live: a `/02-clarify` transition created an empty commit with `docs/specs/006-…md` still untracked.
2. **Non-atomic release leaves poisoning leftovers.** A `hx release` that fails *after* creating its tag and/or its release-root directory leaves both behind; the next run is then blocked or corrupted. Evidence (this session, ergon v0.1.2): a leftover `v0.1.2` tag made `BugReleaseGit.IsReleased` see the release-ready bugs as already-released → *"Release train is invalid: no completed unreleased feature cycles are ready for release"*; and a leftover `DOTI_RELEASE_ROOT/ergon/0.1.2` dir bound to a different source commit → *"Existing version release directory belongs to a different release identity"* (VAL0001). Both required manual `git tag -d` / `rm -rf`.
3. **Silent working-tree handling.** No coded doti operation surfaces pre-existing uncommitted/untracked changes for a decision; they are silently ignored and become orphans, or (in a naive commit) silently swept in. Operator: *"the agent should confirm what to do instead of by default just continue to ignore."*

## Priority Mode

**Workflow / tooling change** — ordered by **fail-closed safety + deterministic proof first, ergonomics last**. This is not product code; there is no MVP user-value slice. Work items are prioritised by how directly they protect repository integrity.

## Context and constraints (verified)

- Commits are owned by coded Doti workflow transitions and release paths, not by an agent-visible commit command; an untracked insurance pre-commit hook blocks bare commits (`PrecommitGuard`, sentinel `DOTI_SANCTIONED_COMMIT`). The fix must therefore live in the **engine**, using the existing sanctioned-commit mechanics, never an agent git command.
- An explicit-pathspec, touch-only commit discipline already exists (035/036): [`SanctionedGitCommit`](../../tools/Hx.Doti.Core/SanctionedGitCommit.cs) commits an explicit pathspec of only the paths a command staged and skips unstageable candidates — **never `git add -A`**. The transition and release paths must reuse this "only our own scoped paths" discipline.
- `hx release` is the **local** release: it validates config/intent/train, creates or verifies the annotated tag, packs the declared product, runs a source-free install smoke, records `LocalReleaseResult`/`release.identity.json`, and copies artifacts to `DOTI_RELEASE_ROOT`. It **does not push** — the remote `git push origin v*` is a separate `/09` operator step. The push is therefore the **irreversible boundary**; everything `hx release` does is local and reversible.
- The cycle-state is currently **wedged** at `031/release` with `completion:None`, `releasedCycles:0`, `completedUnreleasedCycles:0` — 031 shipped as 0.18.0 via **dev→main→CI**, but that publish path never ran the local release finalization, so the local state was left mid-`release`. [`CycleService.Transition.cs:34-38`](../../tools/Hx.Cycle.Core/CycleService.Transition.cs) only allows a new-feature `specify` from `drift-review`, and **no cycle verb finalizes a released cycle or recovers a `release`-wedged state** (`stamp/status/check/refresh-plan/refresh/review-rebind` — verified; the engine's own `nextActions` offer only bug/amend/converge/drift-fix, none of which advance it). So a released cycle is a **dead-end that blocks every future feature with no coded recovery** — the same lifecycle-integrity disease, at its most severe, and in scope (WI4). Bootstrapping THIS feature required a one-time manual reset of that stale, gitignored, local state (see Assumptions).

## Work items (prioritised: safety/proof → ergonomics)

### WI1 (P1) — Transition-commit integrity: never orphan a produced artifact, never empty-commit
The engine must capture the transitioning stage's declared produced artifact in the transition commit itself, and must fail closed rather than create an empty commit when a stage that declares a `produces` artifact has nothing to commit. This is the immediate live bug.

### WI2 (P2) — Release transaction envelope: snapshot → all-or-revert, engine-owned
`hx release` must run inside a transaction envelope: capture the exact pre-release repository state, perform all local release actions, and on **any** failure at **any** stage automatically restore that snapshot (delete the tag it created, remove the release-root/version dir it created, tear down any isolation worktree and temp tool-paths, undo any working-tree/index change) so the repo is byte-identical to pre-release — then inform the agent. A retry after a failed run starts clean (fixing manifestation 2). The push remains outside the envelope.

### WI3 (P3) — Pre-flight working-tree decision: confirm, never silently ignore
Before it snapshots and mutates, the engine must detect pre-existing uncommitted/untracked changes that are **not** the operation's own artifacts and require an explicit operator decision (include in this release / set aside / abort). It must never silently ignore them (orphaning) nor silently sweep them in.

### WI4 (P4) — Reporting + cycle-state lifecycle: clean, honest, and startable
On revert, the engine reports the failed stage, the reason, and exactly what was rolled back. The stamped cycle-state lifecycle must allow the next feature to start cleanly after a release (not throw "complete drift-review first" from a released state), so authoring the next spec is not blocked by prior residue.

## Scope

**In scope**
- The coded stage-transition commit ([`CycleService.Transition.cs`](../../tools/Hx.Cycle.Core/CycleService.Transition.cs) / `CommitStageTransition`): stage the produced artifact; fail closed on an empty produced-stage commit.
- `hx release` ([`LocalReleaseService`](../../src/Hx.Scaffold.Core/Release/LocalReleaseService.cs)) wrapped in a transaction envelope with automatic revert on any failure.
- A shared, coded **git transaction/snapshot primitive** in a `*.Core` type, reused by the transition and release paths.
- Pre-flight detection + operator-decision surfacing of unrelated uncommitted/untracked changes, reusing the 035/036 explicit-pathspec discipline to separate "our own paths" from operator changes.
- Reverting **release-external** artifacts too: the `DOTI_RELEASE_ROOT/<product>/<version>` dir and `latest` copy the run created.
- The cycle-state lifecycle fix that lets the next feature start after a release (WI4).

**Out of scope (explicitly)**
- The remote push (`git push origin v*`) and anything after it — once pushed, remote state is the irreversible commit point; the envelope covers only local, pre-push actions. A push failure is reported, not auto-reverted on the remote.
- Rewriting or force-pushing published history.
- The `/doti-bug` assess→fix→test records and the `hx doti update`/reconcile commit path beyond confirming they already honor the explicit-pathspec discipline (they were hardened in 035/036); they may adopt the shared primitive later but are not required to here.
- Reverting unrelated, regenerable gitignored build outputs (`bin/`, `obj/`, `.doti/cycle-state.json`, `.doti/gate-proof.json`) — see Assumptions.

## Functional Requirements

**WI1 — transition-commit integrity**
- **FR-001** [WI1]: A coded stage transition MUST stage the transitioning stage's declared produced artifact (its `produces` path / owned paths) itself, using the explicit-pathspec sanctioned-commit discipline (only those paths; never `git add -A`), so an authored-but-untracked produced doc is captured by the transition commit and never left untracked.
- **FR-002** [WI1]: A stage transition MUST NOT create an empty commit for a stage that declares a `produces` artifact. If that artifact is absent or empty at transition time, the engine MUST fail closed with a diagnostic naming the missing artifact, instead of silently committing nothing (the `allowEmpty: !HasStaged` path must be removed or narrowed to stages that legitimately produce no tracked artifact).
- **FR-003** [WI1]: After a successful transition, the working tree MUST contain zero orphaned produced artifacts for the transitioned stage — the produced artifact is committed and `doti cycle status`/`check` reflect it as committed, not as an untracked change.

**WI2 — release transaction envelope**
- **FR-010** [WI2]: Before its first mutating action, `hx release` MUST capture a snapshot of the repository sufficient to restore it exactly: HEAD and relevant refs (tags), the index, and the working-tree state (tracked and untracked content), plus a baseline of the release-root/version directory it would write.
- **FR-011** [WI2]: If any release stage fails — config/manifest validation, release-intent validation, release-train validation, tag creation, pack, install smoke, artifact copy, or result recording — the engine MUST automatically revert every side effect performed in that run (the created tag; the release-root/version dir + `latest` copy + `release.identity.json`; any isolation worktree; any temp tool-path; any working-tree/index change) and restore the captured snapshot, with **no agent action required**.
- **FR-012** [WI2]: The revert MUST remove anything added to the working tree **after** the snapshot (including files an agent created while debugging a failed release) and restore anything the release changed or removed, so a failed release leaves the repo byte-identical to the pre-release snapshot.
- **FR-013** [WI2]: The revert MUST run **automatically and immediately** on failure (the engine restores the clean snapshot before returning), so no half-released dirty state is ever presented to the agent to "recover" from.
- **FR-014** [WI2]: A release MUST be safe against leftovers from its OWN prior failed runs — because a failed run now reverts its tag and release-root dir, a retry starts from a clean snapshot; a pre-existing tag/dir from an interrupted earlier run MUST NOT block or corrupt a fresh run.
- **FR-015** [WI2]: The transaction boundary ends at the local release. The engine MUST NOT push as part of the release, MUST leave the successful tag/artifacts in place on success, and MUST surface the separate `git push origin v*` publish step to the operator.

**WI3 — pre-flight working-tree decision**
- **FR-020** [WI3]: Before snapshotting and mutating, the engine MUST detect pre-existing uncommitted or untracked changes that are not the operation's own artifacts and MUST require an explicit operator decision (include in this release / set aside / abort). It MUST NOT silently ignore them (orphaning) nor silently include them.
- **FR-021** [WI3]: The engine MUST distinguish the operation's OWN paths (what it creates/touches) from pre-existing operator/agent changes, and only ever stage/commit its own scoped paths (reusing the 035/036 explicit-pathspec discipline).

**WI4 — reporting + lifecycle**
- **FR-030** [WI4]: On a reverted release, the engine MUST report the failed stage, the reason, and a summary of exactly what was rolled back (tag removed, dir removed, tree restored) via the standard `CliResult`/error-code envelope.
- **FR-031** [WI4]: The snapshot/revert MUST be a coded, fail-closed, deterministic engine primitive shared by the transition and release paths — never delegated to agent-run git commands — and MUST refuse to revert past the pre-operation snapshot (bounded rollback; no rewriting published history / force-push).
- **FR-032** [WI4]: A released feature cycle MUST finalize its local cycle-state cleanly — including on the **dev→main→CI publish path**, which today leaves the local state wedged mid-`release` — so the next feature's `specify` can start without residue.
- **FR-033** [WI4]: The engine MUST guarantee that **no cycle-state is ever a dead-end**: from any reachable state (including a `release`-wedged one) there MUST be a sanctioned coded recovery/forward path surfaced by `doti cycle status`/`refresh-plan`. A cycle that can only be unblocked by hand-editing gitignored local state is a fail-closed defect.

## Success Criteria

- **SC-001**: A stage transition on a repo where the produced spec/doc is authored-but-untracked yields a **non-empty** commit that **includes** the produced artifact; afterward the working tree has **zero** orphaned produced artifacts and **zero** empty produced-stage transition commits.
- **SC-002**: A release injected to fail at each stage (validation, tag, pack, smoke, copy, record) leaves the repo **byte-identical** to the pre-release snapshot in **every** case — verified by comparing `git status`, working-tree tree-hash, and the ref/tag list before vs after; the failed tag and release-root dir are gone in all cases (100% revert fidelity).
- **SC-003**: Files added to the working tree **after** the snapshot (simulating agent debug output) are removed by the revert; uncommitted changes present **at** snapshot time are preserved intact.
- **SC-004**: A release retried immediately after a failed run — with that run's tag/dir now reverted — succeeds with **no** manual cleanup (the ergon-v0.1.2 leftover scenario passes end-to-end).
- **SC-005**: A release started with unrelated pre-existing uncommitted changes **fails closed** with an operator-facing decision surfacing those changes by path; it never silently includes or orphans them.
- **SC-006**: Every cleanliness guarantee above is achieved by engine code only — no happy-path or failure-path test requires an agent-issued `git add`/`reset`/`clean`/`tag -d` to reach a clean state.
- **SC-007**: After a released cycle (including one published via dev→main→CI), stamping the next feature's `specify` succeeds with no manual state reset — and `doti cycle status`/`refresh-plan` surface a sanctioned recovery for a `release`-wedged state (no dead-end).

## Key entities / data

- **RepoSnapshot** — an immutable capture of {HEAD, tag refs, index tree-id, working-tree state (tracked + untracked, e.g. a dedicated stash/tree ref), release-root baseline} taken before the first mutation; the single source the revert restores to.
- **ReleaseTransaction / TransactionEnvelope** — wraps an operation: holds the `RepoSnapshot` and an ordered ledger of performed side effects, each with its compensating action; `Commit()` (keep results, drop the snapshot) or `Rollback()` (restore the snapshot, run compensations in reverse).
- **WorkingTreeDelta** — the classified pre-flight diff separating the operation's own paths from unrelated operator/agent changes (drives FR-020/FR-021).

## Deterministic surfaces

- `hx release` — transactional; `LocalReleaseResult` records the snapshot id and, on failure, the revert outcome (JSON proof).
- The coded transition commit — stages produced artifacts and fails closed on an empty produced-stage commit.
- A new `*.Core` git-transaction/snapshot primitive (single responsibility; CLI stays parse→delegate→render).
- (Planned-but-absent, mark advisory) any new operator-facing sub-verb (e.g. a status of the last transaction) is advisory until authored.

## Architecture / Sentrux / hygiene impact

- New `*.Core` type(s) for the snapshot/transaction primitive — small, single-responsibility, one concern per file; composed into `CycleService` (transition) and `LocalReleaseService` (release), not inlined. Keep functions within the Sentrux size limit.
- Reuses `SanctionedGitCommit`'s explicit-pathspec discipline; no new `git add -A` anywhere.
- No new cross-layer dependency: the primitive lives where both consumers can reference it without cycles (candidate: `Hx.Doti.Core` alongside `SanctionedGitCommit`, or a dedicated git-ops module); the arch-review lens confirms the boundary.
- Change-set-bound gate proof and the fail-closed chokepoints are preserved; nothing is downgraded enforced→advisory.

## Assumptions

- **Irreversible boundary = push.** The envelope covers only local, pre-push actions; once `git push` publishes a tag, remote state is not auto-reverted (only reported). Rationale: the `/09` model already separates the local `hx release` from the operator push, and safely auto-reverting a remote is out of scope and risky.
- **Snapshot preserves pre-existing work; revert removes only post-snapshot additions.** The snapshot captures any pre-existing uncommitted/untracked work, so a revert restores exactly that; only changes made *after* the snapshot (a failed release's own edits, agent debug files) are undone. The operator's in-progress work present at release start is never destroyed — this is why WI3 confirms it first.
- **Gitignored, regenerable outputs are not part of the reverted logical state.** `bin/`, `obj/`, `.doti/cycle-state.json`, `.doti/gate-proof.json` are regenerable and gitignored; the snapshot/revert operates on the tracked surface + the release's own artifacts + the release-root dir. (If a future need arises to also pin these, that is an extension, not this feature.)
- **Worktree isolation is the expected mechanism for the release** (per the operator's suggestion): running the release in a throwaway worktree keeps the operator's main working tree untouched, so a concurrent edit in the main tree is never at risk, and the revert is "delete the worktree + compensate the shared tag/dir." The exact snapshot/revert mechanism (worktree vs stash+reset vs a reserved ref) is a `/03-plan` decision; the spec fixes the **outcome** (exact revert), not the mechanism.
- **WI1's core is stage-produced-artifact + fail-closed**, not a full transactional transition. The shared snapshot primitive MAY later wrap transitions too, but the immediate transition fix is "stage the produced artifact and never empty-commit"; a full transactional transition is a possible extension.
- **Pre-flight decision default = fail-closed + surface.** When unrelated uncommitted changes are present, the engine fails closed and surfaces them for the operator to resolve (commit into the release or set aside) before proceeding; a richer "interactively include selected paths" flow is a possible `/02-clarify` refinement, not assumed here.
- **Feature and bug numbers share one monotonic sequence** (026–031 features, 032–038 bugs); this feature is 039.
- **Bootstrap (one-time).** This feature's own `specify` could not be stamped while the local cycle-state was wedged at `031/release` (FR-032/FR-033 evidence). Because that state is gitignored, local-only, and belongs to a cycle already shipped as 0.18.0 with empty train records (nothing of value to lose), it was reset once to start the 039 cycle. This manual step is exactly what FR-033 makes unnecessary going forward; it is not a sanctioned pattern, and no committed artifact was hand-edited.

## Dependencies

- Existing `SanctionedGitCommit` explicit-pathspec discipline (035/036) — the reuse target for scoped staging.
- The `LocalReleaseService` release stages and `CycleService` transition path — the two consumers wrapped by the envelope.
- GitVersion / tag creation (`EnsureReleaseTag`) — the tag is a compensable side effect.

## Command availability

Deterministic gate proof, `hx release`, `doti cycle stamp/status/check`, `gate run`, and the sanctioned-commit mechanics are available and own this behavior. Any new operator-facing sub-verb is planned-but-absent and marked advisory until authored. This spec claims no gate proof it has not produced.
