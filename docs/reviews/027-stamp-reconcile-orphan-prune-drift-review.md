# 027 — Drift review: codified stamp reconciliation + orphan-prune

Scope: the 027 implementation change set — 9 production engine files (`FreshnessEvaluator`, `RestampSafetyClassifier`, `CycleRecoveryPlanner`, `CycleService.Refresh`/`.Stamp`/`.TransitionRecords`, `CycleState`, `DotiInstaller`, `DotiPayloadParityChecker`) + 24 tests + the docs sweep + a behavior-preserving `DotiInstaller` extraction. Triage: a workflow/tooling change with real runtime logic (freshness classification + on-stamp cascade + install orphan-prune) — all code lenses applicable; verified sound by the implementing multi-agent pass (744 tests green, mustFix empty, safetyHolds true).

> baseRef note: the bulk of the 027 work was committed under the analyze→implement transition (`7c9b71e`), which the engine then adopts as the cycle baseRef, so `git diff baseRef..HEAD` surfaces only the later Sentrux refactor + doc-drift fixes. The review below covers the full 027 change (the engine work at/under baseRef plus the two follow-on commits), not just the narrow diff window.

## Axis 1 — spec ↔ code

- **FR-001/002/003** (safe-rebind invariant): `FreshnessEvaluator` emits the new `StaleReason.PrereqRebindable` ONLY when the own-artifact arm did not fire, the stage is not change-set-bound, and divergence is the prereq set with byte-identical shared-path content; `RestampSafetyClassifier` maps it to `ReBindContentEqual`; `CycleRecoveryPlanner` downgrades it to `RerunRequired` unless every producing upstream is `Fresh` and the dependent is not review-kind. A genuine content change, a review-kind stage (arch-review/analyze/drift-review), and a change-set-bound stage (implement/drift-review/release) can NEVER reach the auto-rebind path — verified by `ReviewKindRebindGuardTests` + `ImplementChangeSetLockTests`. No enforcement downgraded enforced→advisory; logic lives in `*.Core`.
- **FR-004** (`inserted-stage` verdict): a current-graph-required stage absent from cycle-state surfaces its `/NN` skill, never an auto-stamp.
- **FR-005/006/007** (reconcile wiring + auto-cascade): `CycleService.Refresh` iterates both `SafeReinterpret` and `ReBindContentEqual`, re-deriving after each; `Stamp` auto-invokes the safe-only cascade over dependents (re-entrancy-guarded, failure-isolated); `RebaseProofsToHead` recomputes `PrerequisiteArtifactHashes` so a transition no longer leaves a false `PrereqArtifactChanged`.
- **FR-008/009** (orphan-prune): `DotiInstaller.PruneOrphanedManagedSkillDirs` deletes only baseline-clean managed `*-doti-*` dirs the new render no longer targets (operator-edited orphans preserved/blocked, recorded in `ObsoleteAssets`); `DotiPayloadParityChecker` flags a surplus skill dir. Sourced from an on-disk scan so a pre-category repo (agentx class) self-heals on next update.
- The Sentrux refactor (`FinalizeManagedBaseline` extracted from `Install`) is behavior-preserving: same obsolete sweep + baseline write + gitignore effects + payload-version stamp, same call order — build + full test suite green afterward.

## Axis 2 — code ↔ docs

- **Drift found and FIXED:** `.doti/core/templates/agent-context-template.md` (the rendered `agent-context.md` capability line) still described the OLD two-tier recovery — *"`refresh --apply-safe` re-stamps ONLY the `SafeReinterpret` stages and refuses the rest"* — which 027 makes false. Corrected to document the `ReBindContentEqual` tier (and that `--apply-safe` now re-stamps it too), the `inserted-stage` verdict, and the auto-cascade. Re-rendered.
- `README.md` table row for `refresh --apply-safe` tightened (it now rebinds safe-to-reinterpret **and** content-equal proofs); the headline 027 note (auto-rebind + orphan-prune, with the slug) was already present.
- `CHANGELOG.md` + `.doti/core/templates/commands/doti-amend.md` carry the 027 behavior (T018). `doti-amend` documents the now-automatic cascade (the hand-re-stamp treadmill is gone).
- The `FinalizeManagedBaseline` extraction is internal (private helper) — no public surface, no doc impact. No removed/renamed public symbol leaks into any doc.

## Axis 3 — source ↔ installed

`hx doti render-skills --check` → no drift; `hx doti payload check --repo .` → parity passed (93 managed files). Source `.doti/core` edited + re-rendered; no hand-edited installed skill; entrypoints thin.

## Gate

`hx gate run --profile normal` → **success** (all 14 steps). `hx gate run --profile release` → **success** (15 steps, incl. `release-documentation` — 027 slug confirmed in README + CHANGELOG). Full suite green.

## Note — Sentrux offender surfaced at the post-commit gate (transparency)

The 027 orphan-prune call pushed `DotiInstaller.Install` to ~127 lines, over the Sentrux `max_fn_lines` (120) cap. Because Sentrux measures **committed** production code, this read green pre-commit and only failed at the post-implement gate. Fixed by extracting `FinalizeManagedBaseline` (`Install` → 105 lines) in a sanctioned commit on top (BaseRef preserved, not amended). This drift-review record was written during the same forward recovery (after the release transition fired); on the feature→`dev` squash it flattens into the single cycle commit.

## Verdict

**Clean** across all three applicable axes (one code↔docs drift found and fixed in the source, not deferred); gate green on both normal and release profiles. Ready for `/09-doti-release`.
