# 028 — Plan: self-describing, agent-gated cycle reconciliation

Scope: FULL (Option B) — the hybrid agent-gated reconciliation engine + the code-generated action model, release-trained with 027. **Revised after `/04-arch-review`** (7 BLOCKERs + 8 HIGHs resolved; see [the arch-review record](../reviews/028-agent-gated-reconcile-arch-review.md)). Spec: [028-agent-gated-reconcile.md](../specs/028-agent-gated-reconcile.md).

## Existing-architecture assessment (corrected)

- **Cycle engine — `Hx.Cycle.Core`** (leaf; refs `Hx.Tooling.Contracts` + `Hx.Runner.Core.Process`): `StageModel` (from `workflow.yml`; `CycleStage` has **no `Next`** today), the `CycleService` partials, `FreshnessEvaluator`, `RestampSafetyClassifier`, `CycleRecoveryPlanner` (`Project` → `StageRecoveryStep` with `Safety` + **`NextCommand`** — the existing single next-step projection), `CanonicalArtifactHasher`, `CycleStateStore` (writes the whole `CycleState` in one `Write`). `CycleService.Stamp` (`Stamp.cs:14-44`) `EnsurePrerequisitesFresh` checks only the target's *prerequisites* (`Check.cs:67`), never the target's own freshness; `Status()` evaluates a stage's own proof via `FreshnessEvaluator.Evaluate` (`Check.cs:25-31`). `CascadeSafeRebindAfterStamp` (`Stamp.cs:56-86`) runs after every stamp.
- **CLI — `Hx.Runner.Cli`**: parse → delegate → render `CliResult`; `CliNextAction` (in `Hx.Tooling.Contracts`) via `CliResults`; error codes `ErrorCodes.Validation_Cycle*`. The recovery menu is already sourced from `CycleRecoveryPlanner.Project` (`RunnerCommands.DotiCycle.cs:84/105/126`). `Hx.Cli.Kernel` is domain-agnostic (Contracts only).
- **Renderer — `Hx.Doti.Core`** (refs **Contracts, Runner.Core, Version.Core — NOT `Hx.Cycle.Core`**; the original plan's assertion was false): `DotiRenderer` (`{operatorQuestionProtocol}`-style placeholder substitution), `SkillMarkdownRenderer` (`ResolveSkillIdentity` falls back to `skill.NextStage` for the 7 utility skills), `DotiWorkflowRegistry` (hand-maintained 9-stage `NextStep`/`AlternateActions` + presentation strings `StageStatus`/`DisplayTitle`), `DotiWorkflowDescribe`.
- **Other CLIs with payload-derived `nextActions`** (NOT cycle-state functions; stay locally built): `Hx.Impact.Cli` (`ImpactCommands` — `AffectedOutcome` + `data.files`/`selectedTests`), `Hx.Scaffold.Cli` (`ScaffoldCommands.Prereq` per-install string; `DotiInstall` hook paths). Neither references `Hx.Cycle.Core`.
- **Conformance:** ArchUnit `cliSurfaceConfinement`/`cliDelegation`; Sentrux 120 + layer boundaries; commits owned by coded transitions.

## Design decisions

**D1 — Rebind = content-binding re-stamp; decision = the verb, which evaluates its target directly.** Verb `doti cycle review-rebind --target <stage> --attest no-impact [--reason]`. `CycleService.ReviewRebind(target)` reads the **target's own** freshness via `FreshnessEvaluator.Evaluate(targetProof, requireChangeSetIdentity)` (B2 fix — `Check(target)` only sees prerequisites). *Alt rejected:* the full snapshot model (rebase-carve-out hazard) and bare `Stamp` (no fence/record).

**D2 — The eligibility fence lives INSIDE `Stamp` as one pure predicate (B1, the headline fix).** A pure `ReviewRebindEligibility.IsAttestable(StageFreshnessResult, CycleStage)` = `PrereqArtifactChanged && !review-kind && !requireChangeSetIdentity`. `CycleService.Stamp` distinguishes a real re-author (own-artifact hash changed) from an *attestable* prereq-only rebind (own unchanged, only prereq hashes diverge) and **refuses** the latter (throws → `Validation_CycleReviewRebindRequiresAttest`, routing to the verb). Only `ReviewRebind` may clear it, recording the verdict. `Refresh --apply-safe` keeps applying only `SafeReinterpret`/`ReBindContentEqual`. *Alt rejected:* fence beside `Stamp` (in the planner only) — a bare `doti cycle stamp --stage plan` bypasses it (`Stamp.cs:25,27-28,41`), reopening the exact rubber-stamp.

**D3 — Audit record in a dedicated `ReviewedRebinds` field; one atomic write (B3).** Add `IReadOnlyList<CycleReviewedRebindRecord>? ReviewedRebinds = null` to `CycleState` (additive nullable trailing, like `CompletedUnreleasedCycles`) — NOT `Transitions` (typed `CycleTransitionRecord`, release-train-scanned). Built + persisted in one `CycleStateStore.Write`. *Alt rejected:* shoehorning into `Transitions` (won't compile/deserialize; pollutes the train scan).

**D4 — Diff surfacing at the recovery/CLI seam, null-safe (FR-002, H2/H3).** The changed-path SET comes from the pure `FreshnessEvaluator`/`CycleRecoveryPlanner` (on `StageRecoveryStep`); the line-level `git diff <StampedAtCommit>..HEAD -- <paths>` runs at the **CLI/recovery-presentation seam** (`RunnerCommands` / a `Hx.Runner.Core.Process` call), lazily, with a worktree fallback when `StampedAtCommit` is null. *Alt rejected:* git on the pure `FreshnessEvaluator` (couples the leaf to process exec; brittle SC-004).

**D5 — Cascade suppressed for an attested rebind (B7).** `ReviewRebind` rebinds the target only and does NOT trigger `CascadeSafeRebindAfterStamp`; each further-downstream stage surfaces for its own attestation. *Alt rejected:* let the cascade run (one record silently flips multiple stages Fresh) or record every cascade descendant (couples the verb to the cascade; the agent-decides-each model is cleaner).

**D6 — One `DotiActionModel`, scoped to the workflow-affordance class, wrapping the single recovery projection (B4/B5/H6/H8).** The model (descriptors + declarative applicability + `DotiActionProjector`) lives in `Hx.Cycle.Core`. Recovery-menu descriptors **wrap** `CycleRecoveryPlanner.Project` (tag each `StageRecoveryStep` with its descriptor id) — never a second evaluator (H6, the 027 disagree hazard). Rendering adds an **explicit reviewed `Hx.Doti.Core → Hx.Cycle.Core` edge** (acyclic; both `order=1`); the descriptor→`CliNextAction` mapping lives in `Hx.Runner.Cli` so `CliResults`/the kernel stay domain-agnostic. Descriptors carry affordance metadata (id, invocation template, label/why, `When`); deep presentation (`DotiWorkflowRegistry`'s stage status/title) is rendered at `Hx.Doti.Core` from `StageModel` + descriptors (H8 — keep heavy presentation out of the engine leaf). **Scope** = stage advance, recovery menu, attest verb, publish STOP (`Command=null`), train-loop, bug-phase, the 7 utility skills (`Utility` descriptors — B6). Payload-derived next-actions (`ImpactCommands`/`Scaffold`) stay locally built (B5). *Alt rejected:* "zero hand-authored anywhere" (unsatisfiable — payload data isn't a cycle-state function; would force `Impact.Cli→Cycle.Core` edges).

**D7 — `next:` for all stages + single-source the graph (FR-007).** `+ CycleStage.Next` + the `StageEntry` binder + `next:` on all 9 stages; de-dup the `CheckHelpers` DFS into `StageModel.TransitivePrereqStages`. Additive within schemaVersion 2.

**D8 — §1 `Self-describing automation` invariant** (FR-001).

## Module breakdown (one concern per file; within Sentrux 120)

| Type | Project | File | Responsibility |
|---|---|---|---|
| `ReviewRebindEligibility.IsAttestable` | `Hx.Cycle.Core` | `ReviewRebindEligibility.cs` | the ONE pure compound fence predicate (git-free testable — H4) |
| `CycleService.ReviewRebind` | `Hx.Cycle.Core` | `CycleService.ReviewRebind.cs` (new partial) | evaluate target → fence → content-rebind → `ReviewedRebinds` → one write; cascade suppressed |
| `CycleReviewedRebindRecord` + `ReviewedRebinds` + `ReviewedNoImpactRebound` | `Hx.Tooling.Contracts` | `CycleState.cs` | additive contract |
| `RestampSafety.ReviewedNoImpact` + decay arm | `Hx.Cycle.Core` | `RestampSafetyClassifier.cs` / `FreshnessEvaluator.cs` | classification + re-derive decay |
| `CommandDescriptor`/`Applicability`/`CommandContext`/`DotiActionModel`/`DotiActionProjector` | `Hx.Cycle.Core` | `Actions/*.cs` | the model + projector; recovery descriptors wrap `CycleRecoveryPlanner.Project` |
| descriptor→`CliNextAction` mapping | `Hx.Runner.Cli` | `CliActionRendering.cs` | keeps kernel domain-agnostic |
| `CycleStage.Next` + binder | `Hx.Cycle.Core` | `StageModel.cs` | declared successor edges |
| a `CycleService` test seam | `Hx.Cycle.Core` | `CycleService.cs` | inject `CycleStateStore`/`StageModel` so the verb + SC-007 are testable without a real git repo (H5) |

Renderer (`Hx.Doti.Core`, with the new edge): `DotiRenderer` `{commandAvailability}`; `SkillMarkdownRenderer.ResolveSkillIdentity` + "Next stage:" + stage status/title rendered from the projector + `StageModel`; `DotiWorkflowDescribe` re-targeted; `DotiWorkflowRegistry` deleted (its presentation data rehomed in the renderer projection — H1).

## Architecture delta

- **New project edge:** `Hx.Doti.Core → Hx.Cycle.Core` (explicit, reviewed; acyclic — Cycle.Core never re-enters Doti). `Impact.Cli`/`Scaffold.Cli` get NO new edge.
- **CLI confinement preserved:** the verb delegates to `CycleService.ReviewRebind`; the descriptor→`CliNextAction` mapping is CLI-layer; `CliResults`/`Hx.Cli.Kernel` stay domain-agnostic. `cliSurfaceConfinement`/`cliDelegation` unchanged.
- **Test-enforced single-source invariant (affordance-scoped):** a guard test asserts no hand-authored `CliNextAction` for the workflow-affordance class outside the mapping (explicit allow-list for payload-templated sites), + registry static-invariants (one advance/stage; no overlap; no empty decision point).
- **Sentrux/layers:** the model is data + small helpers (within 120); deep presentation stays in `Hx.Doti.Core`, not the engine leaf (H8).
- **Fail-closed:** the fence is inside `Stamp`; `ReviewedNoImpact` is excluded from auto-`Refresh`; nothing enforced→advisory.

## File-by-file

`Hx.Tooling.Contracts/CycleState.cs` (+ `ReviewedRebinds`, `CycleReviewedRebindRecord`, `ReviewedNoImpactRebound`); `Hx.Cycle.Core` (`FreshnessEvaluator` decay arm + changed-path set on result; `RestampSafetyClassifier`/`CycleRecoveryPlanner` `ReviewedNoImpact` + descriptor-id tagging; `CycleService.Stamp` in-fence; `CycleService.ReviewRebind.cs` new; `ReviewRebindEligibility.cs`; `Actions/*`; `StageModel.cs` `Next`; `CheckHelpers` DFS removed; the test seam); `Hx.Runner.Cli` (the verb + factory; `CliActionRendering`; workflow `nextActions` → model); `Hx.Doti.Core` (`+ Cycle.Core` ref; `DotiRenderer` `{commandAvailability}`; `SkillMarkdownRenderer`; `DotiWorkflowDescribe`; delete `DotiWorkflowRegistry`); `.doti/core/workflows/doti/workflow.yml` (`next:` ×9); `skills.json` (`nextStage` deleted; utility skills as descriptors); `profile.json`; `agent-context-template.md` (`{commandAvailability}` + Workflow-Rules prose); `errorcodes/registry.json` (`Validation_CycleReviewRebindRequiresAttest`/`Ineligible`/`NotStale`); `.doti/core/templates/commands/*` (FR-008 + FR-001).

## Test strategy

Pure-unit (git-free): `IsAttestable` matrix (SC-002 blocked cases); each named `Cond`; `Applicability.Describe()` == `Evaluate` (no-drift); the projector per-state; the descriptor-id tagging matches `CycleRecoveryPlanner`. Engine (via the test seam): `ReviewRebind` evaluates the target's own freshness; bare `Stamp` on an attestable stale throws; `Refresh --apply-safe` never applies `ReviewedNoImpact`; the record in `ReviewedRebinds`; decay (SC-001/003); the one-write rebound state (SC-007). FR-009 matrix locks. **H7 gating task:** capture a byte-stable golden baseline of the current rendered skills/agent-context BEFORE the render migration; assert render-from-model == baseline per surface; `render-skills --check` + `payload check` green. Registry static-invariants + the affordance-scoped guard (SC-010/011).

## Risks

- **Byte-stable render regression** (#1) — mitigated by the H7 golden baseline as a gating task before migrating any surface, surface-by-surface.
- **The new `Doti.Core → Cycle.Core` edge** pulls Cycle.Core's transitive closure into the renderer — confirm no Sentrux/ArchUnit boundary break + acyclicity in implement.
- **Registry as single point of truth** — per-`Cond` tests + static-invariants.
- **Scope** (engine + migration in one trained cycle) — the freshness engine + the action model are independently testable; sequence the render migration behind the golden baseline.

## Resolved arch-review items

B1→D2, B2→D1, B3→D3, B4→D6+delta, B5→D6 scope, B6→D6 utility descriptors, B7→D5; H1→renderer rehome, H2/H3→D4, H4→`ReviewRebindEligibility`, H5→test seam, H6→D6 wraps single projection, H7→gating golden baseline, H8→presentation in `Hx.Doti.Core`. Remaining open (for `/05-tasks` / `/06-analyze`): TOCTOU (deferred, Out-of-scope); the `--reason` stays optional; delete `DotiWorkflowRegistry` outright (no shim).
