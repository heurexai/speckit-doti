# Feature Specification: Doti Review Recovery and Change Context

**Slug**: `008-doti-review-recovery-and-change-context`
**Priority mode**: Mixed — dominant mode is **workflow/tooling** (fail-closed safety + deterministic proof before ergonomics). Doti-prose/template changes follow source-of-truth + rendered-asset consistency. Advisory semantic candidate search is ergonomics, ordered last and never gating.

## Goal

Improve the Doti cycle so architecture review, drift review, template packaging, and post-review recovery are grounded in deterministic change context, while allowing advisory semantic search to narrow review effort. The workflow must reduce stale-stamp loops without weakening proof freshness, and must keep `.doti/core/templates` as the single source for Doti templates.

## Prioritised work items (workflow/tooling — safety/proof before ergonomics)

`/04-doti-tasks` orders the build by these bands, not MVP-first.

- **P1 — fail-closed proof integrity (load-bearing):** safe-refresh refusal + read-only recovery plan (FR-001, FR-005, FR-006, FR-007); review-stage artifact binding (FR-003, FR-004); the in-cycle drift patch loop (FR-012, FR-013); new-feature-start change-set scoping (FR-038).
- **P2 — reusable deterministic change/review context:** change-set context (FR-008–FR-011); the review-context command (FR-025); arch-review + drift-review consume it and constrain scope (FR-010, FR-026, FR-027); scoped gate/Sentrux (FR-028–FR-032).
- **P3 — release-train completeness:** per-feature gate-proof reporting (FR-036) and cross-feature release-train drift (FR-037). *(FR-033/034/035 already exist — see Release-train validation.)*
- **P4 — single-source templates:** `.doti/core/templates` authoritative + materialized-payload parity (FR-014–FR-017).
- **P5 — advisory ergonomics (last, never gating):** semantic candidate finder (FR-018–FR-021, FR-039–FR-041); unnumbered utility skills (FR-022–FR-024); `nextActions` hints (FR-002).

## Scope

In scope:
- Reusable change-set context for review stages, especially `arch-review` and `drift-review`.
- Structured cycle recovery planning for stale or missing stage proofs.
- Artifact binding for review stages before safe restamp/refresh.
- In-cycle patching after drift review finds code/spec/doc gaps.
- Single-source Doti templates: source uses `.doti/core/templates`; packaged/install payload materializes `.doti/templates`.
- Advisory semantic candidate finding to suggest likely review areas and missing patches.
- Optional unnumbered utility skills for workflow amendment and drift patching.

Out of scope:
- Replacing gate proof, render checks, payload checks, or human review with semantic AI.
- Allowing semantic search results to mark a review clean.
- Changing normal `/01` through `/09` stage ordering.
- Downgrading the spec to match incomplete code unless the operator explicitly starts a new specify decision.

## Functional Requirements

