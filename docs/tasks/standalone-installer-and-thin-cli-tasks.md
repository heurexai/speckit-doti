# Tasks: Standalone installer (shared store) + thin-CLI enforcement + release pipeline

> Plan: `docs/plans/standalone-installer-and-thin-cli-plan.md`. Phased: P1 store + offline `new` → P2 doctor/update → P3 thin-CLI → P4 CI/release/README. Test tasks precede the implementation they cover where applicable.

## Tasks

**P1 — shared store + offline, location-independent `new`**

- [ ] `T001` (FR-009/010, CLI contract) — Append 4 codes to `errorcodes/registry.json` (`validation/tool-store-version-unavailable`, `validation/tool-store-version-too-new`, `integrity/tool-store-hash-mismatch`, `internal/tool-store-populate-failed`); run `errorcodes render` (regenerates `tools/Hx.Cli.Kernel/ErrorCodes.g.cs`) + `errorcodes check`.
- [ ] `T002` (FR-003) — `ToolStore` — `tools/Hx.Runner.Core/Tools/ToolStore.cs`: `Root()` (per-OS + `HX_TOOL_STORE`), `PathFor(tool,version,rid,exeName)`, `.store-manifest.json` read/write with a file lock (transactional, additive).
- [ ] `T003` (FR-005/006) — `ToolStoreResolver` — `tools/Hx.Runner.Core/Tools/ToolStoreResolver.cs`: `Resolve(tool,version,rid,expectedSha) → verified store path | null` (SHA-256 via existing `FileHashing`).
- [ ] `T004` (FR-003) — `StorePopulator` — `tools/Hx.Runner.Core/Tools/StorePopulator.cs`: write verified bytes (embedded payload or `ToolFetcher`) into the store; fail-closed; update `.store-manifest.json`.
- [ ] `T005` (SC-003) — Tests (precede consumers) — `test/Hx.Runner.Tests/ToolStoreTests.cs`: store-hit resolves; in-repo fallback; hash-mismatch fails closed; populate is idempotent; `HX_TOOL_STORE` honored. No network.
- [ ] `T006` (FR-005/006) — Route resolution sites store-first → in-repo fallback → fail closed: `tools/Hx.Version.Core/GitVersionTool.cs` (`Verify`/`ResolveExecutable`), the gitleaks validator + the sentrux validator / `SentruxToolPathResolver` / `SentruxChecker` / `SentruxBaselineRunner` / `SentruxGrammarStager`, and `tools/Hx.Runner.Core/Tools/ToolFetcher.cs` (write target → `ToolStore.PathFor`). `RepositoryPathResolver.ResolveInside` UNCHANGED.
- [ ] `T007` (FR-005) — `src/Hx.Scaffold.Core/ToolVendor.cs`: copy predicate EXCLUDES `bin/` + `grammars/` (carry manifests/config/LICENSE only); `RefreshGitleaksConfig` unchanged.
- [ ] `T008` (FR-001/002/004) — Installer self-containment: `src/Hx.Scaffold.Core/EmbeddedPayloadExtractor.cs` (new) extracts the embedded payload to a temp source root; `ScaffoldCommands.New`/`ScaffoldNewRunner` use it (keep `ScaffoldRoot.Find` as dev fallback); `TemplateGenerator.cs` installs the embedded prebuilt nupkg (no runtime `dotnet pack`); `ScaffoldNewRunner` calls `StorePopulator` before first smoke. `tools/Hx.Scaffold.Cli/Hx.Scaffold.Cli.csproj`: `StageEmbeddedPayload` pre-pack target + conditional self-contained single-file publish profile + GitVersion-wired `<Version>`.
- [ ] `T009` (SC-001/002/003) — Tests — `test/Hx.Scaffold.Tests`: `new` runs with no `scaffold-dotnet.slnx` ancestor (extractor path); generated solution has NO tool `bin/`; resolution finds the store. (Heavy end-to-end is env-gated like existing smokes.)

**P1b — trusted prerequisite preflight + Windows winget remediation**

