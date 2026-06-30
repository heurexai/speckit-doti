# Drift Review — 029-scaffold-setup-config

**Stage:** /08-doti-drift-review · **Verdict:** CLEAN — no open drift in any applicable axis. Stamp authorized.
**Reviewed diff:** `751bfc1..d9cb8f1` (the implementation landed in the `analyze: 029` transition commit `d9cb8f1`; the cycle `baseRef` points at it, so the true implementation diff is `HEAD^..HEAD`). 77 files, +4573 / −99.

> **Release bundling (re-stamp):** the **0.17.0** train bundles bug **030-bug-release-bridge** (commits `3b57c92`, `1b53f34`) on top of 029. The drift-review proof was re-stamped over the extended change-set; the bundled fix is reviewed in *Bundled bug fix* below and separately attested by its `/doti-bug` mini-cycle.

## Change set, by area

| Area | What changed |
|------|--------------|
| `Hx.Tooling.Contracts/Setup` | the pure model + resolver + schema + defaults + prompt-definitions + `ISetupTargetWriter` + `SetupConfigProjector` orchestration (`SetupConfig`, `ResolvedSetupConfig`, `ConfigField{value,source,default}`, `SetupKey` Group/AppliesTo, `ConfigSource`) + the additive `ScaffoldRequest.Setup`/`DotiInstallBootstrapRequest.Setup` |
| `Hx.Doti.Core/Setup` | the `.doti`-asset writers (`ReleaseTargetWriter`, `GitVersionSeedWriter`, `CsprojMetadataWriter` XML-encoded, `ConstitutionSection2Writer`), `SetupConfigStore`, `SetupConfigTableFormatter`, `SetupConfigInput` (shared load+validate+resolve) |
| `Hx.Scaffold.Cli` | `--config`/`--interactive` on `hx new`, the wizard (`SetupWizard`/`SetupConsole`), thin command + extracted setup-wiring partials |
| `Hx.Runner.Cli` | `--config`/`--interactive` on `hx doti install`; `hx doti config show [--json]` |
| `Hx.Scaffold.Core` | `ScaffoldNewRunner` projection step (`SetupProjectionStep`) |
| Tests | 20 new integration tests (Scaffold + Runner) + ~58 unit tests (Doti.Tests/Setup) |
| Docs | README Configuration section, `docs/configuration.md` (resolve ladder + setup section + example), `CHANGELOG.md`, `errorcodes/registry.json` (`Validation_SetupConfigInvalid`) |

## Axis 1 — spec ↔ code

