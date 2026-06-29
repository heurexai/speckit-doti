# 022 — Doti repo version lifecycle — Tasks

> Code mode, MVP-first. Phases are sequential + gate-enforced; work the lowest-numbered incomplete task. Read-only visibility (US1, US2) before mutation (US3, US4). Tests precede the impl they cover. Reuses existing machinery (`CanonicalContentHasher`, `ManagedAssetScanner`, `DotiInstaller`, `RepoPayloadStamp`) — most new code is thin wiring + the `GitWorktree` primitive.

## Phase 1: Setup (shared infrastructure)

- [ ] T001 — Add `Hx.Runner.Core` project reference to `Hx.Doti.Core` (within-core edge for `GitWorktree`; acyclic — verify `Hx.Runner.Core` gains no `Hx.Doti.Core` ref) — `tools/Hx.Doti.Core/Hx.Doti.Core.csproj`
- [ ] T002 — Append new diagnostic codes (`doti-not-a-repo` validation, `doti-version-unknown` validation, `git-required` validation, `doti-update-failed` integrity) — `errorcodes/registry.json`; then `hx errorcodes render --repo . --json` to regenerate `tools/Hx.Cli.Kernel/ErrorCodes.g.cs`; `hx errorcodes check` green — [covers FR-004/014]

## Phase 2: Foundational (blocking prerequisites — single-sourced, used by every story)

- [ ] T010 [P] — Contract records: `DotiVersionRelation` enum (`Current`/`Outdated`/`Ahead`/`Unknown`), `DotiRepoVersion`, `DotiScanEntry`/`DotiScanResult`, `DotiAssetOutcome`, `DotiUpdateOutcome`, `DotiUpdateAllSummary` — `tools/Hx.Tooling.Contracts/DistributionContracts.cs` (or a new `DotiVersionContracts.cs`) — [covers FR-001/002/005/016]
- [ ] T011 — Test for `RepoPayloadStore` read/write round-trip + missing/malformed file — `test/Hx.Doti.Tests/RepoPayloadStoreTests.cs` (write first; must FAIL) — [covers FR-001]
- [ ] T012 — Extract `RepoPayloadStore` (read/write `.doti/payload.json`) from `DotiInstaller`'s private `ReadRepoPayloadVersion`/`StampRepoPayload`; `DotiInstaller` now calls it (no behaviour change — `DotiReconciliationTests` stay green) — `tools/Hx.Doti.Core/RepoPayloadStore.cs`, `tools/Hx.Doti.Core/DotiInstaller.cs` — [covers FR-001/020]
- [ ] T013 — Test for `DotiVersionRelationCalculator` (equal→Current, older→Outdated, newer→Ahead, null→Unknown) — `test/Hx.Doti.Tests/DotiVersionRelationTests.cs` (write first; must FAIL) — [covers FR-002]
- [ ] T014 — `DotiVersionRelationCalculator.Relate(repoVersion, toolVersion)` via `GitVersionTool.CompareVersions` — single source of the relation — `tools/Hx.Doti.Core/DotiVersionRelationCalculator.cs` — [covers FR-002/020]

**Checkpoint:** `gate run --profile normal` green.

## Phase 3: User Story 1 — Know a repo's version + fix `version --repo` (Priority: P1) 🎯 MVP

**Goal:** `hx doti check-version` reports a repo's Doti version + relation; `hx version --repo` stops mis-reporting `newer`.  ·  **Independent Test:** against `speckit-nomos` (0.13.3) → `current`, human + JSON; `version --repo` reads `equal` not `newer`.

