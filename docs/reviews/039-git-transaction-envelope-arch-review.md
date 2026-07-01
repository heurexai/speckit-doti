# 039 — Architecture Review: Git Transaction Envelope

Multi-lens review (design-soundness/fit, failure-modes, blast-radius/simpler, testability) of [spec 039](../specs/039-git-transaction-envelope.md) + [plan 039](../plans/039-git-transaction-envelope-plan.md) against the real code. **Verdict: the DIRECTION is right (engine-owned, fail-closed, closes the three real defects) but the planned MECHANISM is over-engineered and, in two places, unsafe. The plan must be revised before `/05-tasks`.** Open BLOCKERs → `/07-implement` is blocked until resolved.

## Findings

### BLOCKER-1 — Primitive placement would not compile
The plan places `GitTransaction` in `Hx.Doti.Core` ("alongside `SanctionedGitCommit`"), but `Hx.Doti.Core → Hx.Cycle.Core` (`Hx.Doti.Core.csproj:13`), and the transition consumer is IN `Hx.Cycle.Core` — so `Hx.Cycle.Core → Hx.Doti.Core` would be a project-reference cycle. The plan also wrongly claims `GitRefs`/`CommitScopeInspector` "live in the runner layer" — they are in `Hx.Cycle.Core` (`GitRefs.cs`, `CommitScopeInspector.cs`); only `ProcessRunner` is in `Hx.Runner.Core`.
**Resolution:** place `GitTransaction`/compensation-ledger in `Hx.Runner.Core/Git/` (the true leaf, beside the **existing** `GitWorktree`). Both `Hx.Cycle.Core` and `Hx.Scaffold.Core` already reference `Hx.Runner.Core` → zero new edges, no cycle.

### BLOCKER-2 — FR-002 would fail-close a legitimate `clarify→plan`
`specify` and `clarify` declare the **same** `produces` (`docs/specs/{feature}.md`, `workflow.yml:14`+`:20`); the spec is committed by the `specify→clarify` transition, so `clarify→plan` (current=clarify) has nothing new for its declared produces. FR-002 "fail closed on an empty produced-stage commit" would hard-error a routine stamp.
**Resolution:** narrow the fail-closed predicate to: the leaving stage declares a `produces` path AND that path is **absent/orphaned or has staged-uncommitted content that would be lost** — **never** when it is present-and-already-committed. Add a `clarify→plan` (spec unchanged) test so the 031/#42 doc-dance fix cannot regress.

### BLOCKER-3 — the whole-tree snapshot/restore is unsafe (clobbers concurrent work) and unnecessary
The release makes **zero tracked-working-tree mutations**: pack/publish write to a temp dir (`LocalReleaseService.cs:175/:394/:541`), the release root is required OUTSIDE the repo (`:820-829`), and the tag is a shared `.git` ref (a worktree shares `.git`, so isolates nothing). A blanket `git reset --hard`+`git clean` to a snapshot would **destroy the operator's concurrent main-tree edits** — the opposite of the safety goal — and a write-tree snapshot **cannot preserve the exec-bit** on the Windows host (`core.fileMode=false` + `File.Copy` restore), silently breaking restored shell hooks on Unix (the 032 3-OS trap).
**Resolution:** **drop the whole-repo snapshot and the worktree-for-isolation.** Replace with a small **compensation ledger** of the operation's OWN durable side effects: `created-tag → git tag -d`; `created-version-dir → restore-to-baseline`; `cycle-state write → restore-prior-bytes`. On failure, run compensations (best-effort-all, reverse order); never touch unrelated tree state. This meets SC-002/003/004/012 with **no** tracked-tree restore code.

### BLOCKER-4 — cycle-state.json must be a first-class ledgered side effect
The release mutates `.doti/cycle-state.json` mid-run (`MarkReleaseTrainReleased`, `:201`). The plan excludes it (gitignored) — so a post-mark failure leaves it advanced with no tag: a released-with-no-tag dead-end **worse than the 031 wedge**.
**Resolution:** snapshot its exact bytes at `Begin`, `Record(mark → restore-prior-bytes)` around `MarkReleaseTrainReleased`. (Keep `bin/obj`/`gate-proof.json` out — those are regenerable; cycle-state is authoritative.)

### BLOCKER-5 — the tag is created outside any envelope, and its collision path hard-throws
`EnsureReleaseTag` runs at `Run:65`, before the `try/finally` — so on the plan's design the tag is uncompensated; and a pre-existing tag HARD-THROWS, not "offered for reconciliation" (FR-014/SC-004 unbacked).
**Resolution:** open the ledger at the top of `Run`; `Record(create-tag → delete-tag)` the instant the tag is created. Make reconciliation concrete: same-source-commit = idempotent reuse; a self-authored leftover (Doti trailers, unreachable) = delete+recreate under the ledger; a reachable/pushed tag = fail closed with the operator decision.

### BLOCKER-6 (testability) — no failure-injection seam for SC-002
`LocalReleaseService.Run` has no seam to force a failure at stage N (and every real stage needs dotnet+network).
**Resolution:** model the release as an ordered `enum ReleaseStage { Validation, Tag, Pack, Smoke, Copy, Record }`, each a step the ledger wraps, with an `InternalsVisibleTo` fault-hook so a test throws at a chosen stage on a plain temp git repo — no dotnet.

