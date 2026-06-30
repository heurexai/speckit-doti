# Drift Review — 028-agent-gated-reconcile

**Stage:** /08-doti-drift-review
**Verdict:** CLEAN — no open drift in any applicable axis. Stamp authorized.
**Reviewed diff:** `cc3a0b4^..HEAD` (the four implement commits — `cc3a0b4` carries the bulk, `04cf7e8`/`80b6848`/`f0cf188` the in-cycle gate fixes). 82 files, +2703 / −258.

> Scope note: the cycle `baseRef` (`cc3a0b4`, the `analyze: 028` transition commit) *contains* the bulk implementation, so `baseRef..HEAD` alone shows only the three fix commits. The review scoped from `cc3a0b4^` — the commit before any 028 code landed — to cover the whole change set. (This is the known baseRef-scoping quirk; the implementation diff was taken from the true pre-implementation base.)

## Change set, by area

| Area | Files | What changed |
|------|-------|--------------|
| Engine — `Hx.Cycle.Core` | 19 | `ReviewRebindEligibility` (pure `IsAttestable`), `CycleService.ReviewRebind` (new partial verb), in-`Stamp` attestation fence, `FreshnessEvaluator` decay arm + `ChangedPrereqPaths`, `RestampSafetyClassifier`/`CycleRecoveryPlanner` `ReviewedNoImpact` tier, `StageModel.Next` + binder, `Actions/*` (DotiActionModel/Projector/descriptors), `CycleRecoveryDiff` |
| Contracts — `Hx.Tooling.Contracts` | 1 | `CycleStageOutcome.ReviewedNoImpactRebound`, `CycleReviewedRebindRecord`, nullable `ReviewedRebinds` |
| CLI — `Hx.Runner.Cli` | 6 | `review-rebind` verb wiring, `WorkflowNextActions`/`EnrichWithDiff`/`RecoveryNextAction` (split across two partials for fan-out), `CliActionRendering` |
| Renderer — `Hx.Doti.Core` | 9 | `DotiWorkflowRegistry` **deleted**; `DotiWorkflowPresentation`/`StagePresentation`/`CommandAvailabilityRenderer` added; `DotiRenderer`/`SkillMarkdownRenderer`/`DotiWorkflowDescribe` re-homed onto the model + `Hx.Cycle.Core` edge |
| Tests | 14 | `ReviewRebind{,Matrix Lock,Eligibility}Tests`, `DotiActionModelTests`, `CycleReconcileTests`, `CycleRecoveryDiffTests`, `WorkflowPresentationFixture`, migrated `DotiWorkflowRegistryTests` (×2), renderer/search proofs |
| Prose / payload | ~20 | `.doti/core/templates/commands/*` (FR-008 prompts + FR-001), `.doti/core/templates/constitution-template.md`, `.doti/memory/constitution.md` (§1), `workflow.yml` (`next:` ×9, source + installed), `agent-context.md` (`{commandAvailability}`), re-rendered `.claude`/`.agents` skills, `errorcodes/registry.json` (+3) |
| Docs | 4 | spec, plan, arch-review, tasks, analyze report; `CHANGELOG.md`, `README.md` |

## Axis 1 — spec ↔ code

Each FR is satisfied by a real enforcing mechanism, matches the approved arch-review/plan design (all 7 BLOCKERs resolved), and is locked by a test. Nothing was downgraded enforced→advisory; no `*.Core` logic leaked into `*.Cli` (CLI partials are parse→delegate→render).

| FR | Mechanism (where) | Lock |
|----|-------------------|------|
| FR-001 self-describing invariant | `### Self-describing Automation` in **both** `constitution-template.md` and active `.doti/memory/constitution.md` §1 | constitution render |
| FR-002 diff at the seam, null-safe | `CycleRecoveryDiff.Surface` + `EnrichWithDiff`/`RecoveryNextAction` surface line-level upstream diff only on the `ReviewedNoImpact` action; degrades to no-affordance on error | `CycleRecoveryDiffTests`, `CycleReconcileTests` |
| FR-003 verb reads target's own freshness | `CycleService.ReviewRebind` evaluates the target directly; refuses `NotStale`/`Ineligible` | `ReviewRebindTests` |
| FR-004 in-`Stamp` fence, one pure predicate | `ReviewRebindEligibility.IsAttestable`; `Stamp` throws `CycleReviewRebindRequiredException`→`Validation_CycleReviewRebindRequiresAttest` | `ReviewRebindEligibilityTests` |
| FR-005 record, one atomic write, cascade suppressed | `ReviewedRebinds` (nullable) + `CycleReviewedRebindRecord`; rebind binds target only, no downstream cascade | `ReviewRebindTests` |
| FR-006 propagation, target-only rebind | `FreshnessEvaluator` decay arm + `ChangedPrereqPaths`; recovery plan tags the single `ReviewedNoImpact` step | `ReviewRebindMatrixLockTests` |
| FR-007 `next:` ×9 + binder + DFS de-dup | `next:` on all 9 stages (source + installed `workflow.yml`); `StageModel.Next` binder | matrix-lock + render-check |
| FR-008 anti-rubber-stamp prompts | reminder + review-rebind affordance in `doti-{amend,drift-fix,plan,tasks,analyze,clarify}.md` | payload check |
| FR-009 matrix-lock tests | `ReviewRebindMatrixLockTests` enumerates the finite edit-matrix (spec-only / spec+plan / downstream-only…) | the test itself |
| FR-010 action model, workflow-affordance scope | `DotiActionModel`/`DotiActionProjector` over `CommandContext`; **wraps** the single `CycleRecoveryPlanner` projection (never a second evaluator); declarative named-condition applicability | `DotiActionModelTests` |