- [ ] T020 [P] [US1] — Test `DotiVersionInspector`: doti repo → version+relation; no `.doti` → `not-a-repo`; `.doti` but no payload → `version-unknown` — `test/Hx.Doti.Tests/DotiVersionInspectorTests.cs` (FAIL first) — [covers FR-001/002/004]
- [ ] T021 [US1] — `DotiVersionInspector.Inspect(repo, toolVersion)` → `DotiRepoVersion` (via `RepoPayloadStore` + `DotiVersionRelationCalculator`), distinguishing not-a-repo vs version-unknown — `tools/Hx.Doti.Core/DotiVersionInspector.cs` — [covers FR-001/002/004]
- [ ] T022 [US1] — Wire `hx doti check-version --repo <p> --json`: `AddDotiCheckVersion` + handler delegating to `DotiVersionInspector`, rendering `CliResult` (human + JSON) — `tools/Hx.Runner.Cli/RunnerCommandFactory.Doti.cs`, `tools/Hx.Runner.Cli/RunnerCommands.Doti.CheckVersion.cs` — [covers FR-001/004/019]
- [ ] T023 [US1] — Test: `ScaffoldVersionReporter` computes `targetRelation` from `payload.json` (clean adopted repo → `equal`, not `newer`); falls back to `scaffold-version.json` only when payload absent — `test/Hx.Runner.Tests/VersionTargetRelationTests.cs` (FAIL first) — [covers FR-003]
- [ ] T024 [US1] — Fix `ScaffoldVersionReporter.Report()`: source the target version from `.doti/payload.json` via `RepoPayloadStore` + relate via `DotiVersionRelationCalculator`; scaffold-version fallback only — `src/Hx.Scaffold.Core/Versioning/ScaffoldVersionReport.cs` — [covers FR-003/020]

**Checkpoint:** US1 independently functional + `gate run` green (MVP — stop & validate).

## Phase 4: User Story 2 — Scan a tree for Doti repos (Priority: P2)

**Goal:** `hx doti scan --root <d>` tables every Doti repo + its version.  ·  **Independent Test:** a root with 2 doti repos + 1 non-doti → exactly the 2, table + JSON.

- [ ] T030 [P] [US2] — Test `DotiRepoScanner`: discovers `.doti/payload.json` repos under a root; skips `.git`/vendored + doesn't descend into a found repo; empty → empty success; unreadable/malformed → `unknown`+reason — `test/Hx.Doti.Tests/DotiRepoScannerTests.cs` (FAIL first) — [covers FR-005/006/007]
- [ ] T031 [US2] — `DotiRepoScanner.Scan(root)` → `DotiScanResult` (read-only, error-tolerant, via `DotiVersionInspector`) — `tools/Hx.Doti.Core/DotiRepoScanner.cs` — [covers FR-005/007]
- [ ] T032 [US2] — Wire `hx doti scan --root <d> --json`: factory + handler → `DotiRepoScanner`; human table + JSON array; explicit empty — `tools/Hx.Runner.Cli/RunnerCommandFactory.Doti.cs`, `tools/Hx.Runner.Cli/RunnerCommands.Doti.Scan.cs` — [covers FR-005/006/019]

**Checkpoint:** US2 functional + `gate run` green.

## Phase 5: User Story 3 — Safe, customization-aware update (Priority: P3)

**Goal:** `hx doti update` reconciles tool-owned assets, reports before→after, preserves+reports customizations (`--force` to override), applies in a git worktree (`--dry-run` preview), git required.  ·  **Independent Test:** a repo with one hand-edited managed template → kept+reported without `--force`, overwritten with `--force`; constitution untouched; mutation via worktree; git absent → fail hard.

- [ ] T040 [P] [US3] — Test `GitWorktree`: create at HEAD, run action inside, capture change set (diff), apply back; `--dry-run` leaves repo untouched; no-git → fail hard — `test/Hx.Runner.Tests/GitWorktreeTests.cs` (FAIL first) — [covers FR-013/014]
- [ ] T041 [US3] — `GitWorktree` primitive (`Create`/`CaptureChanges`/`ApplyBack`/`Remove`/`EnsureGitAvailable`) via `ProcessRunner` — `tools/Hx.Runner.Core/Git/GitWorktree.cs` — [covers FR-013/014]
- [ ] T042 [P] [US3] — Test `DotiUpdater`: before→after version; customized managed asset kept+reported (no force) / overwritten (force); operator-owned (constitution) untouched even with force; no baseline → proceed+warn — `test/Hx.Doti.Tests/DotiUpdaterTests.cs` (FAIL first) — [covers FR-008/009/010/011/012]
- [ ] T043 [US3] — `DotiUpdater.Update(repo, force)`: precondition is-doti; capture before; reconcile via `DotiInstaller.Install(force)` (reuses customization-aware reconcile + `.new` sidecars + payload stamp); capture after; project `DotiInstallResult` → `DotiUpdateOutcome` — `tools/Hx.Doti.Core/DotiUpdater.cs` — [covers FR-008/009/010/011/012/015]
- [ ] T044 [US3] — `DotiWorktreeUpdate.Run(repo, force, dryRun)`: orchestrate `GitWorktree` + `DotiUpdater` (apply in worktree → preview → apply back / dry-run stops) — `tools/Hx.Doti.Core/DotiWorktreeUpdate.cs` — [covers FR-013/014/015]
- [ ] T045 [US3] — Wire `hx doti update --repo <p> [--force] [--dry-run] --json`: factory + handler → `DotiWorktreeUpdate`; human + JSON; fail-closed not-a-repo / git-required — `tools/Hx.Runner.Cli/RunnerCommandFactory.Doti.cs`, `tools/Hx.Runner.Cli/RunnerCommands.Doti.Update.cs` — [covers FR-008/014/015/019]