- [ ] `T025` (FR-020, FR-021, FR-024, FR-025, SC-009) — Tests — `test/Hx.Scaffold.Tests`: trusted prerequisite manifest loads from release-managed data, generated repos carry the policy, tampered/missing manifests fail closed, and repo-local extensions cannot override official sources, hard requirements, or winget package/source metadata.
- [ ] `T026` (FR-020, FR-021, FR-024, FR-025) — Implement `doti/core/prerequisites.json`, `src/Hx.Scaffold.Core/Prerequisites/*` manifest loader/models, optional `tools/Hx.Tooling.Contracts/PrerequisiteResult.cs`, generated `.doti/prerequisites.json` carriage, and trusted mappings for `Microsoft.DotNet.SDK.10` and `Git.Git`.
- [ ] `T027` (FR-020, FR-021, FR-022, FR-023, SC-008, SC-010) — Tests — `test/Hx.Scaffold.Tests`: `new` fails before output mutation when .NET SDK/Git probes fail, unsupported versions are detected, output/temp/cache/store directories are unavailable, or trusted manifest evaluation fails; human and JSON diagnostics include safe next actions.
- [ ] `T028` (FR-020, FR-021, FR-022, FR-023) — Wire prerequisite and directory preflight into `src/Hx.Scaffold.Core/ScaffoldNewRunner.cs`, `tools/Hx.Scaffold.Cli/ScaffoldCommands.cs`, and `tools/Hx.Scaffold.Cli/ScaffoldCommandFactory.cs` before payload extraction side effects, template install, store population, first smoke, Git initialization, or output mutation.
- [ ] `T029` (FR-026, FR-027, FR-028, FR-029, FR-030, FR-031, SC-011, SC-012, SC-013) — Tests — `test/Hx.Scaffold.Tests`: `hx prereq install` presents exact plan/digest, requires explicit approval, rejects `--force`/JSON/retry as approval, handles winget unavailable/no mapping/failed install/post-install probe failure, refuses repo-local winget override, and returns platform-unsupported on non-Windows without invoking a package manager.
- [ ] `T030` (FR-026, FR-027, FR-028, FR-029, FR-030, FR-031, SC-011, SC-012, SC-013) — Implement `hx prereq check` and `hx prereq install` in `src/Hx.Scaffold.Core/Prerequisites/*`, `tools/Hx.Scaffold.Cli/Program.cs`, `tools/Hx.Scaffold.Cli/ScaffoldCommandFactory.cs`, and `tools/Hx.Scaffold.Cli/ScaffoldCommands.cs`; verify winget availability/identity, bind approval to the plan digest, execute only trusted release-defined winget package/source actions, rerun probes, and stop before generation unless every hard prerequisite verifies.

**P2 — localised version-stamped skills + doctor/update**

- [ ] `T010` (FR-008) — `tools/Hx.Doti.Core/DotiInstaller.cs`: `WriteMetadata` also writes `.doti/tool-stamp.json` (scaffoldVersion + per-tool version/rid/sha from the vendored manifests); extend the `DotiIntegration` record.
- [ ] `T011` (FR-009/010) — doctor/update engine (`tools/Hx.Runner.Core/Tools/` or `Hx.Doti.Core`): three-way classify (stamp vs store vs pinned manifest) → up-to-date/out-of-date/too-new/hash-mismatch; `update` reconciles store↔pin + re-renders skills via `DotiRenderer`; idempotent; never silent-downgrade (too-new ⇒ refuse). Emits `CliResult` proof.
- [ ] `T012` (FR-009/010/011) — Commands: `hx doctor`/`hx update` in `tools/Hx.Scaffold.Cli/Program.cs`; mirrored `doti tools doctor`/`doti tools update` in `tools/Hx.Runner.Cli/RunnerCommands.cs` + `Program.cs`; add both groups to `describe`.
- [ ] `T013` (SC-004) — Tests — `test/Hx.Runner.Tests` (+ `Hx.Doti.Tests`): classification of older/newer/missing/hash-mismatch; `update` idempotent (second run no-op); too-new refused. No network.

**P3 — thin-CLI architecture enforcement**

- [ ] `T014` (FR-012) — Add **Channel Independence (Thin Adapter)** principle to `doti/core/memory/constitution.md`; re-render so `.doti/memory/constitution.md` matches (`doti render-skills` covers rendered assets / copy as installed).
- [ ] `T015` (FR-013) — `doti/core/templates/plan-template.md` (channel-boundary subsection in *CLI surface & error contract*) + `doti/core/templates/commands/doti-arch-review.md` (thin-CLI checklist item).
- [ ] `T016` (FR-014) — Template families: `scaffold/templates/dotnet-cli/rules/architecture.json` (add `cliSurfaceConfinement` + `cliDelegation`) + `scaffold/templates/dotnet-cli/test/HxScaffoldSample.Architecture.Tests/ArchitectureTests.cs` (positive `[Fact]` + non-vacuity `[Fact]` each).
- [ ] `T017` (FR-015) — Scaffold-own dogfood: new `test/Hx.Architecture.Tests/` project (loads `Hx.*.Cli` + `*.Core`), applies the two families; new **root** `rules/architecture.json` declaring the families; add the project to `scaffold-dotnet.slnx`.
- [ ] `T018` (FR-016) — CLI complexity budget (per plan Research — Sentrux per-layer if supported, else a dedicated check); wire into the gate/arch path.
- [ ] `T019` (verification) — `doti render-skills --check` (no drift) after the constitution/template/skill changes.

