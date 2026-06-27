# Implementation Plan: Doti Review Recovery and Change Context

**Slug**: `008-doti-review-recovery-and-change-context`
**Spec**: [docs/specs/008-doti-review-recovery-and-change-context.md](../specs/008-doti-review-recovery-and-change-context.md)
**Priority mode**: workflow/tooling (fail-closed safety + deterministic proof before ergonomics).

## Selection rule for this plan

Every decision below picks the **simplest correct, modular** design that (a) satisfies the FR/SCs, (b) fits the existing patterns named in the assessment, (c) preserves deterministic proof (gates stay fail-closed; nothing enforced→advisory), and (d) avoids future drift. Alternatives are rejected by **architectural trade-off**, never effort. Single-responsibility `*.Core` types, composed not inlined.

## Architecture assessment (the patterns this design fits)

- **Boundary:** `contracts → core → cli` one-way (`rules/architecture.json`, `.sentrux/rules.toml` layers). Cycle commands are thin CLI in `Hx.Runner.Cli/RunnerCommandFactory.*` delegating to `Hx.Cycle.Core`; every command builds a `CliResult` (`Hx.Cli.Kernel`) carrying a structured error-code. ArchUnit families `cliSurfaceConfinement` / `cliDelegation` forbid business logic (`*Planner`/`*Classifier`/`*Projector`/`*Detector` suffixes) in `*.Cli`.
- **Freshness is re-derived, never stored.** `FreshnessEvaluator.Evaluate` (`tools/Hx.Cycle.Core/FreshnessEvaluator.cs:31`) derives `Fresh|Stale` from three bindings: change-set identity (`:37`), the stage's own `produces` artifact hash (`:44`), transitive prerequisite-artifact hashes (`:61`). The "missing prerequisite artifact binding" stale arises from a stage with `produces` but empty `ArtifactHashes` (`:50`); the "change-set identity differs" stale from `:37`.
- **The change-set engine already exists.** `ImpactChangeCollector.Collect` (`tools/Hx.Impact.Core/ChangeDetection/ImpactChangeCollector.cs:13`) computes `merge-base(base,head)..head ∪ working-tree`, fail-closed on unresolved merge-base (`:18`); `ChangeSetIdentity.Of` (`tools/Hx.Cycle.Core/ChangeSetIdentity.cs:30`) and `AffectedTestPlanner` already consume it. `Hx.Cycle.Core` already depends on `Hx.Impact.Core` (`ChangeSetIdentity.cs:1`) — the edge for reuse exists.
- **`produces` is the binding hook.** Stages with `produces` get their artifact hashed at stamp (`CycleService.Stamp.cs:171`) and re-checked at read; `specify/clarify/plan/tasks` use it (`workflow.yml:11`). `analyze`/`arch-review` have no `produces` (`workflow.yml:31`) → the empty-hash stale-loop.
- **The gate** runs Sentrux + architecture per-tier but with **no docs-only short-circuit** (`tools/Hx.Gate.Core/GateRunner.cs:48,55,60`, `ApplyTier:89`); `.sentruxignore` excludes `tools/`/`artifacts/`/`scaffold/templates/` but **not** `.md`/`docs/`/`.doti` prose. `SentruxRegression.Evaluate` is binary pass/fail vs `signalToleranceBand=100` (`sentrux.json:7`). The committed `.sentrux/baseline.json` is the deterministic anchor (kept — see Phase 2).
- **polaris-core** (`D:/github/heurex/polaris-core`) already has the dual-engine embedding stack to leverage: `IEmbedder` + `SemanticEngineFactory`, `Engines/Qwen3Embedder.cs` (LLamaSharp/GGUF), `Engines/BgeM3Embedder.cs` (ONNX), `ModelLocator.cs` (env-only today), `SimilarityService`/`Vectors`. Its `Hx.Semantic.Core` is "zero project references" — the decoupling FR-040 wants.

---

# Design by phase (build order for /04-doti-tasks)

## Phase 0 — Shared foundations (P1/P2 prerequisites)