**Checkpoint:** US3 functional + `gate run` green.

## Phase 6: User Story 4 — Batch update under a root (Priority: P4)

**Goal:** `hx doti update-all --root <d>` updates every Doti repo, fail-soft, with a summary.  ·  **Independent Test:** mixed-version root → outdated updated, current skipped, customized kept; summary counts; one failure doesn't abort.

- [ ] T050 [P] [US4] — Test `DotiBatchUpdater`: per-repo before→after; fail-soft (one failure, others proceed); `--dry-run`; summary counts (updated/current/customized-skipped/failed) — `test/Hx.Doti.Tests/DotiBatchUpdaterTests.cs` (FAIL first) — [covers FR-016/017/018]
- [ ] T051 [US4] — `DotiBatchUpdater.Run(root, force, dryRun)`: `DotiRepoScanner` → `DotiWorktreeUpdate` per repo, fail-soft, aggregate `DotiUpdateAllSummary` — `tools/Hx.Doti.Core/DotiBatchUpdater.cs` — [covers FR-016/017/018]
- [ ] T052 [US4] — Wire `hx doti update-all --root <d> [--force] [--dry-run] --json`: factory + handler → `DotiBatchUpdater`; human summary table + JSON; `Partial` on any failure — `tools/Hx.Runner.Cli/RunnerCommandFactory.Doti.cs`, `tools/Hx.Runner.Cli/RunnerCommands.Doti.UpdateAll.cs` — [covers FR-016/017/019]

**Checkpoint:** US4 functional + `gate run` green.

## Phase: Polish & Cross-cutting (docs are a required, gate-enforced deliverable)

- [ ] T060 — Update `README.md`: add the four `hx doti` commands to the command map / status boxes; describe the relation/customization/worktree behaviour (implemented-only) — `README.md` — [covers SC-002, docs deliverable]
- [ ] T061 — Update `.doti/core/templates/agent-context-template.md` (+ re-render via `hx doti render-skills --repo . --agents codex,claude`) so command availability lists the four new commands — `.doti/core/templates/agent-context-template.md`, rendered `.doti/agent-context.md`/skills — [covers docs deliverable]
- [ ] T062 — Add the `022-doti-repo-version-lifecycle` CHANGELOG entry under `[Unreleased]` — `CHANGELOG.md` — [covers docs deliverable]
- [ ] T063 — Add the `feature→dev` (squash) / `dev→main` (merge) branch-flow note to `CONTRIBUTING.md` — `CONTRIBUTING.md`
- [ ] T064 — Verify `describe --json` surfaces the four new commands + their options + exit classes + new error codes (capability model) — (no file; `hx describe --json`) — [covers FR-019]
- [ ] T070 — Final `hx gate run --repo . --profile release --json` green (full suite + security) — release Checkpoint

## Dependencies & Execution Order

Phase order above. Within: T011→T012, T013→T014 (test-first); Phase 2 (contracts + single-sourced types) blocks all stories; `DotiVersionInspector` (T021) blocks scan (T031) + update (T043); `GitWorktree` (T041) + `DotiUpdater` (T043) block `DotiWorktreeUpdate` (T044); `DotiWorktreeUpdate` blocks `DotiBatchUpdater` (T051). `[P]` test tasks within a phase are parallelizable (distinct files).

## Implementation Strategy

MVP = Phases 1–3 (check-version + the `version --repo` fix) — the direct fix for "confirm the version", shippable on its own. Then US2 (scan), US3 (update + worktree + customization), US4 (update-all), each validated independently without breaking earlier stories. Reuse over reinvention throughout (the hash/reconcile/payload machinery already exists).

## Gate Notes

`gate run --profile normal` is each phase Checkpoint; `--profile release` (T070) before release. All four commands are planned/advisory until their phase lands; no manual check is presented as gate proof.
