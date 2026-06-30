# Drift Review — 031-doti-update-self-contained

**Stage:** /08-doti-drift-review · **Verdict:** CLEAN — no open drift in any applicable axis. Proceed to /09-release.

**Scope:** the implementation diff `git diff a32afce..HEAD` (cycle `baseRef`→HEAD) + working tree — 39 files, +1912 / −84. Mixed change: code (P1 source resolution + P2 prune + P3 self-commit in `Hx.Doti.Core` + `RunnerCommands.Doti.*`; P5 `Hx.Cycle.Core` doc-stamp scope) and doti-prose (P4 the `/doti-bug` skill + bug command templates). All three axes run.

## Axis 1 — spec ↔ code

Each of the 16 FRs is satisfied by a real enforcing mechanism that matches the approved /03 plan and /04 arch-review design; nothing downgraded enforced→advisory; no `*.Core` logic leaked into `*.Cli`.

| FR | Mechanism in the diff | Locked by |
|----|------------------------|-----------|
| FR-001/002 | `BundledPayloadResolver.Resolve()` (`Hx.Doti.Core`) → `AppContext.BaseDirectory` when `<base>/.doti/core/skills.json` exists; CLI resolves `Resolve() ?? FindDotiSource(CWD)`, fail-closed `Validation_DotiPayloadSourceUnresolved` | `BundledPayloadResolverTests`, `DotiSelfContainedCommandTests` |
| FR-003 | bundled source ⇒ `Install` reads real `payload.manifest.json` ⇒ `before≠after` (no false `already-current`) | `DotiUpdateCommitSeamTests`, `DotiUpdaterTests` |
| FR-004/005 | `PruneOneOrphanDir` no-baseline branch prunes `*doti-*` rendered orphans (reported `removed`); skills.json/constitution preserved | `DotiOrphanPruneTests` |
| FR-006 | `StageSidecar` byte-identical skip + distinct merge-pending `.new` list | `DotiInstallCommandTests` |
| FR-007..011 | `DotiReconcileCommit.Commit` — precise touched-path staging (∪ Installed/Removed/Written, −`.new`/−gitignored), `DOTI_SANCTIONED_COMMIT=1` child env, idempotent, non-git/`--no-commit` skip; result envelope (`SourceOrigin`/`Pruned`/`MergePending`/`Commit`) | `DotiReconcileCommitTests`, `DotiUpdateCommitSeamTests`, `DotiSelfContainedCommandTests` |
| FR-013/014/015 | `/doti-bug` body + `speckit.bug.{assess,fix,test}.md` require RCA→root-cause-fix, forbid bandaid + bandaid-vs-root options/asking | `BugCycleRcaProseTests` |
| FR-016 | `ValidateTransitionReadiness` unions `FeatureArtifactScope.OwnedPaths` into `excluded` (mirrors `Check()`); foreign paths still block | `DocStampScopeTests` |

No spec↔code gap — every FR has a covering mechanism + test; no FR amended or deferred.

## Axis 2 — code ↔ docs

Every code change has a matching doc change; no removed/renamed symbol survives in any doc (the diff is additive — `FindDotiSource` is **retained** as the dev fallback, referenced in 7 files, so there is no stale-reference class of miss).

- README "doti update/install" + CHANGELOG 031 entry — bundled-source default, husk pruning, auto-commit + `--no-commit`.
- `.doti/agent-context.md` re-rendered from `.doti/core/skills.json`; `RunnerCommandFactory.Doti.cs` `update`/`install` descriptions updated.
- P4 prose change is single-sourced in `.doti/core/skills.json` + `extensions/bug/commands/*` → rendered skills/agent-context regenerated (no hand-edited installed skill).

## Axis 3 — source ↔ installed

Both parity authorities green:
- `doti render-skills --check` → **No skill or payload drift across 93 managed payloads.**
- `doti payload check --repo .` → **parity passed for 93 managed file(s).**

## Gate (blocking)

`gate run --profile normal` → **OVERALL: True / success** (post-implement-commit, HEAD 4035d9c) — hygiene, gitleaks, affected-change, sentrux, task-completion (16 hashes), restore/build/test, architecture-test all pass.

## Dogfood record

P5 (FR-016) was implemented + built first, then **used for every stamp in this cycle** (specify→…→implement) with each next-stage produces doc authored ahead and left untracked — zero set-aside dance. That is the working proof #42 is fixed; a genuinely foreign untracked path would still block.

## Conclusion

Code agrees with the spec (16/16 FRs, real mechanisms, no downgrade), with the docs (additive; no stale symbol), and with the installed payload (both parity checks green); the blocking gate is green. **Drift-review stamp authorized; proceed to /09-doti-release (feature cycle → minor → 0.18.0).**
