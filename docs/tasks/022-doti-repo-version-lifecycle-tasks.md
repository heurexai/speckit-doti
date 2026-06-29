# 022 — Doti repo version lifecycle — Tasks

> Code mode, MVP-first. Phases are sequential + gate-enforced; work the lowest-numbered incomplete task. Read-only visibility (US1, US2) before mutation (US3, US4). Tests precede the impl they cover. Reuses existing machinery (`CanonicalContentHasher`, `ManagedAssetScanner`, `DotiInstaller`, `RepoPayloadStamp`) — most new code is thin wiring + the `GitWorktree` primitive.

## Phase 1: Setup (shared infrastructure)

- [x] T001 — Add `Hx.Runner.Core` project reference to `Hx.Doti.Core` (within-core edge for `GitWorktree`; acyclic — verify `Hx.Runner.Core` gains no `Hx.Doti.Core` ref) — `tools/Hx.Doti.Core/Hx.Doti.Core.csproj` <!-- doti-task-hash: 17d6bf8baa8f37c1415a1542e1599226da7c2fc0435f16b62773c678b9534a4c -->
- [x] T002 — Append new diagnostic codes (`doti-not-a-repo` validation, `doti-version-unknown` validation, `git-required` validation, `doti-update-failed` integrity) — `errorcodes/registry.json`; then `hx errorcodes render --repo . --json` to regenerate `tools/Hx.Cli.Kernel/ErrorCodes.g.cs`; `hx errorcodes check` green — [covers FR-004/014] <!-- doti-task-hash: e3a0bf4d52b03b3de319ac3ab745af2e58e386760583819158a8ac6ecb90c6fb -->

## Phase 2: Foundational (blocking prerequisites — single-sourced, used by every story)

- [x] T010 [P] — Contract records: `DotiVersionRelation` enum (`Current`/`Outdated`/`Ahead`/`Unknown`), `DotiRepoVersion`, `DotiScanEntry`/`DotiScanResult`, `DotiAssetOutcome`, `DotiUpdateOutcome`, `DotiUpdateAllSummary` — `tools/Hx.Tooling.Contracts/DistributionContracts.cs` (or a new `DotiVersionContracts.cs`) — [covers FR-001/002/005/016] <!-- doti-task-hash: 8d5af5ad37641128482d4825a778ffc8391db453d575a198263189883f89955b -->
- [x] T011 — Test for `RepoPayloadStore` read/write round-trip + missing/malformed file — `test/Hx.Doti.Tests/RepoPayloadStoreTests.cs` (write first; must FAIL) — [covers FR-001] <!-- doti-task-hash: 481c8051e9735c9451c6e584ac70854ab794ac03ce55488dc42820ec01d35fab -->
- [x] T012 — Extract `RepoPayloadStore` (read/write `.doti/payload.json`) from `DotiInstaller`'s private `ReadRepoPayloadVersion`/`StampRepoPayload`; `DotiInstaller` now calls it (no behaviour change — `DotiReconciliationTests` stay green) — `tools/Hx.Doti.Core/RepoPayloadStore.cs`, `tools/Hx.Doti.Core/DotiInstaller.cs` — [covers FR-001/020] <!-- doti-task-hash: 678db7a60719ff92aa0ad0e563405391d15cca7e3052c7e66fdb597da825ae5a -->
- [x] T013 — Test for `DotiVersionRelationCalculator` (equal→Current, older→Outdated, newer→Ahead, null→Unknown) — `test/Hx.Doti.Tests/DotiVersionRelationTests.cs` (write first; must FAIL) — [covers FR-002] <!-- doti-task-hash: 304982759bcca2c9fd404d49f137ec8f56a00a0271a0c3eeae1993f82d89a74f -->
- [x] T014 — `DotiVersionRelationCalculator.Relate(repoVersion, toolVersion)` via `GitVersionTool.CompareVersions` — single source of the relation — `tools/Hx.Doti.Core/DotiVersionRelationCalculator.cs` — [covers FR-002/020] <!-- doti-task-hash: eb612ba68c4e3c2b7556471b6609a1312afc0af401afaa260dc5de4a4b6cf17a -->

**Checkpoint:** `gate run --profile normal` green.

## Phase 3: User Story 1 — Know a repo's version + fix `version --repo` (Priority: P1) 🎯 MVP

