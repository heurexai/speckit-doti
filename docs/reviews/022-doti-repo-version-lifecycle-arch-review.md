# 022 — Doti repo version lifecycle — Arch-review

**Triage:** runtime CODE change (new `*.Core` types + a `Hx.Runner.Core` git primitive + the `Hx.Scaffold.Core` version-reporter fix + thin CLI). Code lenses apply; no `scaffold/templates/**` change (template lenses N/A). Reviewed against spec/plan/tasks + the existing code mapped during plan.

## Lenses

**Design-soundness — PASS.** The load-bearing decision (reuse `CanonicalContentHasher`/`ManagedAssetScanner`/`DotiInstaller` for customization detection rather than a second scheme; single-source the relation; thin CLI) is sound and fits the established patterns. No re-invention.

**Edge-case / failure-mode — PASS (1 LOW to verify).** Covered: not-a-repo vs version-unknown (FR-004), `ahead` (repo newer, reported never downgraded), no managed-asset baseline → degrade+warn (FR-012), git absent → fail hard (FR-014), empty scan → explicit success, per-repo failure → fail-soft (FR-017). **LOW (verify in /07):** the worktree apply-back uses `git diff` (tracked changes) — confirm no *managed* asset is gitignored, so an update can't silently drop one. *Evidence:* the gitignored `.doti` entries are runtime state only (`cycle-state.json`, `gate-proof.json`); the payload (`core`/skills/`agent-context`/`payload.json`/`managed-assets.json`) is tracked → captured. T040/T041 + T043 assert the round-trip. Not a blocker.

**Data-contract — PASS.** New records (`DotiRepoVersion`, `DotiScanResult`, `DotiUpdateOutcome`, `DotiUpdateAllSummary`, `DotiVersionRelation`) live in `Hx.Tooling.Contracts`, are JSON-serialisable into the `CliResult` `data`, and validate against `schemas/cli-envelope.schema.json` (FR-019, SC-008). The `version --repo` JSON contract (`targetRelation` label set) is preserved — only its source changes (FR-003).

**Security — PASS.** `scan`/`update-all` walk only the operator-supplied `--root`/`--repo`; git is invoked via `ProcessRunner` `ArgumentList` (no shell, no injection); worktrees use temp dirs; no secrets/local-path leakage beyond operator input.

**Blast-radius — PASS.** Mostly additive. Two existing-code touches: extracting `RepoPayloadStore` from `DotiInstaller` (T012 — behaviour-preserving, `DotiReconciliationTests` must stay green) and the `ScaffoldVersionReporter` source fix (T024 — guarded by the new regression test T023). Contained.

**Simpler-alternative — PASS.** The worktree is *required* (operator decision Q2), not gold-plating; everything else reuses existing machinery, so the surface is near-minimal. No simpler correct design that still satisfies FR-013/014.

**Modularity / design-smells — PASS.** One named single-responsibility `*.Core` type per behaviour, composed not inlined (`DotiWorktreeUpdate` = `GitWorktree` + `DotiUpdater`; `DotiBatchUpdater` = `DotiRepoScanner` + `DotiWorktreeUpdate`). No god-type; each fits the Sentrux function-size limit. Roles (`*Scanner`/`*Updater`/`*Calculator`/`*Inspector`/`*Store`) correctly in Core (cliSurfaceConfinement).

**Testability — PASS.** Every Core type has a dedicated test (T011/T013/T020/T030/T042/T050) with temp-dir repos; `GitWorktree` and `DotiUpdater` are testable in isolation; the `version --repo` fix has a regression test (T023). Test-first ordering enforced.

**Fit-with-current-architecture — PASS (1 MEDIUM to hold).** Fits Contracts→Core→Cli, the `CliResult`/error-code convention, and the thin-CLI families. **MEDIUM (hold at /07/gate):** the new within-core edge `Hx.Doti.Core → Hx.Runner.Core` must stay acyclic — `Hx.Runner.Core` must never gain a `Hx.Doti.Core` reference. *Evidence:* sanctioned precedent (`.sentrux/rules.toml:62-64`, `Hx.Sentrux.Core → Hx.Runner.Core`); `max_cycles=0` + Sentrux hold it. T001 verifies no reverse edge.

## Verdict

**No BLOCKER.** Two non-blocking items to confirm during implement (worktree captures all managed assets; the within-core edge stays acyclic) — both have tasks (T040/T041/T043, T001) and gate enforcement. Cleared for `/07-implement`.