Design fidelity confirmed: the `Hx.Doti.Core → Hx.Cycle.Core` edge is the explicit, acyclic edge the plan declared (B4); `DotiWorkflowRegistry`'s presentation data was re-homed in the renderer projection (H1), not duplicated; payload-derived next-actions stay locally built (B5). The gate's `architecture-test` + `sentrux-check` (both green) prove the boundary holds.

## Axis 2 — code ↔ docs

Every code change moved with its describing doc: `CHANGELOG.md` + `README.md` (028 entry + `review-rebind` CLI-map row), `agent-context.md` (`{commandAvailability}`), the re-rendered `.claude`/`.agents` skills, the command templates, the constitution, and `errorcodes/registry.json` (+3 codes) all changed in the same diff. The removed `DotiWorkflowRegistry` symbol survives in **no** live source and **no** user-facing doc — the only mentions are prior-cycle historical records (026 plan/review) and the 028 plan that documents the deletion, plus a deliberate test-migration comment. No code change lacks a matching doc change.

## Axis 3 — source ↔ installed

Both parity authorities green: `doti render-skills --check` = ok, `doti payload check --repo .` = ok. The 9-stage `next:` chain, the `{commandAvailability}` agent-context block, and all rendered skills match their `.doti/core` source. No hand-edited installed skills.

## Gate

`hx gate run --repo . --profile normal` — **all 14 steps pass**: hygiene, gitleaks-verify, affected-change, sentrux-verify, task-completion, restore-build-test, architecture-test, no-velopack, no-source, skill-drift, doti-payload, sentrux-check, version-calculate, security-scan. Full suite green (Cycle 129, Runner 248, Doti 101, Scaffold 108, + Embedding/Templates/Cli.Kernel/Sentrux/Impact/Gate).

## In-cycle fixes made during implement (RCA, not symptom-patched)

1. **Sentrux file-level cycle** (`CycleService.Recovery.cs` ↔ new partials) — the new `RecoveryEvaluation recovery = …` declarations made Sentrux ambiguously resolve a shared `_store` back-edge. Fixed at root: `var recovery = …` (×4). Verified `cycles=0`.
2. **God-file fan-out 6→7** — the 028 additions pushed `RunnerCommands.DotiCycle.cs` over fan-out 15. Fixed by splitting the new surface into two single-responsibility partials (`DotiCycleReviewRebind.cs` verb, `DotiCycleReconcile.cs` projection), each < 16. Verified god-count back to 6.
3. **`hx describe` crash outside a doti repo** — the render migration made `DotiWorkflowDescribe.Build()` hard-depend on a repo `workflow.yml`, aborting the whole CLI tree in any dir without one (13 `UnifiedWorkflowSurfaceTests` failures). Fixed to degrade to an empty cycle workflow when no `workflow.yml` is present — the deleted registry never required a file. Verified Scaffold.Tests green.

## Reviewed observations (non-blocking)

- **`DotiWorkflowRegistryTests.cs` (×2) retain the deleted type's name.** Accepted as a deliberate, documented migration: the XML-doc comment states the tests assert the *same* order/branching/no-commit invariants the registry guaranteed, now projected from `DotiWorkflowPresentation`. Keeping the name signals contract continuity; no live reference to the deleted type. Future-cleanup candidate (rename to `*WorkflowPresentationTests`), not drift.

## Conclusion

The implementation reinforces the arch-reviewed design with real enforcing mechanisms, the docs and installed payload agree with the code on all three axes, and the gate is fully green. No spec↔code gap, no stale user-facing reference, no source↔installed divergence. **Drift-review stamp authorized.**