### 0a. `ChangeSetContext` + `ChangeSetContextBuilder` (FR-008/009/011) — `Hx.Impact.Core`
**Decision.** A `ChangeSetContext(BaseRef, HeadRef, BaseSha, IncludesWorkingTree, IReadOnlyList<ChangedFile> Files, IReadOnlyList<string> AffectedSourceProjects, bool RefsResolved, string? UnresolvedReason)` record + `ChangedFile(Path, Status, OldPath?)`. **Generalise `ImpactChangeCollector` in place**: keep the diff/status NUL-parsing (`ImpactChangeCollector.cs:44`) but emit `ChangedFile` (status + rename old-path) instead of collapsing to a bare path; the existing bare-path `Collect` becomes a `.Files.Select(f => f.Path)` adapter so `ChangeSetIdentity` + `AffectedTestPlanner.Plan` are unchanged. `AffectedSourceProjects` reuses `AffectedTestPlanner.Resolve` (`AffectedTestPlanner.cs:27`) — no graph re-walk. Fail-closed stays (`:18`): `RefsResolved=false` for the projection path, still throwing for the identity path.
**Rationale.** Spec Assumptions: *generalise `impact plan --for arch-review`, not a new git layer.* One place parses rename/status; every consumer is a projection. Co-locating with the planner reuses the reverse-closure for free.
**Alternatives rejected.** A standalone `Hx.Change.Core` — duplicates the collector + forces a new cross-core edge Sentrux would flag.
**Architecture delta.** New records in `Hx.Impact.Core` (core layer) — no boundary change. **Invariant:** `ChangeSetContext` is review context, **not** a proof input — it must never enter a gate-proof hash (`GateRunner.cs:434`), preserving FR-020/SC-009.

### 0b. `FreshnessEvaluator` typed `StaleReason` (Areas 2/4 prerequisite) — `Hx.Cycle.Core`
**Decision.** Change `StageFreshnessResult` to carry a `StaleReason { ChangeSetDiffers, OwnArtifactChanged, PrereqArtifactChanged, MissingBinding, NotProduced }` enum alongside the existing prose `Reason`. Internal to `Hx.Cycle.Core`; prose preserved for CLI display.
**Rationale.** The refresh classifier (1a) needs the staleness *category*, not a prose string. One source of truth for "why stale" is the substrate's whole ethos.
**Alternatives rejected.** Re-derive the category in the classifier by re-running the hash comparisons — a second source of truth that can drift from the evaluator.
**Architecture delta.** Mechanical churn to freshness consumers (`Status`, `Check`, tests). No new type/edge.

### 0c. `FeatureArtifactScope` (FR-038/037 prerequisite) — `Hx.Cycle.Core`
**Decision.** Pure helper `OwnedPaths(StageModel, feature)` → the set of `produces` paths resolved against a slug (via `StageModel.ResolveProduces`, `StageModel.cs:66`). Used to subtract a feature's footprint.
**Rationale.** Both FR-038 (exclude the incoming feature's footprint) and FR-037 (the earlier feature's footprint) need the same "what does feature X own" set. Single-responsibility, IO-free, testable.
**Architecture delta.** Leaf `*.Core` type. No edge.

---

## Phase 1 — Proof integrity (P1, load-bearing)

