# 027 — Codified stamp reconciliation + update orphan-prune

## Goal

Stop forcing the agent to hand-re-stamp stages to refresh hashes, and stop `hx doti update` from leaving conflicting orphaned skill dirs. Two root-caused bugs:

- **Bug A (freshness cascade):** when a revision moves an artifact's binding, the engine classifies *every* derived stale as `RerunRequired`, so `refresh --apply-safe` fixes nothing and the agent re-stamps stage-by-stage. The genuinely-mechanical cases (a prereq **edge** moved / a stage-model reorder, with byte-identical artifacts; and inserted stages) should reconcile **automatically** — but a genuine **content** change must still force a real re-run (never a rubber-stamp).
- **Bug B (orphan-prune):** `hx doti update` is additive for rendered skills; a stage renumber renames the skill dir and **leaves the old one**, so a repo ends with both `04-doti-tasks` (old) and `04-doti-arch-review` (new) — confusing the agent (observed in `agentx` after the 0.15.x reorder).

## User Scenarios & Testing

**Priority Mode: Workflow / tooling** — fail-closed safety + deterministic proof before ergonomics. The auto-reconcile must NEVER weaken proof: it re-binds only when content is provably unchanged.

### Work Item W1 — The safe-rebind invariant (Priority: P1)

Auto-rebind a staled stamp **iff** (i) its OWN produced artifact still canonical-hashes to its bound `ArtifactHashes` (own content unchanged) **AND** (ii) every producing stage now in its transitive prerequisite closure is itself currently `Fresh` **AND** (iii) for every shared prerequisite path, the upstream's canonical content hash is **byte-identical** to what the dependent already bound (the prereq SET/edge moved, not the content value). Any genuine content change (`OwnArtifactChanged`, or a prereq content value change), any change-set-bound stage (`ChangeSetDiffers`), and any **review-kind** stage (`arch-review`/`analyze`/`drift-review`) STAYS `RerunRequired`.

- **Why this priority:** this is the safety boundary. A wrong auto-rebind silently stamps a downstream artifact (plan/tasks) as proven against an upstream it was never re-derived against — inverting "the spec/artifact is source of truth; fix the artifact, never the proof". A review stage's verdict is a judgment over its inputs; a changed input invalidates it by definition.
- **Independent Test:** stamp specify→analyze; edit the spec (real content change) → specify/clarify read `OwnArtifactChanged`, plan/tasks/analyze read content-changed → all stay `RerunRequired` (no auto-rebind). Separately, a pure edge/reorder with byte-identical artifacts → dependents read the new rebindable reason and auto-rebind.
- **Acceptance Scenarios:**
  1. **Given** stamped stages and a spec **content** edit, **When** `cycle check` runs, **Then** plan/tasks/analyze are `RerunRequired` and are NOT auto-rebound.
  2. **Given** a workflow.yml reorder with byte-identical artifacts, **When** the affected stages are checked, **Then** they classify as content-equal-rebindable and `refresh --apply-safe` clears them with no re-run.
  3. **Given** any prereq change, **When** a **review-kind** dependent (arch-review/analyze/drift-review) is evaluated, **Then** it is never auto-rebound — it stays `RerunRequired`.
  4. **Given** a code-only working-tree edit with `implement` stamped, **When** checked, **Then** `implement` is `ChangeSetDiffers`/`RerunRequired` and is never reclassified (locks the 021/026 fix).

### Work Item W2 — Codified auto-reconcile (no manual re-stamp treadmill) (Priority: P2)

Re-running the ONE genuinely-changed stage automatically re-binds its content-equal/edge-only dependents; an inserted/reordered stage absent from cycle-state is surfaced as a first-class **`inserted-stage`** verdict naming the single `/NN` command to produce+stamp it (instead of an opaque "missing"). `refresh --apply-safe` and the automatic on-stamp cascade share one deterministic projection over the freshness the chokepoint already computes.

