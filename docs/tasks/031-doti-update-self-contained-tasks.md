# 031 — Tasks: self-contained `hx doti update`/`install` + workflow hardening

**Priority Mode:** workflow/tooling — fail-closed safety + deterministic proof before ergonomics. Order: P1 correct source/version → P2 complete reconcile → P3 self-commit → P4 bug-cycle prose → P5 doc-stamp scope (already landed, dogfooded). Each task maps to its FR/SC for `/06-analyze` coverage. Module homes: resolver/commit in `Hx.Doti.Core`; CLI thin in `RunnerCommands.Doti.*`; P5 in `Hx.Cycle.Core`; prose in `.doti/core/skills.json` + `extensions/bug/commands/*`.

## Phase 1 — P1 correct source + version (fail-closed foundation)

- [x] T001 [P1] `Hx.Doti.Core`: `BundledPayloadResolver.Resolve()` → `AppContext.BaseDirectory` when `<BaseDirectory>/.doti/core/skills.json` exists, else null. Single-responsibility, IO-light, testable. (FR-001, D1) <!-- doti-task-hash: 92ccb9db98689804d7acc313303659d48b85060602e63c42f6214e6558a7ad21 -->
- [x] T002 [P1] `Hx.Runner.Cli`: `RunnerCommands.Doti.{Install,Update,UpdateAll}.cs` resolve `source = BundledPayloadResolver.Resolve() ?? FindDotiSource(CWD)`; fail closed with new `Validation_DotiPayloadSourceUnresolved` (registered in `errorcodes/registry.json`) when both null. (FR-001, FR-002, D1) <!-- doti-task-hash: efb5e7e8c6ec66234c481faa67df2553d1ddf9a0d9cf25997ce9fd2abb8aea3f -->
- [x] T003 [P1] Verify the source fix resolves the false `already-current` (FR-003): with the bundled source, `Install` reads `payload.manifest.json` → stamps the real version → `before≠after`; lock with a test. (FR-003, D1) <!-- doti-task-hash: 8603f1729a31524f7c5bf99dddc93e49dc2c114c1b225ecd70e7b39c8b095df1 -->

## Phase 2 — P2 complete reconcile (no old doti left behind)

- [x] T004 [P2] `Hx.Doti.Core`: `DotiInstaller.PruneOneOrphanDir` no-baseline branch — a `*doti-*` rendered-skill orphan the render no longer targets is PRUNED (reported `removed`), not preserved as "operator-owned"; operator-policy assets (skills.json/constitution) are untouched. (FR-004, FR-005, D2) <!-- doti-task-hash: d3788f679968d70f462300d253e8aaa6060dba3a59d454f9e929d8ad225e5a8a -->
- [x] T005 [P2] `Hx.Doti.Core`: `StageSidecar` skips the `.new` when bundled content is byte-identical to the operator's file; surface the staged `.new` sidecars as a distinct merge-pending list on the result. (FR-006, D3) <!-- doti-task-hash: 168ae3a7b3e95a155483ef4ae6433748b7cabdebe3edc4200455f60b17a2bd26 -->

## Phase 3 — P3 self-owned sanctioned commit

- [x] T006 [P3] `Hx.Doti.Core`: `DotiReconcileCommit` — stage exactly the reconcile's touched paths (Installed ∪ Removed ∪ render Written, MINUS `*.new` + gitignored runtime state) and make a sanctioned commit (`DOTI_SANCTIONED_COMMIT=1` in the child `git commit` env) with an auto message (before→after + pruned). Git repo only; no change → no commit (idempotent). (FR-007, FR-008, FR-010, D4) <!-- doti-task-hash: ccde62aa9dfc2f0a476a13b087972e8508f181d79e517a92ecc90cddeaaca941 -->
- [x] T007 [P3] `Hx.Runner.Cli`: invoke the commit step after a successful reconcile in `Install`/`Update`/`UpdateAll`; add `--no-commit` (default off → commit on); non-git target skips the commit (no error). (FR-008, FR-009, D4) <!-- doti-task-hash: f136f076cfd41ecfa2ce8613d44896184ebfc18ce49aa58109b6f138196ac591 -->
- [x] T008 [P3] `Hx.Tooling.Contracts` + CLI render: extend `DotiUpdateOutcome`/install result with resolved-source origin, pruned paths, `.new` merge-pending list, commit sha/skip-reason; surface in `--json` + the human summary. (FR-011, D5) <!-- doti-task-hash: a95a5eb9c45bccec6cacb550221e3e0f8d22b40d53c5b66bfea4d616d427ed93 -->