**Goal:** `hx doti check-version` reports a repo's Doti version + relation; `hx version --repo` stops mis-reporting `newer`.  ·  **Independent Test:** against `speckit-nomos` (0.13.3) → `current`, human + JSON; `version --repo` reads `equal` not `newer`.

- [x] T020 [P] [US1] — Test `DotiVersionInspector`: doti repo → version+relation; no `.doti` → `not-a-repo`; `.doti` but no payload → `version-unknown` — `test/Hx.Doti.Tests/DotiVersionInspectorTests.cs` (FAIL first) — [covers FR-001/002/004] <!-- doti-task-hash: 72017aa1afd8e4010d5e51b789c9786812218c6b0e8f83f60f957e70c927d156 -->
- [x] T021 [US1] — `DotiVersionInspector.Inspect(repo, toolVersion)` → `DotiRepoVersion` (via `RepoPayloadStore` + `DotiVersionRelationCalculator`), distinguishing not-a-repo vs version-unknown — `tools/Hx.Doti.Core/DotiVersionInspector.cs` — [covers FR-001/002/004] <!-- doti-task-hash: 40c65fe1ae57dcae62a730c4cc01344559a3b2ba16410d8e614d3500ffa8c185 -->
- [x] T022 [US1] — Wire `hx doti check-version --repo <p> --json`: `AddDotiCheckVersion` + handler delegating to `DotiVersionInspector`, rendering `CliResult` (human + JSON) — `tools/Hx.Runner.Cli/RunnerCommandFactory.Doti.cs`, `tools/Hx.Runner.Cli/RunnerCommands.Doti.CheckVersion.cs` — [covers FR-001/004/019] <!-- doti-task-hash: fb330e1a4b9a76e35003a5e693c3fc8105b86af9abcfc8d5059e5d94011750df -->
- [x] T023 [US1] — Test: `ScaffoldVersionReporter` computes `targetRelation` from `payload.json` (clean adopted repo → `equal`, not `newer`); falls back to `scaffold-version.json` only when payload absent — `test/Hx.Runner.Tests/VersionTargetRelationTests.cs` (FAIL first) — [covers FR-003] <!-- doti-task-hash: fad62f154048c24bd519ae7dd79b5aa7f37bc337900fe5f84a89d408cf4dc400 -->
- [x] T024 [US1] — Fix `ScaffoldVersionReporter.Report()`: source the target version from `.doti/payload.json` via `RepoPayloadStore` + relate via `DotiVersionRelationCalculator`; scaffold-version fallback only — `src/Hx.Scaffold.Core/Versioning/ScaffoldVersionReport.cs` — [covers FR-003/020] <!-- doti-task-hash: 257ecf261f3633c76b1a2c24f48b66020b0cc5091b36aca46ef23fa7ca3275f4 -->

**Checkpoint:** US1 independently functional + `gate run` green (MVP — stop & validate).

## Phase 4: User Story 2 — Scan a tree for Doti repos (Priority: P2)

**Goal:** `hx doti scan --root <d>` tables every Doti repo + its version.  ·  **Independent Test:** a root with 2 doti repos + 1 non-doti → exactly the 2, table + JSON.

- [x] T030 [P] [US2] — Test `DotiRepoScanner`: discovers `.doti/payload.json` repos under a root; skips `.git`/vendored + doesn't descend into a found repo; empty → empty success; unreadable/malformed → `unknown`+reason — `test/Hx.Doti.Tests/DotiRepoScannerTests.cs` (FAIL first) — [covers FR-005/006/007] <!-- doti-task-hash: b64b69d244dd5690b69d547cc5e9d6ca46636d563db09fd2dae403e7b9b4b195 -->
- [x] T031 [US2] — `DotiRepoScanner.Scan(root)` → `DotiScanResult` (read-only, error-tolerant, via `DotiVersionInspector`) — `tools/Hx.Doti.Core/DotiRepoScanner.cs` — [covers FR-005/007] <!-- doti-task-hash: c151029b4c9794baded8e8f5493d8d8086573f2d7ff408a58ed5e622c2c2bc59 -->
- [x] T032 [US2] — Wire `hx doti scan --root <d> --json`: factory + handler → `DotiRepoScanner`; human table + JSON array; explicit empty — `tools/Hx.Runner.Cli/RunnerCommandFactory.Doti.cs`, `tools/Hx.Runner.Cli/RunnerCommands.Doti.Scan.cs` — [covers FR-005/006/019] <!-- doti-task-hash: 489aa4022ad6c61b9e348183b62802cc4095eed7b7d61a206e140e3403b749f0 -->

