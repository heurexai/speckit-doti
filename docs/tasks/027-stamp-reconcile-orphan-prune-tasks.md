# 027 — Tasks: Codified stamp reconciliation + orphan-prune

Priority Mode: workflow/tooling — fail-closed safety + deterministic proof first. The safe-rebind invariant lands before the auto-cascade that relies on it; tests pair with each change.

## Phase 1: Freshness + classification (the safe invariant) [W1]

- [x] T001 Add `StaleReason.PrereqRebindable` and split the prereq arm in `tools/Hx.Cycle.Core/FreshnessEvaluator.cs`: emit it ONLY when the own-artifact arm did not fire (own content unchanged), the stage is not change-set-bound, and the divergence is the prereq set with byte-identical shared-path content; otherwise keep `PrereqArtifactChanged`. (FR-001) <!-- doti-task-hash: 20623634cba1254f76736cf1e1d1226be10fe79bfdfb7cee6627004871924cb4 -->
- [x] T002 Add `RestampSafety.ReBindContentEqual` and map `PrereqRebindable -> ReBindContentEqual` in `tools/Hx.Cycle.Core/RestampSafetyClassifier.cs`; leave OwnArtifactChanged/PrereqArtifactChanged/ChangeSetDiffers -> RerunRequired and Missing* -> SafeReinterpret; keep pure/total. (FR-002) <!-- doti-task-hash: 6b25f06086185d758dff3e5414be9118104df6d8c00f294565f67a4d37d2519d -->
- [x] T003 In `tools/Hx.Cycle.Core/CycleRecoveryPlanner.cs`: downgrade a `ReBindContentEqual` step to `RerunRequired` unless every producing stage in its transitive closure is `Fresh` in the same report AND the dependent is not review-kind; emit a distinct `inserted-stage` verdict (recommended cmd `/{stage.Command}`) for a current-graph-required stage absent from cycle-state; map ReBindContentEqual's next-cmd to `doti cycle refresh --apply-safe`. (FR-003, FR-004) <!-- doti-task-hash: 80e1dc670a672c8e0b75c1375e41f1efbb78aad8f2f6efaf84f126ba25c51eba -->

## Phase 2: Reconcile wiring [W2]

- [x] T004 `tools/Hx.Cycle.Core/CycleService.Refresh.cs`: iterate `SafeReinterpret` AND `ReBindContentEqual`; re-derive the plan after each safe re-stamp so a chain settles in one pass. (FR-005) <!-- doti-task-hash: de2f60ad3b03a594b54efb49a64fcc7c45165568958b1a187880230ff9d4f0e7 -->
- [x] T005 `tools/Hx.Cycle.Core/CycleService.Stamp.cs`: after `_store.Write`, auto-invoke the safe-only cascade over the stamped stage's dependents (closure-bounded, prereq-first, ambient re-entrancy guard); never auto-stamp RerunRequired/ChangeSetDiffers/inserted; isolate so a cascade failure never fails the primary stamp. (FR-006) <!-- doti-task-hash: 0989855fc441d599810ab406cc3fd97eefab24dc2c1ee31f3bd5c8fe1451f880 -->
- [x] T006 `tools/Hx.Cycle.Core/CycleService.TransitionRecords.cs`: in `RebaseProofsToHead` also recompute `PrerequisiteArtifactHashes`. (FR-007) <!-- doti-task-hash: 21a290e3f633440dee0038a6eb3dae32d5fafccaa6a6e369297b02585ae994f6 -->
- [x] T007 (optional) `tools/Hx.Tooling.Contracts/CycleState.cs`: additive nullable `StageGraphFingerprint` on `CycleStageProof`. (FR-010) <!-- doti-task-hash: bfb40d068a063c57a0e509bc2e868d5258d127fe4726a9f657b6ca237906eecd -->

## Phase 3: Update orphan-prune [W3]

- [x] T008 `tools/Hx.Doti.Core/DotiInstaller.cs`: orphan-prune pass before `WriteBaseline` — on-disk scan of agent SkillsRoots for managed `*-doti-*` dirs not in `DotiRenderer.BuildTargets`, delete ONLY baseline-clean files (else preserved/blocked), prune empty dirs, record in `ObsoleteAssets`; generalize `RemoveObsoleteLegacyDotiAssets` beyond the `doti/` prefix. (FR-008) <!-- doti-task-hash: 0e6246f26c183bff0b1f219cd15973599cfbd34450b80fdf95e7035d63def16d -->
- [x] T009 `tools/Hx.Doti.Core/DotiPayloadParityChecker.cs`: flag a `*-doti-*` skill dir present in the repo but absent from render targets. (FR-009) <!-- doti-task-hash: 77095d8211cc028f2d8ef7c29aa11c058e4a0c6b9ab2c392688fedaf98ec9644 -->