## Phase 4 — P4 bug-cycle RCA prose (doti-prose)

- [x] T009 [P4] `.doti/core/skills.json` (`/doti-bug` body) + `extensions/bug/commands/speckit.bug.{assess,fix,test}.md` + `.doti/core/templates/commands/doti-bug.md`: require a proper RCA (reproduce → root cause → validate) in assess; root-cause fix in fix (forbid bandaid); DO the fix, forbid bandaid-vs-root options/asking. (FR-013, FR-014, FR-015, D7) <!-- doti-task-hash: 384e7c468064caeb1a508c696b038d367f31006fb01b76c63cf57b5c799d7e51 -->
- [x] T010 [P4] Re-render skills + agent context; `doti render-skills --check` + `doti payload check` green. (FR-013, SC-014, D7) <!-- doti-task-hash: 35cf443f05d3514215183f104dc5ec561d31c9110e3479f774d4e4ff1503913e -->

## Phase 5 — P5 doc-stamp scope consistency (LANDED, dogfooded)

- [x] T011 [P5] `Hx.Cycle.Core`: `ValidateTransitionReadiness` unions `FeatureArtifactScope.OwnedPaths(_stageModel, state.Feature)` into `excluded` (mirrors `Check()`); foreign paths still block. Done in `7fe8a66`, dogfooded across this cycle's own re-stamps. (FR-016, D8) <!-- doti-task-hash: 01c51e045e9e7159fc1e4893f9a1d56b7c886f04f80ae4baa31330ca06673a69 -->
- [x] T012 [P5] Test: a transition into a doc stage with the next-stage doc present (untracked) succeeds with no set-aside dance; a foreign untracked file STILL blocks. (FR-016, SC-015) <!-- doti-task-hash: 5250b06588bc58bca33f6177f6e444426bc6bec53fa330ebc10330c570785723 -->

## Phase 6 — docs + verification

- [x] T013 [P1-P3 docs] README "doti update/install" + `.doti/agent-context.md` (re-rendered) + `update`/`install` command descriptions (`RunnerCommandFactory.Doti.cs`): bundled-source default, pruning, auto-commit + `--no-commit`. (FR-012) <!-- doti-task-hash: c291d53300fb394b1790ef431bc7284bb15c037bdee448811b165a7f38cafa63 -->
- [x] T014 Integration + golden: SC-001..SC-012 — neutral-dir/dev-checkout source resolution; renamed-orphan removal + `payload check` pass; commit precision/idempotence/non-git/`--no-commit`; `.new` guard; `update-all` parity. (SC-001..012) <!-- doti-task-hash: b5ac6df82a2262feaca27b3d14276bc36448340aa8e1af42b62b43dc2e87c016 -->
- [x] T015 **Documentation sweep (permanent final task):** agent-context, rendered skills, `hx describe`/`--help`, README, `docs/configuration.md` all describe the self-contained update + the bug-cycle RCA discipline consistently; `doti render-skills --check` + `doti payload check` green. (FR docs) <!-- doti-task-hash: e5d3dcba5a70c8791987819a7d274e0cc0316c1e078f672079f86d33e9f8dbd7 -->

## Phase 7 — release docs

- [x] T016 `CHANGELOG.md` + `README.md`: add the `031-doti-update-self-contained` note (release-train documentation). (FR docs) <!-- doti-task-hash: b909a08a7f7a0e5790121fae7a7d90c1a5b9aff6bb60d934551bb67fb5a3d350 -->