**Checkpoint:** US2 functional + `gate run` green.

## Phase 5: User Story 3 — Safe, customization-aware update (Priority: P3)

**Goal:** `hx doti update` reconciles tool-owned assets, reports before→after, preserves+reports customizations (`--force` to override), applies in a git worktree (`--dry-run` preview), git required.  ·  **Independent Test:** a repo with one hand-edited managed template → kept+reported without `--force`, overwritten with `--force`; constitution untouched; mutation via worktree; git absent → fail hard.

- [x] T040 [P] [US3] — Test `GitWorktree`: create at HEAD, run action inside, capture change set (diff), apply back; `--dry-run` leaves repo untouched; no-git → fail hard — `test/Hx.Runner.Tests/GitWorktreeTests.cs` (FAIL first) — [covers FR-013/014] <!-- doti-task-hash: a9c770085e9ffa5fe342c77f680134979e6d25b7646890460fa1a63f9ede2941 -->
- [x] T041 [US3] — `GitWorktree` primitive (`Create`/`CaptureChanges`/`ApplyBack`/`Remove`/`EnsureGitAvailable`) via `ProcessRunner` — `tools/Hx.Runner.Core/Git/GitWorktree.cs` — [covers FR-013/014] <!-- doti-task-hash: 003e4f3439b02f5dbb84c73c0aa75bfbe4e1431322b1f3dbfc99b99b24a717f1 -->
- [x] T042 [P] [US3] — Test `DotiUpdater`: before→after version; customized managed asset kept+reported (no force) / overwritten (force); operator-owned (constitution) untouched even with force; no baseline → proceed+warn — `test/Hx.Doti.Tests/DotiUpdaterTests.cs` (FAIL first) — [covers FR-008/009/010/011/012] <!-- doti-task-hash: b0bce5de700d11bbd3f6dc271b28c7358a582ae2e7e16cd6d6a2f9b22b4d8262 -->
- [x] T043 [US3] — `DotiUpdater.Update(repo, force)`: precondition is-doti; capture before; reconcile via `DotiInstaller.Install(force)` (reuses customization-aware reconcile + `.new` sidecars + payload stamp); capture after; project `DotiInstallResult` → `DotiUpdateOutcome` — `tools/Hx.Doti.Core/DotiUpdater.cs` — [covers FR-008/009/010/011/012/015] <!-- doti-task-hash: 612f2b4cf8e55ef5482395fec63c92f43e319046c942c576c7e613b9a0e4daaa -->
- [x] T044 [US3] — `DotiWorktreeUpdate.Run(repo, force, dryRun)`: orchestrate `GitWorktree` + `DotiUpdater` (apply in worktree → preview → apply back / dry-run stops) — `tools/Hx.Doti.Core/DotiWorktreeUpdate.cs` — [covers FR-013/014/015] <!-- doti-task-hash: 3e4a500e1e6835330806d1e668f77e38277af92837cc68943cebb69f3fa924f3 -->
- [x] T045 [US3] — Wire `hx doti update --repo <p> [--force] [--dry-run] --json`: factory + handler → `DotiWorktreeUpdate`; human + JSON; fail-closed not-a-repo / git-required — `tools/Hx.Runner.Cli/RunnerCommandFactory.Doti.cs`, `tools/Hx.Runner.Cli/RunnerCommands.Doti.Update.cs` — [covers FR-008/014/015/019] <!-- doti-task-hash: 3521ec2c3b1306d32bc5bc9014af3d761cb140d90cc585fe6c6e7bb7ba4dc834 -->

**Checkpoint:** US3 functional + `gate run` green.

## Phase 6: User Story 4 — Batch update under a root (Priority: P4)

**Goal:** `hx doti update-all --root <d>` updates every Doti repo, fail-soft, with a summary.  ·  **Independent Test:** mixed-version root → outdated updated, current skipped, customized kept; summary counts; one failure doesn't abort.