**P4 — CI release + version tracking + README**

- [x] `T020` (FR-017) — `.github/workflows/release.yml`: on `v*` tag → `tools fetch` (acquire verified binaries) → `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true` → assemble `speckit-doti-<ver>-win-x64.zip` → `gh release create <tag> --generate-notes` + attach archive (`permissions: contents: write`).
- [x] `T021` (FR-018) — `CHANGELOG.md` (Keep a Changelog) seeded with v0.1.0; baseline annotated tag `v0.1.0` (operator/release step); optional `GitVersion.yml`.
- [x] `T022` (FR-019) — `README.md`: install/get-started via the downloadable installer, linked to the Releases page; refresh Status.

**Verification**

- [ ] `T023` (SC-005, gate) — `dotnet build`/`dotnet test scaffold-dotnet.slnx -c Release` green (incl. the new families on the scaffold's own CLIs); `errorcodes check` intact; `doti render-skills --check` no drift. This is the PR's CI gate.
- [ ] `T024` (SC-001/002/003/004/006 — advisory) — Live proof: `dotnet publish` the installer, run `hx new` from a clean temp dir offline, confirm no in-repo binaries + gate-green via the store + `doctor`/`update` idempotent. Manual/network — recorded, not a CI gate.

## Coverage

| Requirement | Task(s) |
| --- | --- |
| FR-001, FR-002, FR-004 | T008 |
| FR-003 | T002, T004 |
| FR-005, FR-006 | T003, T006, T007 |
| FR-007 | (existing render; preserved) T010 |
| FR-008 | T010 |
| FR-009, FR-010 | T001, T011, T012 |
| FR-011 | T012 |
| FR-012 | T014 |
| FR-013 | T015 |
| FR-014 | T016 |
| FR-015 | T017 |
| FR-016 | T018 |
| FR-017 | T020 |
| FR-018 | T021 |
| FR-019 | T022 |
| FR-020, FR-021 | T025, T026, T027, T028 |
| FR-022, FR-023 | T027, T028 |
| FR-024, FR-025 | T025, T026 |
| FR-026..FR-031 | T029, T030 |
| SC-001, SC-002 | T009, T024 |
| SC-003 | T005, T009, T024 |
| SC-004 | T013, T024 |
| SC-005 | T016, T017, T023 |
| SC-006 | T020, T024 |
| SC-007 | T022 |
| SC-008..SC-010 | T025, T026, T027, T028 |
| SC-011..SC-013 | T029, T030 |

## Dependencies

- `T001` → `T011`/`T012` (codes before doctor/update use them).
- `T002` → `T003`, `T004`, `T006`, `T011` (store before resolver/populator/consumers).
- `T003`/`T004` → `T006`, `T008` (resolver+populator before routing + populate-on-new).
- `T005` precedes `T002`-consumers' completion (tests-first for the store).
- `T007` independent of `T006` but both before `T009`.
- `T025` blocks `T026`, `T027`, `T028`, `T029`, and `T030`.
- `T026` blocks generated-repo policy carriage in `T028`; `T027` blocks `T028`; `T029` blocks `T030`.
- `T028` depends on the `T008` `new` wiring surface and must land before `T009`/`T024` can claim no-coder-safe `new`; `T030` blocks `T024` live proof when missing-prerequisite remediation is part of the scenario.
- `T010` → `T011` (stamp before classification).
- `T014`/`T015` (doti assets) → `T019` (`render-skills --check`).
- `T016`/`T017` before `T023` (families must compile + pass).
- P4 (`T020`-`T022`) independent of P1-P3 code; `T021` baseline tag is a release-stage operator step.
- `T023` after all code/doc tasks; `T024` after `T008`/`T011`/`T020`.
- Independently executable: P3 (`T014`-`T019`) and P4 docs (`T022`) can proceed in parallel with P1/P2 code.

## Gate Notes

- Command-backed today: `dotnet build`/`test`, `architecture test`, `errorcodes check`, `tools fetch`, `render-skills --check`, hygiene — all win-x64.
- Advisory until built this cycle (NOT gate proof until they exist): `ToolStore`/`ToolStoreResolver`/`StorePopulator`, location-independent `new`, `hx doctor`/`hx update` + `doti tools doctor`/`update`, the two new ArchUnit families, `test/Hx.Architecture.Tests`, the CLI complexity budget, `release.yml`, the self-contained installer, CHANGELOG/release automation. Trusted prerequisite manifest/preflight and Windows-only winget prerequisite installation are implemented in the current working tree, pending final command proof.
- Commit/release go through PR + CI (the enforced gate); the full local `gate run` needs the win-x64 binaries + sentrux grammar.