### 1a. `RestampSafetyClassifier` + `CycleService.Refresh.cs` (FR-005/006/007, SC-002/003)
**Decision.** Pure `RestampSafetyClassifier` maps a stale stage (using 0b's `StaleReason`) to `RestampSafety { SafeReinterpret, RerunRequired, NotBound }`: `MissingBinding`/`NotProduced`+absent → `NotBound` (refuse, FR-005); `PrerequisiteArtifactHashes is null` with unchanged content + unchanged change-set identity → `SafeReinterpret` (FR-006 — the runner/schema-bump case, `FreshnessEvaluator.cs:67`); any content/identity diff → `RerunRequired` (FR-007). `CycleService.Refresh(target, applySafe)` builds the recovery plan and, for `--apply-safe`, re-stamps **only** `SafeReinterpret` steps by reusing the existing `Stamp` path (which recomputes `PrerequisiteArtifactHashes`, `CycleService.Stamp.cs:20`); `NotBound`/`RerunRequired` are left + reported. CLI: `doti cycle refresh --target <stage> --apply-safe --json`.
**Rationale.** `SafeReinterpret` is already a distinct named branch in `FreshnessEvaluator` — the only staleness from the *runner*, not the *inputs*. Classifying faithfully re-reads the evaluator's own arms, so refresh can never disagree with the freshness gate. Classification (read-only) is separated from application (the only mutator), and application reuses `Stamp`.
**Alternatives rejected.** A stored `proof.SafeToRefresh` flag — violates "freshness is re-derived, never stored." Silent auto-refresh inside `Stamp` — weakens SC-002/003's *refusal* guarantee; refresh must be opt-in + report what it refused.
**Architecture delta.** Two `*.Core` types; no new edge. `--apply-safe` is the one mutating path; everything else read-only.

### 1b. `CycleRecoveryPlanner` + `nextActions` (FR-001/002, SC-001)
**Decision.** Pure `CycleRecoveryPlanner.Project(CycleCheckReport, StageModel, classifier)` → `CycleRecoveryPlan(Target, Recoverable, IReadOnlyList<StageRecoveryStep>)`, each step = `{Stage, Status, Reason, Safety (from 1a), RequiredRerun (CycleStage.Command), NextCommand}`. It **projects the existing `Check` output** (`CycleService.Check.cs:39`), not a second evaluator. New partial `CycleService.RecoveryPlan.cs`; CLI `doti cycle refresh-plan --target <stage> --json` (read-only). **FR-002:** `RunnerCommands.CycleCheck` populates `nextActions` from the plan's `NextCommand`s (the `CliNextAction` pattern already used by `CycleStamp`, `RunnerCommands.DotiCycle.cs:50`).
**Rationale.** The plan is a projection of `Check` — one source of truth for "what is stale and why." `CycleRecoveryPlanner` is pure/IO-free (testable without a repo).
**Alternatives rejected.** Extend the crash-recovery `CycleService.Recovery.cs` — couples unrelated failure modes (process-crash vs stale-stamp). Compute the plan inside `Check` — overloads the fail-closed chokepoint with advisory output.
**Architecture delta.** New `*.Core` planner + contract records. CLI stays thin. No Sentrux implication.

### 1c. New-feature-start change-set scoping (FR-038, SC-020)
**Decision.** On a new-feature start, exclude the **incoming** feature's owned paths (0c `FeatureArtifactScope.OwnedPaths(incoming slug)`) from the **prior** feature's `HasUnstaged/HasUntracked` readiness rejection. Thread a "starting new feature" scope from `TransitionBeforeStamp` (`CycleService.Transition.cs:25`, which already knows `startingNewFeature:21`) into `ValidateTransitionReadiness` (`CycleService.TransitionReadiness.cs:54`). The transition **already rebases** the prior proofs to the new commit (`RebaseProofsToHead`); only the pre-check over-reads — this is the minimal fix.
**Rationale.** Touches exactly one decision (the prior-feature readiness scope), reuses the `produces` ownership model; the rebase logic is already correct.
**Alternatives rejected.** Require manual stage/stash (the workaround) — forbidden by FR-038/SC-020. Filter at `CommitScopeInspector` for all callers — weakens the deliberate-scope guard for normal same-feature transitions (blast radius).
**Architecture delta.** Confined to `Hx.Cycle.Core`. **Risk to test (SC-020):** the exclusion must be *only* the incoming feature's `produces` set — a stray unrelated untracked file MUST still block. The test proves a one-stamp clean start with `008-…md` on disk **and** that a stray file still blocks.

### 1d. Review-stage artifact binding (FR-003/004, SC-021)
**Decision.** Declarative: add `produces:` in `workflow.yml` — `arch-review → docs/reviews/{feature}-arch-review.md`, `drift-review → docs/reviews/{feature}-drift-review.md`, `analyze → docs/reviews/{feature}-analyze-report.md` (its own report, for binding uniformity — anchoring on `converge` was the rejected alternative). The existing `Stamp`/`FreshnessEvaluator` binding does the rest; SC-021 (safe re-interpret on a runner bump) falls out of 1a automatically. **Must land paired with 1a's `--apply-safe`** (adding `produces` to an empty-hash stage makes it instantly Stale, `FreshnessEvaluator.cs:50` — the empty→bound transition is precisely `SafeReinterpret`).
**Rationale.** Declarative data change reusing the identical `produces` path the doc stages use; review stages become first-class bound stages. The drift/arch templates already write the record.
**Alternatives rejected.** A bespoke review-artifact registry — forks the freshness model. Leave `analyze` unbound — SC-021 now requires it safe-refreshable.
**Architecture delta.** `workflow.yml` data + report files under `docs/reviews/`. **Sentrux:** these `.md` reports MUST be source-excluded (Phase 2 FR-029). Update cycle-state tests that assert review stages carry empty `ArtifactHashes`.

### 1e. In-cycle drift patch loop (FR-012/013) — doti-prose
**Decision.** The `/08-doti-drift-review` template already encodes "fix the CODE, never the spec" (this session). Extend it with the explicit patch loop: fix code/docs/assets → rerun required proof → **refresh implement state** (1a) → rerun affected drift axes (Phase 2 review-context) → stamp drift-review. No new code — a template change consuming 1a + Phase 2.
**Rationale.** The loop is workflow choreography over existing commands; FR-013 (spec is truth) is already enforced.
**Architecture delta.** Doti-prose only (`.doti/core/templates/commands/doti-drift-review.md` + skills.json highlight → re-render).

---

## Phase 2 — Change/review context + gate/Sentrux scoping (P2)

### 2a. `ReviewContext` projector (FR-025) — `Hx.Cycle.Core`
**Decision.** `ReviewContext(ChangedFiles, AffectedProjects, Categories, ApplicableLenses, SkippedLenses, EscalationReasons)` + `ReviewContextProjector.Project(ChangeSetContext)`. Categories lift the planner's `Classify` taxonomy (`AffectedTestPlanner.Analysis.cs:85`) + the FR-023 set (`doti-prose`, `generated-template`, `runtime-code`, `docs-only`, `contract`). The lens set is the arch-review panel *Applies-when* table (`doti-arch-review.md:21`) made **data** (category→lens map). Surfaced as `hx doti review-context --json` (lives in `Hx.Cycle.Core` because the lens↔stage map is workflow knowledge — the resolved fork). drift-review (FR-010) consumes `hx impact plan --for change-context` for its diff (replacing the manual `git diff` at `doti-drift-review.md:8`).
**Rationale.** One taxonomy, two read sites; promotes prose triage to a deterministic, testable projection (SC-011/012/013 become projector unit tests).
**Alternatives rejected.** Keep triage as skill prose — non-deterministic, unassertable. A standalone `hx review-context` command — re-collects the change set.
**Architecture delta.** `*Projector` → `.Core` (`cliSurfaceConfinement`). Reads `rules.toml` layer paths via a small `LayerMap` loader (one named seam).

### 2b. Arch-review consumes review-context + conditional rerun (FR-026/027, SC-012/013)
**Decision.** Arch-review injects `data.applicableLenses` verbatim instead of re-deriving triage. `ArchitectureRelevantSurface.IsTouched(ChangeSetContext)` returns true when the categorised change touches the FR-027 surfaces (contracts, CLI shape, dependency direction/layering via `rules.toml`, persistence, security, generated-code templates, cross-module) — same taxonomy as `Classify` + the layer map. During implement/drift-patching the skill checks this predicate; docs/prose-only → no rerun.
**Rationale.** Consolidate the taxonomy that already lives in two places (planner `Classify`, two skills' prose) into the projector; one taxonomy, two read sites.
**Architecture delta.** `ArchitectureRelevantSurface` is a `*.Core` predicate.

### 2c. Docs-only gate skip (FR-028, SC-011)
**Decision.** Derive a `GateScope` from the affected plan the gate **already computes** (`AffectedChangeStep`, `GateRunner.cs:251`; docs/generated → `NoTestsRequired`, `AffectedTestPlanner.cs:41`) + `ReviewContext` categories. When the change is docs-only/doti-prose-only with no runtime/generated code, mark `architecture-test` + `sentrux-*` `Skipped` **with a scope reason**, gated through the existing `ApplyTier` seam (`:89`) so the skip is recorded in `GateProof` coverage and provable-not-bypassed on recompute. render/payload/skill-drift stay enforced (not tier-wrapped) — SC-011.
**Rationale.** Second condition on the seam the gate already has; recorded in the same coverage proof. A *scope* skip is distinct from a *missing-config* fail (preserve bypass-safety).
**Alternatives rejected.** A new `GateScopePlanner` re-deriving docs-only-ness — the affected plan already classifies it.
**Architecture delta.** `GateScope` written into `GateProof` coverage; the docs-only skip is a scope skip, not a fail.

### 2d. Sentrux source-scope (FR-029, SC-016)
**Decision.** Two layers: (floor) extend `.sentruxignore` to exclude `*.md`, `docs/`, `.doti/` prose (templates/commands, rendered skills, agent-context), payload metadata, non-code templates; (override) add `codeExtensions` (default `[".cs",".csproj"]`, matching `requiredGrammars:["csharp"]`) + an "explicitly configured as code" list to `SentruxPolicy` (`SentruxPolicy.cs:8`); `SentruxChecker` filters the change set to code scope before invoking. No new fork `--paths` flag (the adapter notes flags are pinned-fork-verified — `SentruxProcessAdapter.cs:8`).
**Rationale.** `.sentruxignore` floor is what SC-016 literally asserts (out of the `git ls-files` set, not merely unparsed); the policy field is FR-029's "unless explicitly configured as code."
**Architecture delta.** **Operationally load-bearing:** extending `.sentruxignore` shifts the committed `.sentrux/baseline.json` coupling set — design the regression check to tolerate this (the baseline is kept; see 2e).

### 2e. Escalation band + two-try diagnostic + rebaseline gate (FR-030/031/032, SC-014/015) — the operator-clarified design
**Decision.** The committed `.sentrux/baseline.json` is **never removed** — the deterministic complexity/architecture anchor. Extend `SentruxRegression.Evaluate` (`SentruxRegression.cs:12`) to a **three-state** result: `Pass` (within tolerance), `EscalationBand` (above hard tolerance but ≤ **130 / 1.3× the band**), `Fail` (beyond). The **two-try diagnostic** lives in `Hx.Cycle.Core` (cycle state, not Sentrux state) — a small `SentruxOptimizationLog` keyed by change-set identity records attempts; within the band, the agent gets two documented optimization attempts. If two attempts fail to bring it under tolerance, emit a **structural-architecture-review next action** (FR-030/SC-014) — *not* another blind optimization pass. The structural arch-review classifies: **legitimate functionality-driven growth → evidence-gated rebaseline** (raises the baseline) vs **wrong architecture → refactor, no rebaseline**. `SentruxRebaselinePolicy.Authorize(repo)` (`Hx.Cycle.Core`) guards `SentruxBaselineRunner.Save` (`SentruxBaselineRunner.cs:14`) — **refusing** unless explicit operator intent **and** a fresh arch-review record justifying the structural change (FR-031/SC-015). The gate path **never** calls `Save` (preserve `GateRunner.cs:27`). **FR-032:** at release, an unresolved `EscalationBand` is `Fail` unless the cycle carries an authorized rebaseline record.
**Rationale.** Band classification is arithmetic → `Hx.Sentrux.Core` (pure). Attempt-count + next-action + evidence gate need workflow/artifact reads → `Hx.Cycle.Core` (it owns `GateProofStore`, `StageModel`, next-actions). Keeps `Hx.Sentrux.Core` free of workflow knowledge; no Sentrux→Cycle edge. The arch-review-gated rebaseline is the operator's "floor" that lets the baseline rise with functionality without degrading.
**Alternatives rejected.** A stateless band number with no cycle memory — can't count the two attempts (SC-014 unimplementable). Auto-rebaseline when "approved" — violates the never-auto-create rule. Remove the baseline — explicitly rejected by the operator; FR-031/032 presuppose it.
**Architecture delta.** `SentruxRegression` band + `SentruxPolicy` fields in `Hx.Sentrux.Core`; `SentruxOptimizationLog` + `SentruxRebaselinePolicy` beside `GateProofStore`/`CycleStateStore` in `Hx.Cycle.Core` (same pattern, no new edge).

---

## Phase 3 — Release-train completeness (P3)

### 3a. Per-feature gate-proof status (FR-036)
**Decision.** Replace the degenerate `FeatureForCompletion` gate-proof line (`CycleService.ReleaseTrain.cs:115`, currently "digest string exists?") with real validation: resolve the feature's gate proof via `GateProofStore` and run `GateProofValidator.ValidateAffectedTestProof`/`ValidateLadderCoverage` (the same validators the transition uses, `CycleService.TransitionReadiness.cs:49`). Emit `present-valid|present-stale|missing` and push failures into `blockers`. The `GateProofStatus` field already exists.
**Rationale.** One-method correctness fix to an already-wired field using existing validators — zero new surface.
**Architecture delta.** Reuses existing `Hx.Cycle.Core` types; no new edge.

### 3b. Cross-feature release-train drift (FR-037, SC-019)
**Decision.** Pure `ReleaseTrainDriftDetector`: for each *earlier* completed-unreleased feature, intersect its owned footprint (`FeatureArtifactScope.OwnedPaths`, 0c) with a *later* feature's change set (`ImpactChangeCollector.Collect(earlier.CommitSha, later.CommitSha)`); a non-empty intersection (a later feature changed a path an earlier feature owns/documents) → `ReleaseTrainDriftFinding(EarlierFeature, LaterFeature, ConflictingPaths, Reason)` added to `blockers`. Surfaced on `CycleReleaseTrain`. `BuildReleaseTrain` composes it.
**Rationale.** Reuses the two engines the substrate is built on; deterministic commit-range analysis, not heuristics. Pairwise O(n²) analysis is a distinct responsibility → its own type (isolated SC-019 test).
**Alternatives rejected.** Semantic/AI drift — advisory, can never gate a release (FR-020). Inline in `BuildReleaseTrain` — cohesion.
**Architecture delta.** `*Detector` → `.Core`. Depends on `ImpactChangeCollector` — edge already exists (`ChangeSetIdentity.cs:1`).

---

## Phase 4 — Single-source templates (P4)

### 4a. Remove the committed twin, materialize from core (FR-014–017, SC-005/006/007)
**Decision.** (1) `git rm -r .doti/templates/` (verified 19 byte-identical files nothing reads at runtime — only `.doti/core/templates` is consumed: `DotiRenderer.cs:18`, skills.json `commandTemplateDir`); gitignore `.doti/templates/`. (2) **Materialize at pack** in `StageHxPayload` (`Hx.Scaffold.Cli.csproj` after `:87`): copy `.doti/core/templates/**` → staged `.doti/templates/` *before* manifest generation (anchor preserved). (3) **Materialize at install** via a small reusable `DotiTemplateMaterializer` in `Hx.Doti.Core`, called by `DotiInstaller.Install` (installed repos keep `.doti/templates` — the resolved fork, smallest delta). (4) **Rewrite the parity test** + `DotiPayloadParityChecker.CheckStaticFiles`: the templates leg's *expected* side becomes `core/templates`, *actual* side the materialized installed `.doti/templates` — proving materialized==core **with source `.doti/templates` absent** (FR-017, fixing the silently-skipped hole at `DotiPayloadParityChecker.cs:45`). (5) Key `HasFullDotiPayloadShape` on `.doti/core/templates` not `.doti/templates` (`RunnerCommands.Doti.Render.cs:50`) so source-repo `render-skills --check` still runs payload parity. (6) Reclassify `.doti/templates` as a *materialized* asset in `ManagedAssetScanner` (not `DotiSource`) so the source repo's own install/check passes with it absent.
**Rationale.** Single source of truth is the architectural invariant (FR-014); materialization makes `core/templates` *causally upstream* — drift is structurally impossible, not test-policed. Materialize where the payload is already assembled (`StageHxPayload`), co-located with the manifest/anchor it must precede.
**Alternatives rejected.** Keep the twin + test (violates FR-014/015). Drop `.doti/templates` from the payload + render on demand (changes the install contract; FR-016 requires materialized templates in the payload). Symlink (not portable; doesn't survive `dotnet pack`). Generate into the committed source tree (re-introduces a twin).
**Architecture delta.** One deletion + gitignore, one MSBuild copy, one reusable `DotiTemplateMaterializer` in core, three checker/test/predicate edits. The parity assertion moves from "two committed trees agree" to "materialized payload == authoritative core, proven without the twin."

---

## Phase 5 — Advisory semantic finder (P5, never gating)

### 5a. `Hx.Embedding.Core` — the standalone dual-engine loader (FR-040/041/042) — new project
**Decision.** A new `tools/Hx.Embedding.Core` project, **zero `Hx.*` deps except `Hx.Tooling.Contracts`** (reusable by the scaffold), porting polaris-core verbatim where the math is engine-agnostic: `IEmbedder`, `SemanticEngineFactory` (fail-closed construction), `SimilarityService`, `Vectors`, `EngineOptions`. **Both engines** behind `IEmbedder`: `Qwen3Embedder` (LLamaSharp/GGUF — the **primary**, ported from polaris-core `Engines/Qwen3Embedder.cs`) and `BgeM3Embedder` (ONNX via `Microsoft.ML.OnnxRuntime` + `OnnxRuntimeExtensions` — the **fallback**). `SemanticEngineFactory` selects Qwen3 when its GGUF model resolves, else falls back to BGE-M3, and **records the active engine id** (FR-042). `ModelLocator` (5b). Package refs: `LLamaSharp` + `LLamaSharp.Backend.Cpu` (primary) and `Microsoft.ML.OnnxRuntime`(+Extensions) (fallback).
**Rationale.** polaris-core already proves the decoupling (`Hx.Semantic.Core` = zero project references) and already has both engines behind `IEmbedder` — leverage = port the working dual-engine stack; the scaffold can reuse it precisely because it depends on nothing doti.
**Alternatives rejected.** Cross-repo ProjectReference to polaris-core — breaks 007's source-independence. One combined project (loader+drift logic) — couples the reusable loader to doti's change-set/axis concepts.
**Architecture delta.** New ArchUnit rules (in `test/Hx.Architecture.Tests/ArchitectureTests.cs`): (1) `Hx.Embedding.Core` depends on no `Hx.*` except `Hx.Tooling.Contracts` (FR-040); (2) `Hx.Embedding.Core` has no `System.Net.Http` (FR-021 local-only). Dependency direction `Hx.Semantic.Cli → Hx.Semantic.Core → Hx.Embedding.Core → (LLamaSharp|ONNX only)`. Model files + native backends are vendored non-code → `.sentruxignore` + pinned `*.version.json` (FR-039).

### 5b. Model-store resolution (FR-041) — `Hx.Embedding.Core.ModelLocator`
**Decision.** Customise polaris-core's locator (which is **env-only** today, `ModelLocator.cs:11`) to: `root = config.LlmModelRoot ?? Environment.GetEnvironmentVariable("HEUREX_LLM_ROOT") ?? throw` (config WINS; fail-hard when neither). Config home = `hx.config.json` `llmModelRoot` (`HxLocalConfiguration`, the existing executable-adjacent trusted config — resolved fork). Keep the per-asset `RequireFile` fail-closed guard. The operator's `D:/LLM-Models` is supplied via config/env, never hardcoded. Models are operator-provisioned into the store, pinned+hashed via a `model.version.json` (matching 007's source-free vendored-tool pattern — **not** bundled in the nupkg).
**Rationale.** polaris-core gives env + fail-hard; only config-source + config-wins precedence are missing. Reuse the existing trusted-config chokepoint = least new surface.
**Architecture delta.** `ModelLocator` in `Hx.Embedding.Core`; `llmModelRoot` field on `HxLocalConfiguration`.

### 5c. `Hx.Semantic.Core` + `Hx.Semantic.Cli` — `drift-candidates` (FR-018/019/020/021, SC-008/009) — new projects
**Decision.** `Hx.Semantic.Core` (refs `Hx.Embedding.Core` + `Hx.Impact.Core` for the change set) implements `hx doti drift-candidates --base <baseRef> --json`: consume the **existing** `ChangeSetContext` (0a) — no own `git diff`; chunk changed-code hunks + doc/help/skill/test sections → `IEmbedder.Embed` → `SimilarityService` → rank → `SemanticCandidate{evidenceSnippet, confidence, affectedAxes, suggestedDeterministicChecks}` (FR-019), emitted via `CliResult` with the active engine (FR-042). `Hx.Semantic.Cli` is the thin channel. **It has no stamp, no proof, no gate-proof field** — advisory only.
**Rationale.** Everything needed exists + is reusable (change set from `impact plan`; embed→similarity from polaris-core). Purely additive, read-only.
**Architecture delta.** **The never-gating invariant is compile-checked:** new ArchUnit rule — `Hx.Gate.Core`/`Hx.Cycle.Core` MUST NOT depend on `Hx.Semantic.*` (so semantic output can never reach a proof; FR-020/SC-009). Sentrux: the new `*.Core` projects are in scope (source/code); model assets excluded.

### 5d. Utility skills (FR-022/023/024, SC-010) — doti-prose
**Decision.** Unnumbered utility skills `doti-amend` (workflow amendment) + `doti-drift-fix`/`doti-patch` (drift patching), rendered from `skills.json` like the existing `converge`/`doti-bug`/`doti-upgrade` utilities (they don't reorder `/01–/09`). They classify changes (FR-023) via `ReviewContext` categories (2a) and use the recovery plan (1b) rather than guessing stamps (FR-024).
**Rationale.** Mirrors the existing unnumbered-utility pattern; reuses 2a + 1b rather than new logic.
**Architecture delta.** `skills.json` entries → re-render. No reorder of the numbered path.

---

# New projects + consolidated architecture delta

| New project | Layer | Deps | Purpose |
|---|---|---|---|
| `Hx.Embedding.Core` | core (leaf) | `Hx.Tooling.Contracts` + LLamaSharp/ONNX only | Standalone dual-engine embedding loader (FR-040) |
| `Hx.Semantic.Core` | core | `Hx.Embedding.Core`, `Hx.Impact.Core` | `drift-candidates` logic (advisory) |
| `Hx.Semantic.Cli` | cli | `Hx.Semantic.Core` | thin `hx doti drift-candidates` channel |

**New ArchUnit families** (codify spec safety clauses as compile-checked invariants): `Hx.Embedding.Core` → no `Hx.*` except Contracts (FR-040); `Hx.Embedding.Core` → no `System.Net.Http` (FR-021); `Hx.Gate.Core`/`Hx.Cycle.Core` → no `Hx.Semantic.*` (FR-020/SC-009). New `*Planner`/`*Classifier`/`*Projector`/`*Detector` types all `.Core`-confined (existing `cliSurfaceConfinement`).

**Sentrux delta:** new `*.Core` projects in scope (code); model GGUF/ONNX assets + `model.version.json` excluded via `.sentruxignore` + pinned manifest. The `.doti`/docs/`.md` exclusion (2d) shifts the committed baseline coupling set — the regression check tolerates it; the baseline stays.

**Resolved forks (recorded above):** FreshnessEvaluator typed enum (0b) · analyze own-report (1d) · ReviewContext in `Hx.Cycle.Core` (2a) · installed repos keep materialized templates (4a) · model-root config in `hx.config.json` (5b) · model operator-provisioned + pinned (5a) · Sentrux source-scope via `.sentruxignore`+policy (2d) · arch-review bound this cycle (1d) · **engine = Qwen3/GGUF primary + BGE-M3/ONNX fallback + reported** (5a, operator) · **Sentrux baseline never removed, band-as-diagnostic, arch-review-gated rebaseline** (2e, operator).

# Build order for /04-doti-tasks
**Phase 0** (ChangeSetContext, StaleReason refactor, FeatureArtifactScope) → **Phase 1** (refresh classifier+command, recovery plan+nextActions, new-feature scoping, review binding [paired with refresh], drift patch loop) → **Phase 2** (ReviewContext, arch-review/drift-review consume, docs-only gate skip, Sentrux source-scope, band+two-try+rebaseline) → **Phase 3** (gate-proof status, release-train drift) → **Phase 4** (templates single-source) → **Phase 5** (Hx.Embedding.Core dual-engine, ModelLocator, drift-candidates, utility skills). P5 is last + always advisory.

# Risks
- **1d migration hazard:** adding `produces` to empty-hash review stages staling in-flight proofs — mitigated by sequencing 1a (`--apply-safe`) before 1d.
- **2d baseline coupling shift:** `.sentruxignore` change moves the committed baseline's coupling set — the regression gate must tolerate (baseline kept, never auto-recreated).
- **5a engine footprint:** LLamaSharp.Backend.Cpu + ONNX native backends are per-RID vendored binaries — pin+hash like the other tools; never bundle the GGUF/ONNX models in the nupkg.
- **Breadth:** 42 FRs across 6 phases — the phasing is the mitigation; P1 lands the proof-integrity core first.