- **FR-001**: The system MUST provide a read-only cycle recovery plan for a target stage, showing stale or missing prerequisites, reasons, safe-to-restamp status, required reruns, and exact next commands.
- **FR-002**: `doti cycle check` failures SHOULD include structured `nextActions` pointing to the recovery plan or required stage rerun.
- **FR-003**: Review stages used as downstream proof MUST be bound to durable artifacts before automatic refresh is allowed; at minimum this applies to `arch-review` and `drift-review`.
- **FR-004**: `analyze` — a downstream-relied review prerequisite — MUST produce a durable report artifact (e.g. `docs/reviews/{feature}-analyze-report.md`, or anchored on the `converge` coverage output) and be artifact-bound like `arch-review`/`drift-review`, so it is safe-refreshable (FR-006) and never falls into the missing-binding manual-re-stamp loop.
- **FR-005**: Automatic refresh MUST refuse to restamp a review stage when the required artifact is absent, stale, or not produced by the relevant review.
- **FR-006**: Safe automatic refresh MUST be limited to cases where the old proof can be deterministically reinterpreted, such as runner metadata/schema changes with unchanged inputs.
- **FR-007**: Safe automatic refresh MUST NOT restamp when spec, plan, tasks, implementation diff, review artifact, or gate inputs changed in a way that requires rereview or rerun.
- **FR-008**: The system MUST expose reusable change-set context for review consumers beyond current `arch-review` usage.
- **FR-009**: Change-set context MUST include changed files, status/rename/delete metadata, base/head identity, working tree inclusion, affected source projects where applicable, and fail-closed behavior when refs cannot resolve.
- **FR-010**: `drift-review` MUST use the reusable change-set context when available, instead of relying primarily on manual `git diff`/`git status` instructions.
- **FR-011**: Change-set context MAY provide stage-specific guidance, but the underlying deterministic data MUST remain reusable across stages.
- **FR-012**: Drift review MUST define an in-cycle patch loop for implementation gaps: fix code/docs/assets to satisfy the current spec, rerun required proof, refresh implement state, rerun affected drift axes, then stamp drift-review.
- **FR-013**: Drift review MUST treat the spec as the source of truth unless the operator explicitly starts a new specify/plan decision to change requirements.
- **FR-014**: `.doti/core/templates` MUST be the authoritative source for Doti templates in this repo.
- **FR-015**: The source repo SHOULD NOT require committed duplicate `.doti/templates` content.
- **FR-016**: The package/install payload MUST still include install-facing `.doti/templates` materialized from `.doti/core/templates`.
- **FR-017**: Payload/render parity checks MUST prove materialized templates match `.doti/core/templates`, even when source `.doti/templates` is absent.
- **FR-018**: The system MAY provide a read-only semantic candidate finder for drift/review narrowing.
- **FR-019**: Semantic candidates MUST include evidence snippets, confidence, affected axes, and suggested deterministic checks.
- **FR-020**: Absence of semantic candidates MUST NOT be treated as proof that review is clean.
- **FR-021**: Semantic retrieval MUST be **local/private only** — it runs an on-machine model via the **ONNX runtime on CPU** and MUST NOT send source code, docs, or secrets to any external provider. This feature defines no redaction or external-approval path; an external provider, if ever wanted, is a separate future cycle with its own design.
- **FR-039**: The local semantic embedding model MUST run as ONNX on CPU, be pinned + hash-verified like the other vendored tools, and be selected to maximize semantic-capture quality within CPU-runnable latency bounds. **Target: Qwen3-Embedding-0.6B** (code-aware), with **BGE-M3** as the fallback; the exact model is confirmed in `/03-plan` via a feasibility spike (ONNX export + C# tokenizer + CPU latency), leveraging and customising the existing ONNX implementation in `polaris-core` (`Hx.Semantic.Core` / `Hx.Discriminate.Core`).
- **FR-040**: The ONNX model loader/runner MUST be a **standalone library**, decoupled from the doti CLI and workflow code, so it is independently reusable — including potential reuse by the scaffolded app.
- **FR-041**: The local model-store root MUST NOT be hardcoded. It MUST be resolved from **local configuration** or the **`HEUREX_LLM_ROOT`** environment variable; when both are present, **configuration wins**; when neither is present, resolution MUST **fail hard** (fail-closed).
- **FR-022**: Optional amendment/patch skills MUST be unnumbered utilities, not part of the normal numbered path.
- **FR-023**: Utility skills MUST classify changes as active-feature scope, Doti prose, generated-code template, runtime code, docs-only, or future-only workflow improvement.
- **FR-024**: Utility skills MUST use cycle recovery planning rather than guessing which stamps to refresh.
- **FR-025**: The system MUST expose a review-context command for review and gate consumers, including changed files, affected projects, file categories, applicable lenses, skipped lenses, and escalation reasons.
- **FR-026**: Architecture review MUST consume review-context and constrain every lens to affected scope unless the context explicitly requires broader review.
- **FR-027**: During implementation or drift patching, architecture review MUST rerun only when patches change architecture-relevant surfaces: contracts, CLI shape, dependency direction, project boundaries, layering, persistence, security boundaries, generated-code templates, or cross-module behavior.
- **FR-028**: Gate planning SHOULD avoid full architecture/Sentrux validation for docs-only and Doti-prose-only changes where no runtime or generated code is affected.
- **FR-029**: Sentrux checks MUST be source/code scoped by default and exclude docs, Doti prose, rendered skills, payload metadata, and non-code templates unless explicitly configured as code.
- **FR-030**: Sentrux degradation above the hard tolerance but within an escalation band, such as 1.3x, MUST trigger structural architecture review after two documented optimization attempts.
- **FR-031**: Sentrux rebaseline MUST require explicit operator intent plus spec/plan/arch-review evidence that the new structure is desired.
- **FR-032**: Release gates MUST remain fail-closed for unresolved Sentrux degradation unless an approved rebaseline is part of the cycle.

### Multi-Spec Release Train

The workflow MUST support completing multiple numbered Doti feature cycles before a single release. Only one feature cycle is active at a time, but completed `drift-review` cycles may be preserved as completed-unreleased release-train entries while the operator starts another numbered `specify`.

- **FR-033**: After a completed `drift-review`, the workflow MUST allow the next action to be either `release` or a new numbered `specify`.
- **FR-034**: Starting a new `specify` after `drift-review` MUST preserve the prior feature as completed-unreleased rather than forcing immediate release.
- **FR-035**: `/09-doti-release` MUST aggregate all completed-unreleased feature cycles into one release train.
- **FR-036**: Release proof MUST report every included feature slug, completion stage, commit range, task-completion status, gate-proof status, and inclusion/blocker status.
- **FR-037**: If a later completed feature invalidates docs, assets, behavior, or release assumptions from an earlier completed-unreleased feature, release validation MUST surface that as release-train drift before release.
- **FR-038**: Starting a new numbered feature from a completed `drift-review` MUST scope the **prior** feature's freshness and transition-readiness to that prior feature's own change-set. The incoming feature's not-yet-owned artifacts (for example its spec file) MUST NOT stale the prior feature's stamps or block the transition, and a clean new-feature start MUST NOT require a manual clean-tree workaround.

## Success Criteria

- **SC-001**: Given stale prerequisites, a read-only refresh plan reports each stale stage, reason, safety classification, required rerun, and exact next command.
- **SC-002**: Given changed spec/plan/tasks inputs, safe refresh refuses automatic restamp and requires the dependent stages to rerun.
- **SC-003**: Given a review stage without its required artifact, safe refresh refuses to restamp it.
- **SC-004**: Given a code gap found during drift review, the workflow supports patching in the same cycle, rerunning proof, refreshing implement state, and restamping drift-review without a stale-loop dead end.
- **SC-005**: Given Doti-prose/docs-only changes, review triage skips irrelevant code lenses while still checking rendered skills and payload parity.
- **SC-006**: Given source `.doti/templates` removed, package/install payload still contains `.doti/templates` matching `.doti/core/templates`.
- **SC-007**: Given a `.doti/core/templates` update, the packaged install template changes without requiring a second committed copy.
- **SC-008**: Given renamed or behavior-changing code, semantic candidates can point to likely docs/help/tests/rendered assets to inspect, but deterministic checks remain required.
- **SC-009**: Gate/release proof cannot be satisfied by semantic candidate output alone.
- **SC-010**: Optional utility skills appear unnumbered and do not reorder `/01` through `/09`.
- **SC-011**: Given a docs-only change, review-context marks code lenses, Sentrux, and architecture tests not applicable, while render/payload checks remain applicable.
- **SC-012**: Given a patch touching one CLI command handler and tests, architecture review activates only CLI/delegation/testability lenses for affected files.
- **SC-013**: Given a patch adding a new dependency direction, architecture review activates layering/modularity/blast-radius lenses before gate.
- **SC-014**: Given Sentrux degradation within the escalation band after two optimization attempts, the workflow emits a structural-review next action instead of asking for another blind optimization pass.
- **SC-015**: Given a rebaseline request without spec/plan/arch-review justification, the command refuses and explains why it is unsafe.
- **SC-016**: Given source-only Sentrux scope, `.md`, `.doti` prose, rendered skills, payload files, and non-code templates are not scanned.
- **SC-017**: After feature A completes `drift-review`, starting feature B finalizes A as completed-unreleased and starts B without requiring a release.
- **SC-018**: A release after two completed specs reports both specs in the release train.
- **SC-019**: If feature B changes behavior documented by feature A, release validation requires the docs/spec consistency to be repaired before release.
- **SC-020**: Given feature A completed `drift-review`, starting feature B with B's spec already on disk succeeds in a single `specify` stamp — without staling A's `implement` (or any prior stamp) and without a manual clean-tree workaround.
- **SC-021**: Given a runner-metadata/schema bump with unchanged inputs, `analyze` is safely re-interpreted from its durable report artifact (no manual re-stamp), exactly as `arch-review` and `drift-review` are.

## Key Entities

- **ChangeSetContext** — the reusable deterministic record (FR-008/009): changed files with status/rename/delete metadata, base/head identity, working-tree-inclusion flag, affected source projects, and a fail-closed marker when refs do not resolve.
- **ReviewContext** — the review/gate-facing projection (FR-025): changed files, affected projects, file categories, applicable lenses, skipped lenses, escalation reasons.
- **CycleRecoveryPlan** — the read-only recovery record (FR-001): per-stage stale/missing prerequisites, reasons, safe-to-restamp classification, required reruns, exact next commands.
- **SemanticCandidate** — an advisory finding (FR-018/019): evidence snippet, confidence, affected axes, suggested deterministic checks. Never a proof input.
- **ReleaseTrainEntry** — a completed-unreleased feature in the train (FR-035/036): slug, completion stage, commit range, task-completion status, gate-proof status, inclusion/blocker status.

## Deterministic Surfaces

Existing command-backed surfaces (reused, not re-built):
- `hx impact plan --repo . --base <ref> --head HEAD --for arch-review --json`
- `hx doti converge ...`
- `hx doti render-skills --check`
- `hx doti payload check --repo .`
- `hx gate run --profile normal|release`
- `hx doti cycle status/check/stamp`

Planned/advisory surfaces (do NOT report as passing gates until implemented):
- `hx impact plan --for drift-review|change-context` (or an equivalent stage-aware change-context command).
- `hx doti cycle refresh-plan --target <stage> --repo . --json`
- `hx doti cycle refresh --target <stage> --apply-safe --repo . --json`
- `hx doti drift-candidates --base <baseRef> --json` (advisory, read-only).
- Optional unnumbered skills: `doti-amend`, and `doti-drift-fix` or `doti-patch`.

## Architecture and Hygiene Expectations

- Keep CLI entry points thin: parse, delegate, render. Reusable behavior lives in core libraries, not command templates.
- Preserve fail-closed behavior for unresolved git refs, stale proof, missing artifacts, and ambiguous recovery.
- Update workflow metadata so review artifacts are declared consistently.
- Update package/payload parity tests to validate materialized templates instead of committed duplication.
- Do not create or rely on a Sentrux baseline.
- If semantic retrieval is added, it must be advisory, read-only, evidence-producing, and unable to bypass deterministic checks.
- The ONNX model loader is a standalone, reusable library (decoupled from CLI/workflow), leveraging + customising `polaris-core`'s existing ONNX implementation (`Hx.Semantic.Core` / `Hx.Discriminate.Core` model-locator + engines). The model-store root resolves from config or `HEUREX_LLM_ROOT` (config wins; fail-closed when neither is set) — never hardcoded.

## Release-train validation (FR-033–FR-037, evidence-backed)

The multi-spec release-train model the spec relies on is **valid and consistent with the current code** (verified this cycle):

- **FR-033 / FR-034 / SC-017 — already implemented.** `CycleService.Transition.cs` allows starting a new numbered `specify` *only* from `drift-review`, and that transition appends the prior feature to `CompletedUnreleasedCycles` (preserved, not force-released). This spec's `specify` stamp exercises it directly: feature 007 (at `drift-review`) is finalized as completed-unreleased.
- **FR-035 / SC-018 — already implemented.** `CycleService.ReleaseTrain.CompletionRecordsForRelease` aggregates `CompletedUnreleasedCycles` plus the active release feature into one train.
- **FR-036 — mostly implemented; one gap (P3 work).** `FeatureForCompletion` already reports slug, completion stage, commit range (`baseRef..commit`), task-completion status, and inclusion/blocker status. **Per-feature gate-proof status is the gap this feature closes.**
- **FR-037 / SC-019 — new work (P3).** Cross-feature *release-train drift* (a later feature invalidating an earlier completed feature's docs/behavior/assumptions) is **not** detected today; this feature adds it.

So the release-train requirements are not re-implementing existing behavior except where explicitly noted — 008 preserves and tests FR-033/034/035 and builds FR-036's gate-proof reporting + FR-037's cross-feature drift.

## Assumptions

- `ChangeSetContext` builds on the existing `impact plan --for arch-review` machinery (FR-008/T040 lineage), generalised to additional stages — not a new git layer.
- "Safe automatic refresh" (FR-006) covers the *runner-metadata/schema* re-stamp case observed this cycle (the analyze/arch-review "missing prerequisite artifact binding" staleness), where inputs are unchanged — never an inputs-changed restamp (FR-007).
- The release-train requirements assume the verified existing behavior above; the new work is FR-036's gate-proof field and FR-037's cross-feature drift.
- FR-004 (analyze artifact binding) remains open for `/02-doti-clarify`; the conservative default is to bind `analyze` to a durable report artifact only if it is a downstream proof, else mark it refresh-unsafe.
- The semantic finder leverages and customises `polaris-core`'s existing ONNX implementation (verified present: `Hx.Semantic.Core` / `Hx.Discriminate.Core` — a model-locator + ONNX engines). The model-store root is resolved from config or `HEUREX_LLM_ROOT` (config wins; fail-closed if neither is set); the operator's `D:\LLM-Models` is supplied that way, never hardcoded.

## Clarifications

### 2026-06-27 (`/02-doti-clarify`)

- **New-feature-start change-set scoping** (raised + reproduced while starting 008): the new-feature-start transition staled the prior feature's `implement` proof because freshness was bound to the whole working tree, including the incoming feature's spec — requiring a manual clean-tree workaround. **Resolved → (A):** added **FR-038** (the transition MUST scope the prior feature's freshness/readiness to its own change-set; the incoming feature's artifacts MUST NOT stale prior stamps or require a clean-tree start) and **SC-020** (a one-stamp clean start with the new spec on disk).
- **Semantic retrieval trust boundary (FR-021)** → **(A) local/private only.** Retrieval runs a local model via the **ONNX runtime on CPU**; no source/docs/secrets leave the machine; no redaction/external path (an external provider would be a separate future cycle). Added **FR-039**: the model runs as ONNX on CPU, selected for maximal semantic capture within CPU bounds, and is pinned + hash-verified like the other vendored tools — the **specific model is open**, to be confirmed from a research-backed recommendation (the operator wants the strongest semantic-capture model).

- **Semantic model + loader infra (FR-039/040/041)** → **(A):** target **Qwen3-Embedding-0.6B** (code-aware), **BGE-M3** fallback; the exact model is confirmed via a `/03-plan` feasibility spike, **leveraging `polaris-core`'s ONNX implementation** (`Hx.Semantic.Core` / `Hx.Discriminate.Core` — verified present). The ONNX loader is a **standalone reusable library** (FR-040, potential scaffold reuse). The model-store root is resolved from **config or `HEUREX_LLM_ROOT`** (config wins; **fail hard if neither set**) — never hardcoded (FR-041). The operator's current root is `D:\LLM-Models` (verified present), supplied via config/env, not baked into the spec.

- **Analyze artifact binding (FR-004)** → **(A):** `analyze` produces a durable report artifact (anchored on the `converge` coverage output or a `{feature}-analyze-report.md`) and is bound like arch-review/drift-review, so it is safe-refreshable — added **SC-021**. Closes the `analyze` stale-loop reproduced this cycle.

_All `/02-doti-clarify` markers resolved._