- **Why this priority:** removes the stage-by-stage hand-re-stamp the agent is forced into today, which is the operator's core complaint. Doing it as a projection over the existing `Check` keeps refresh and the chokepoint from ever disagreeing.
- **Independent Test:** after a reorder, re-stamp the one changed stage (no explicit refresh) → its content-equal dependents are `Fresh` automatically; an inserted stage yields an `inserted-stage` step with its `/NN` command.
- **Acceptance Scenarios:**
  1. **Given** a content-equal cascade, **When** the upstream is re-stamped, **Then** the on-stamp cascade auto-rebinds the dependents (bounded to the stamped stage's closure, prereq-first, re-entrancy-guarded) with zero manual stamps; a `RerunRequired`/`ChangeSetDiffers`/`inserted` step is never auto-stamped.
  2. **Given** an in-flight cycle whose stamped graph differs from the current workflow.yml, **When** checked, **Then** an inserted/reordered stage is reported as `inserted-stage` with its command, not a dead-end.
  3. **Given** a stage transition, **When** the proofs are rebased, **Then** `PrerequisiteArtifactHashes` are recomputed too (no transition re-introduces a false prereq-artifact stale).

### Work Item W3 — Update orphan-prune (Priority: P3)

`hx doti update`/`doti install` prune managed skill dirs a payload renamed away, and `doti payload check` detects surplus dirs.

- **Why this priority:** a renumber must not leave conflicting skill dirs that confuse the agent; but operator-edited skills must never be destroyed.
- **Independent Test:** install at ordinal-set A (tasks=04), update to set B (tasks=05, arch-review=04) → the old `04-doti-tasks` is removed (clean baseline match), its dir pruned, recorded in `ObsoleteAssets`; a baseline-**modified** orphan is preserved/blocked, not deleted; `payload check` flags a surplus dir.
- **Acceptance Scenarios:**
  1. **Given** a payload that renames a managed skill dir, **When** update runs, **Then** the baseline-clean old dir is deleted and recorded obsolete; **When** the old dir was operator-edited, **Then** it is preserved/blocked (no `--force`).
  2. **Given** a repo with both old + new numbered dirs, **When** `doti payload check` runs, **Then** the surplus dir is flagged.

### Edge Cases

- Edit that BOTH reorders edges AND changes content → fails the all-upstreams-Fresh + content-identical precondition → `RerunRequired` (fail-closed).
- Pre-fingerprint / pre-category proofs and manifests → null fields route to safe re-stamp / no-op prune (never wedge, never destroy).
- The on-stamp cascade must terminate (each pass strictly reduces the rebindable set; a step that doesn't become Fresh is left, never retried).
- A repo whose prior manifest predates the skill category → prune sources candidates from an on-disk scan, not only the manifest.

## Scope

**Included:** the `PrereqRebindable` StaleReason split; the `ReBindContentEqual` safety tier; the all-upstreams-Fresh + content-identical gate; the review-kind + change-set-bound exclusions; the `inserted-stage` verdict; the on-stamp auto-cascade (re-entrancy-guarded); `RebaseProofsToHead` recomputing `PrerequisiteArtifactHashes`; the optional additive `StageGraphFingerprint`; the `DotiInstaller` orphan-prune (on-disk-sourced, clean-baseline-gated); `DotiPayloadParityChecker` surplus detection; the full test matrix; updating `/doti-amend` prose to note the now-automatic rebind.

**Excluded:** no change to gate-proof hashing or the gate-proof digest; no change to stage IDs / `produces` / cycle-state schema version (fields are additive/nullable); no auto-rebind of content changes, review-kind, or change-set-bound stages; no destruction of operator-edited assets.

## Functional Requirements

- `FR-001`: `FreshnessEvaluator` MUST emit a distinct `PrereqRebindable` reason when a stage's own artifact hash is unchanged and the ONLY divergence is its prerequisite-artifact binding with byte-identical shared-path content; a genuine own-content change or a change-set-bound stale MUST NOT reach it. `[W1]`
- `FR-002`: `RestampSafetyClassifier` MUST map `PrereqRebindable` → a new `ReBindContentEqual` tier; `OwnArtifactChanged`/`PrereqArtifactChanged`/`ChangeSetDiffers` MUST remain `RerunRequired`; the function stays pure/total. `[W1]`
- `FR-003`: A `ReBindContentEqual` step MUST be downgraded to `RerunRequired` unless every producing stage in its transitive closure is `Fresh` in the same check report; and a **review-kind** dependent MUST never be auto-rebound. `[W1]`
- `FR-004`: An in-flight stage absent from cycle-state but required by the current graph MUST be reported as `inserted-stage` with the `/NN` command to produce+stamp it (not a null-safety dead-end). `[W2]`
- `FR-005`: `cycle refresh --apply-safe` MUST re-stamp `SafeReinterpret` AND `ReBindContentEqual` steps, re-deriving the plan after each so a chain settles in one pass. `[W2]`
- `FR-006`: A successful `cycle stamp` MUST auto-cascade the safe rebind over the stamped stage's dependents (closure-bounded, prereq-first, re-entrancy-guarded), never auto-stamping a `RerunRequired`/`ChangeSetDiffers`/`inserted` step, and a cascade failure MUST NOT fail or roll back the primary stamp. `[W2]`
- `FR-007`: `RebaseProofsToHead` MUST recompute `PrerequisiteArtifactHashes` alongside the existing rebinds so a transition never re-introduces a false `PrereqArtifactChanged`. `[W2]`
- `FR-008`: `DotiInstaller.Install` MUST prune managed skill dirs (category skill-generated-instruction, under an agent SkillsRoot) that the new render targets no longer include, sourcing candidates from an on-disk scan, deleting ONLY baseline-clean files (operator-edited orphans preserved/blocked), pruning empty dirs, and recording them in `ObsoleteAssets`. `[W3]`
- `FR-009`: `DotiPayloadParityChecker` MUST flag a surplus `*-doti-*` skill dir present in the repo but absent from the render targets. `[W3]`
- `FR-010`: All new proof/state fields MUST be additive and nullable (no schema-version bump, no wedge of existing proofs); the gate-proof digest MUST be byte-unchanged. `[W1]`

## Success Criteria

- `SC-001`: A spec **content** edit leaves plan/tasks/analyze `RerunRequired` (proven by test); they are never auto-rebound.
- `SC-002`: A pure edge/reorder with byte-identical artifacts auto-reconciles via `refresh --apply-safe` (and the on-stamp cascade) with zero manual re-runs.
- `SC-003`: A review-kind stage and a `ChangeSetDiffers` stage are never auto-rebound (proven by test).
- `SC-004`: An inserted/reordered in-flight stage yields an `inserted-stage` verdict with its `/NN` command.
- `SC-005`: A renumber update removes the baseline-clean orphan skill dir and preserves a modified one; `payload check` flags a surplus dir.
- `SC-006`: `gate run --profile release` passes; the persisted gate-proof digest is byte-identical to before the change for an unchanged diff.

## Deterministic Surfaces

`tools/Hx.Cycle.Core/FreshnessEvaluator.cs`, `RestampSafetyClassifier.cs`, `CycleRecoveryPlanner.cs`, `CycleService.Refresh.cs`, `CycleService.Stamp.cs`, `CycleService.TransitionRecords.cs`, optionally `Hx.Tooling.Contracts/CycleState.cs`; `tools/Hx.Doti.Core/DotiInstaller.cs`, `DotiPayloadParityChecker.cs`. Proof commands: `doti cycle check`/`refresh`, `doti payload check`, `gate run`, plus the test matrix in `test/Hx.Cycle.Tests` + `test/Hx.Runner.Tests` + `test/Hx.Doti.Tests`.

## Architecture Impact

`*.Core` logic only (Hx.Cycle.Core + Hx.Doti.Core); no CLI surface change, no new error codes, no stage-model reorder. Additive nullable proof fields. No ArchUnit family or Sentrux boundary change.

## Sentrux And Hygiene Impact

Modest production-code additions to existing types (a new enum value, a tier, a gate, a prune pass). Keep methods within the Sentrux function-size limit (extract the rebindable-decision + the orphan-prune into named helpers). No baseline change expected.

## Assumptions

- The all-upstreams-Fresh precondition is read as a pure projection over the same `Check` report (never a second evaluator), so refresh and the chokepoint cannot disagree.
- "byte-identical content" is via the existing `CanonicalArtifactHasher` normalization (EOL/checkbox/hash-marker-insensitive).
- Feature cycle → minor bump at release.

## Acceptance

All checks command-backed: `dotnet test` (the matrix), `doti cycle check`/`refresh`, `doti payload check`, `gate run`. No advisory-only gates relied on.

## Clarifications

(Populated by `/02-doti-clarify` if a blocking ambiguity arises.)