Each FR is satisfied by a real mechanism, matches the **revised** arch-review design (the 2 BLOCKERs' resolutions held in the implementation), and is locked by tests; the full gate (build/test/sentrux/arch) is green.

| FR | Mechanism | Lock |
|----|-----------|------|
| FR-001 provenance model | `ConfigField{value,source,default}` + `SetupKey` in `ResolvedSetupConfig` | `SetupConfigResolverTests` |
| FR-002 `--config` both commands | `SetupResolve.New.cs` + `SetupResolve.DotiInstall.cs` → shared `SetupConfigInput` | projection + install integration tests |
| FR-003 persist `.doti/setup.json` | `SetupConfigStore` (repo-portable only; machine-local excluded) | `SetupConfigStoreTests` + D6 test |
| FR-004 `config show` + provenance | `RunnerCommands.Doti.ConfigShow` + `SetupConfigTableFormatter`; all-default when absent | `SetupConfigShowTests` + `SetupConfigShowCommandTests` |
| FR-005 `--interactive` wizard | `SetupWizard` (neutral name) + injectable `SetupConsole`; re-enters `--config` path | `SetupWizardIntegrationTests` (scripted == `--config`) |
| FR-006 project-file projections | the Doti.Core writers; XML-encoded `.csproj`; §2 write-once | `SetupTargetWriterTests` + projection integration |
| FR-007 checklist, never executed | `SetupChecklist` (names NuGet OIDC/secret/Environment/branch-protection/tag + 030-deferred git/CI) | `SetupConfigInstallCommandTests` (inert) |
| FR-008 existing flags survive | flags override config; no-config = byte-identical | SC-007 regression (new + install) |
| FR-009 schema-validated rejection | `SetupConfigSchema.Validate` (schemaVersion/enum/SemVer/SPDX/XML-metachar/path) | `SetupConfigSchemaTests` |
| FR-010 host + library placement | model in Contracts; writers in Doti.Core; **no `Doti.Core→Scaffold.Core` edge** | `architecture-test` + `sentrux-check` green |

**Design-fidelity confirmed:** validation runs in the CLI **before** the request is built (B2/D5); values written to `.csproj`/`GitVersion.yml` are XML-encoded (D9); the projector is a provable no-op when `Setup` is absent (D10); `identity.output`/`--config` paths are contained (D9). The arch-review's decisive re-homing held — `architecture-test` + `sentrux-check` pass, proving no cycle/forbidden edge.

## Axis 2 — code ↔ docs

Every new surface is documented: README Configuration (the `--config`/`--interactive` path + `config show`), `docs/configuration.md` (resolve ladder leads with `--config`; a "Setup config" section + an implementation-accurate `doti-setup.json` example verified against the `SetupConfig` DTO), and the `CHANGELOG.md` 029 entry. No symbol was removed/renamed (additive feature), so no stale reference exists. The `Validation_SetupConfigInvalid` error code is registered.

## Axis 3 — source ↔ installed

`doti render-skills --check` = ok, `doti payload check --repo .` = ok. (A pre-existing utility-skill render drift — empty `Next stage:` lines — was synced during implement; not 029-introduced.)

## Gate

`hx gate run --profile normal` — **all 14 steps pass** (hygiene, gitleaks-verify, affected-change, sentrux-verify, task-completion, restore-build-test, architecture-test, no-velopack, no-source, skill-drift, doti-payload, sentrux-check, version-calculate, security-scan). Full suite green (Scaffold 126, Runner 250, Doti 159, Cycle 129, + the rest).

## In-cycle events (RCA, honest record)

1. **Lost implementation agent, recovered.** The first implement agent's process exited mid-run; its on-disk work (the green core + ~58 unit tests) survived. I verified build + suite green, then completed the edges (FR-007 checklist enhancement, 20 integration tests, the docs) and confirmed completeness — no silent gaps.
2. **Sentrux signal regression → god-file refactor (not a rebaseline).** The implementation added 3 new god-files (CLI/runner wiring at fan-out 23/20/16). Rather than raise the baseline (FR-031 rebaseline was blocked at `/07` — it requires a change-set-fresh arch-review the pre-implementation `/04` can't provide; see issue note below), the wiring was **extracted into focused helpers** (`SetupConfigInput`, `SetupProjectionStep`, the setup-support partials), returning the god-file count to the baseline 6, improving coupling, and clearing the regression — genuine improvement, behaviour identical.

## Reviewed observations (non-blocking)

- **`DotiInstaller.cs` fan-out 16 → 20** (a *baseline* god-file the 029 install-config wiring added to). It does not change the god-file count (already over threshold) and the gate passes; a future cleanup could extract the install-subset projection wiring from it as well. Recorded, not drift.
- **FR-031 rebaseline ↔ pre-implementation arch-review tension** (surfaced this cycle): the `/07` sentrux-check (`--include-untracked`) catches an uncommitted signal growth, but the FR-031 rebaseline needs a change-set-fresh arch-review covering the implemented change — which the cycle's pre-implementation `/04` cannot satisfy. Resolved here by reducing the signal (no rebaseline needed); flagged as a candidate workflow issue for a future cycle.

## Bundled bug fix — 030-bug-release-bridge

The 0.17.0 release train bundles bug **030-bug-release-bridge** on top of 029 (recorded under `.doti/bugs/030-bug-release-bridge/`: assess→fix→test). It fixes a real release-train defect — `CycleService.BuildReleaseTrain` counted only completed-unreleased feature cycles plus the active cycle *at* `release`, so a single completed cycle (a feature parked at `drift-review`, or a bug-fix-only repo) could not anchor its own release.

- **spec↔code:** `BuildReleaseTrain` now counts (a) the active feature cycle at `drift-review` (`CompletionForActiveDriftReviewFeature`) and (b) UNRELEASED test-passed `/doti-bug` mini-cycles (`BugCycleService.ReleaseReadyBugMembers`; `BugReleaseGit` excludes any bug whose fix commit is reachable from the latest `v*` tag — self-maintaining, no marker/seed). The bug-member provider is **injected** into `CycleService` as a delegate, so the `Doti.Core → Cycle.Core` layering holds (no forbidden edge; ArchUnit + Sentrux green).
- **locked by:** 12 regression tests — `ReleaseTrainBridgeTests` (5), `BugCycleServiceTests` (6), `CycleEnforcementReleaseTrainRegressionTests` (1, end-to-end git).
- **gate:** `gate run --profile normal` re-run green over the extended change-set (all 14 steps). Live verify with a freshly-built `hx`: the train resolves to exactly **029 + 030**; historical released bugs 021/023/024 are correctly dropped.
- **no drift:** additive contract change, no symbol removed/renamed; `doti render-skills --check` + `doti payload check` green.

Reviewed **CLEAN** — the bundled fix introduces no spec↔code, code↔docs, or source↔installed drift.

## Conclusion

The implementation reinforces the revised arch-reviewed design with real enforcing mechanisms, the docs and installed payload agree with the code on all three axes, and the gate is fully green. No spec↔code gap, no stale reference, no source↔installed divergence. **Drift-review stamp authorized.**