- [x] T050 [P] [US4] — Test `DotiBatchUpdater`: per-repo before→after; fail-soft (one failure, others proceed); `--dry-run`; summary counts (updated/current/customized-skipped/failed) — `test/Hx.Doti.Tests/DotiBatchUpdaterTests.cs` (FAIL first) — [covers FR-016/017/018] <!-- doti-task-hash: bde638480ec46d9442e3020f8113e26541d3a774c35e2d0568adb1cf8fb8e6a1 -->
- [x] T051 [US4] — `DotiBatchUpdater.Run(root, force, dryRun)`: `DotiRepoScanner` → `DotiWorktreeUpdate` per repo, fail-soft, aggregate `DotiUpdateAllSummary` — `tools/Hx.Doti.Core/DotiBatchUpdater.cs` — [covers FR-016/017/018] <!-- doti-task-hash: 185f8d41e0cc7339a574137040c581133a96e7d92c80487fd97bcb9271374afc -->
- [x] T052 [US4] — Wire `hx doti update-all --root <d> [--force] [--dry-run] --json`: factory + handler → `DotiBatchUpdater`; human summary table + JSON; `Partial` on any failure — `tools/Hx.Runner.Cli/RunnerCommandFactory.Doti.cs`, `tools/Hx.Runner.Cli/RunnerCommands.Doti.UpdateAll.cs` — [covers FR-016/017/019] <!-- doti-task-hash: e1afed477b7abd10ea632c1bc0e443273953ada2fd37a31c54f87eed5759abe6 -->

**Checkpoint:** US4 functional + `gate run` green.

## Phase: Polish & Cross-cutting (docs are a required, gate-enforced deliverable)

- [x] T060 — Update `README.md`: add the four `hx doti` commands to the command map / status boxes; describe the relation/customization/worktree behaviour (implemented-only) — `README.md` — [covers SC-002, docs deliverable] <!-- doti-task-hash: ce300d9cf82bb6a60ca82cc3ea3d87ccc08d24fa9ffc5018c2d274434cd5b2a2 -->
- [x] T061 — Update `.doti/core/templates/agent-context-template.md` (+ re-render via `hx doti render-skills --repo . --agents codex,claude`) so command availability lists the four new commands — `.doti/core/templates/agent-context-template.md`, rendered `.doti/agent-context.md`/skills — [covers docs deliverable] <!-- doti-task-hash: d8a4a21265cde42092236b1e90921f7a54d00903ef697dfdab21a7fabfe86b9a -->
- [x] T062 — Add the `022-doti-repo-version-lifecycle` CHANGELOG entry under `[Unreleased]` — `CHANGELOG.md` — [covers docs deliverable] <!-- doti-task-hash: 171a1921b04f6bfb973113eb24432be73e0494d335e7c232fa20aacd81fae083 -->
- [x] T063 — Add the `feature→dev` (squash) / `dev→main` (merge) branch-flow note to `CONTRIBUTING.md` — `CONTRIBUTING.md` <!-- doti-task-hash: 79dc9fcef3e5333c917590fc7586121ede35815b9b313f5d2b8cec18e04bbb0a -->
- [x] T064 — Verify `describe --json` surfaces the four new commands + their options + exit classes + new error codes (capability model) — (no file; `hx describe --json`) — [covers FR-019] <!-- doti-task-hash: 5920e139aab97d3f519a9b1a888dc966cf9854706c7906e4d1956e04cc517386 -->
- [x] T070 — Final `hx gate run --repo . --profile release --json` green (full suite + security) — release Checkpoint <!-- doti-task-hash: 2b9626b6a3754e24595872060471d9f17e8badc4cdcde421b9e61f0146598246 -->

## Dependencies & Execution Order

Phase order above. Within: T011→T012, T013→T014 (test-first); Phase 2 (contracts + single-sourced types) blocks all stories; `DotiVersionInspector` (T021) blocks scan (T031) + update (T043); `GitWorktree` (T041) + `DotiUpdater` (T043) block `DotiWorktreeUpdate` (T044); `DotiWorktreeUpdate` blocks `DotiBatchUpdater` (T051). `[P]` test tasks within a phase are parallelizable (distinct files).

## Implementation Strategy

MVP = Phases 1–3 (check-version + the `version --repo` fix) — the direct fix for "confirm the version", shippable on its own. Then US2 (scan), US3 (update + worktree + customization), US4 (update-all), each validated independently without breaking earlier stories. Reuse over reinvention throughout (the hash/reconcile/payload machinery already exists).

## Gate Notes

`gate run --profile normal` is each phase Checkpoint; `--profile release` (T070) before release. All four commands are planned/advisory until their phase lands; no manual check is presented as gate proof.