## Phase 4: Test matrix [W1/W2/W3]

- [x] T010 `test/Hx.Cycle.Tests/RestampSafetyTests.cs` + `FreshnessReasonTests.cs`: `PrereqRebindable -> ReBindContentEqual`; own-fresh + only-prereq-set-moved-with-identical-content => PrereqRebindable; existing ChangeSetDiffers/OwnArtifactChanged -> RerunRequired rows unchanged. <!-- doti-task-hash: 0873679c1dd5cfadf0286c55e73af379925d02e61135c30d1876c7162464afe1 -->
- [x] T011 `test/Hx.Runner.Tests` (RefreshTests-style): arch-blocker-edits-spec (spec CONTENT edit => plan/tasks/analyze stay RerunRequired, never auto-rebound); arch-blocker-edits-plan (specify/clarify stay Fresh; tasks/analyze rebind after plan re-stamp). (SC-001) <!-- doti-task-hash: 6ab1aedf48fa7a9dacfcf0a83830050d421937ef38bad7db601dad02ddab1858 -->
- [x] T012 edge-only/self-modifying-workflow (byte-identical artifacts, edge moved => ReBindContentEqual, refresh --apply-safe clears) + inserted-stage verdict test. (SC-002, SC-004) <!-- doti-task-hash: e54e7189b4a5026b96bef5940f47cfc6031fe14132785c7aad8d8712bfd4dbb4 -->
- [x] T013 review-kind never auto-rebound (analyze/arch-review stay RerunRequired on any upstream change) + code-edit-during-implement regression (ChangeSetDiffers never reclassified). (SC-003) <!-- doti-task-hash: 4b73ec5995dd05f6eac47a929d757a9ddac82ccc39293720724ac12fcbbc3646 -->
- [x] T014 auto-cascade-on-stamp (re-stamp one upstream, no explicit refresh => content-equal dependents Fresh; RerunRequired dependent NOT auto-stamped) + TransitionRecords PrerequisiteArtifactHashes-fresh-after-transition. <!-- doti-task-hash: e6b9dad767b6a93bfd46fcf42c16abb6d5541abe0f5463c0b6657a281bee8589 -->
- [x] T015 `test/Hx.Doti.Tests`: orphan-prune (install at set A, re-render at set B => old dir removed clean, modified orphan preserved, recorded obsolete) + payload-check surplus detection. (SC-005) <!-- doti-task-hash: 4848c232a82d89420374543b2c4c1ebb2d4d2e32aad6bbaa6b81a007cef96f6d -->
- [x] T016 revise-tasks-after-analyze (tasks RerunRequired; analyze rebinds after tasks re-stamp; negative: tasks edit without re-stamp leaves analyze RerunRequired) + multi-round arch-review thrash (round 2 auto-rebinds, no regrowth). <!-- doti-task-hash: 6316b45c522d7f3287cfa702127eaa5275b00ca97988998d851d6c17620a84a6 -->

## Phase 5: Proof + mandatory documentation sweep

- [x] T017 Verify green: `dotnet build -c Release` + `dotnet test`; `hx doti cycle check`; `hx doti payload check --repo .`; `hx gate run --profile normal`; confirm the gate-proof digest is byte-unchanged for an unchanged diff. (SC-006) <!-- doti-task-hash: 47d8daaa02ec7980b9e0c8b2d83e1168f42baaeaa9de6c47b1a8f66b79868ccc -->
- [x] T018 **Documentation sweep (mandatory final task):** update every doc describing the change so docs match the code — `README.md` (the stamping/reconcile behavior + that arch-review/amend now auto-cascade), `CHANGELOG.md`, the agent context (`CLAUDE.md`/`AGENTS.md`/`.doti/agent-context.md`), `.doti/core/templates/commands/doti-amend.md` (note the now-automatic rebind), each affected rendered skill, and `hx describe`/`--help` — then re-render installed assets. <!-- doti-task-hash: 433c79de3badbacd827da5a80bf2d2b27ddc651621cbf28a45c8dd10eb2272a2 -->

## Acceptance mapping
- FR-001/002/003 -> T001-T003, T010, T013 ; FR-004 -> T003, T012 ; FR-005 -> T004, T012, T016 ; FR-006 -> T005, T014 ; FR-007 -> T006, T014 ; FR-008/009 -> T008, T009, T015 ; FR-010 -> T007, T017 ; SC-001..006 -> T011-T017.