### BLOCKER-7 (testability) — RollbackReport contract undefined + incompatible with throw-based `Run`
`Run` returns `LocalReleaseResult` on success and throws on failure; the CLI flattens exceptions to a string — so FR-013/FR-030 "inform the agent what was rolled back" has nowhere to land.
**Resolution:** `Run` returns a discriminated result carrying `RollbackReport { FailedStage, Reason, IReadOnlyList<CompensationOutcome{Action,Succeeded,Detail}> }` + `Blockers` on the reverted path (no throw); the CLI renders the structure.

### HIGH-1 — blast radius: do NOT wrap the transition in the transaction
WI1 needs only two changes (stage `produces` via the `SanctionedGitCommit` pathspec discipline; fail-closed per BLOCKER-2). Putting an unproven primitive in every cycle stamp's path is a blast radius no SC requires — keep `RecoverStateIfNeeded` for the commit-failed path; the ledger is release-only.

### HIGH-2 — WI4 mostly exists; scope it to the CI-publish gap
`ResolveExistingForStamp`/`Stamp.cs:143-193` already recognize a released feature and let the next `specify` start. The 031 wedge is ONLY because dev→main→CI never calls `MarkReleaseTrainReleased` (so `ReleasedCycles` is empty). The spec's "no coded recovery exists" is overstated.
**Resolution:** WI4 = a coded `finalize-release` (verify the tag exists/reachable, then call the SAME `MarkReleaseTrainReleased`, no post-tag re-validation — mirror the 0.18.5 no-op guard), surfaced by `CycleRecoveryPlanner` so `status`/`refresh-plan` offer it. Correct the spec framing.

### MEDIUM — reuse existing machinery; pin the SC oracles
- WI3's own-vs-foreign split already exists via `CommitScopeInspector.Inspect` + `FeatureArtifactScope.OwnedPaths` — reuse, don't add a third classifier.
- Reuse the **existing** `Hx.Runner.Core/Git/GitWorktree.cs` (032-hardened `PruneLeakedTemps`) if any worktree is ever needed — never a new mechanism.
- SC-002 oracle: define byte-identical precisely = tracked `git write-tree` over a clean index + the tag/ref set + the release-root/`<version>` dir set, EXPLICITLY excluding gitignored paths; state the exact compare commands; run mode/exec-bit checks on the 3-OS matrix.
- SC-006 ("no agent git command"): reframe as "Rollback leaves the repo clean, asserted via `CommitScopeInspector` read-only, no subsequent git mutation" (deterministic) — or add a real test-assembly rule.
- Release-root compensation: restore-to-baseline (not blind-remove); cross-volume via copy+delete (never `Directory.Move`).

## Reshaped design (post-review)

- **Placement:** ledger primitive in `Hx.Runner.Core/Git/`; reuse existing `GitWorktree`.
- **WI1 (transition integrity):** stage the leaving stage's `produces` (scoped, reuse `SanctionedGitCommit`); fail closed only on an absent/orphaned or would-be-lost produces — NOT present-and-clean. No transaction wrapper (keep `RecoverStateIfNeeded`).
- **WI2 (release envelope):** a compensation ledger over exactly {tag, release-root/`<version>` dir (restore-to-baseline), cycle-state bytes}; tag creation moved inside the ledger with concrete collision reconciliation; ordered named stages + fault-hook seam; `RollbackReport` contract; **no whole-tree snapshot, no worktree-for-isolation**.
- **WI3 (pre-flight):** reuse `CommitScopeInspector` + `FeatureArtifactScope.OwnedPaths` to fail-closed-surface unrelated dirty paths.
- **WI4 (lifecycle):** `finalize-release` for the CI-publish gap, reusing `MarkReleaseTrainReleased` + surfaced in `CycleRecoveryPlanner`.
- **Split:** WI1 and WI2 are the operator's two explicit asks and stay in 039; WI3 and WI4 are independently shippable and are candidates to spin into follow-ups so the urgent transition fix ships without waiting on the release-ledger.

## Status — RESOLVED

All seven BLOCKERs and the HIGH/MEDIUM findings are addressed by the in-cycle design revision (operator-concurred: drop the worktree/whole-tree snapshot; keep engine-owned code rollback on a failed release):
- BLOCKER-1 (placement) → plan v2: `Hx.Runner.Core/Git/`.
- BLOCKER-2 (FR-002) → spec FR-002 narrowed to the orphan-only case.
- BLOCKER-3 (unsafe whole-tree snapshot) → plan v2: compensation ledger only; no snapshot, no worktree-for-isolation.
- BLOCKER-4 (cycle-state) → plan v2: cycle-state.json is a first-class ledgered side effect.
- BLOCKER-5 (tag outside envelope) → plan v2: tag inside the ledger + concrete collision reconciliation.
- BLOCKER-6/7 (test seam + RollbackReport contract) → plan v2: named `ReleaseStage` enum + fault-hook + discriminated result carrying `RollbackReport`.
- HIGH/MEDIUM (blast radius, WI4 framing, reuse `GitWorktree`/`CommitScopeInspector`, SC oracles) → plan v2 "Revised design (v2)".

The SCs stand; only the mechanism and framing changed. The reshaped design (plan v2) is the authoritative design for `/05-tasks` onward. No open BLOCKER remains → `/07-implement` is unblocked on this design.
