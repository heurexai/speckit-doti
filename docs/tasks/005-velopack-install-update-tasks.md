# Tasks: Velopack Install and Update

> Spec: `docs/specs/005-velopack-install-update.md`.
> Plan: `docs/plans/005-velopack-install-update-plan.md`.

## Tasks

Ordered so contracts and tests land before the core behavior they protect where practical. All tasks are initially unchecked because this file is the execution queue, not implementation proof.

- [ ] `T001` (FR-030, FR-031, FR-032, FR-033, FR-034, FR-035, FR-036, FR-037, FR-038, SC-017, SC-018, SC-019, SC-020) - Add release-intent/tagging contract tests in `test/Hx.Scaffold.Tests`: mutually exclusive `--major|--minor|--patch`, default patch intent, GitVersion intent mismatch fails before mutation, annotated tag message content, same-commit idempotency, different-commit tag conflict, post-tag GitVersion verification, and release result fields.
- [x] `T002` (FR-030, FR-031, FR-032, FR-033, FR-034, FR-035, FR-036, FR-037, FR-038, SC-017, SC-018, SC-019, SC-020) - Extend release contracts in `tools/Hx.Tooling.Contracts/LocalReleaseResult.cs` and related version/tag result records with release intent, tag name, tag commit/object identity, GitVersion identity, Velopack package version, and staged payload checks.
- [x] `T003` (FR-030, FR-031, FR-032, FR-033, FR-034, FR-035, FR-036, FR-037, SC-017, SC-018, SC-019) - Implement GitVersion-led release intent reconciliation and canonical annotated tag creation in `src/Hx.Scaffold.Core/Release/ReleaseTagService.cs` and `src/Hx.Scaffold.Core/Release/LocalReleaseService.cs`, using GitVersion output only and never Doti-owned semantic-version arithmetic.
- [x] `T004` (FR-030, FR-031, FR-032, FR-033, FR-034, FR-035, FR-036, FR-037, SC-017, SC-018, SC-019) - Wire `hx release --major|--minor|--patch` in `tools/Hx.Scaffold.Cli/ScaffoldCommandFactory.cs` and `tools/Hx.Scaffold.Cli/ScaffoldCommands.Release.cs`, preserving `CliResult` output and usage/validation/integrity exit classes.
- [ ] `T005` (FR-031, FR-032, FR-036, FR-037, SC-013, SC-017, SC-018) - Update `GitVersion.yml` and `tools/Hx.Version.Core/GitVersionTool.Calculate.cs` so GitVersion increment signals are explicit and release validation can inspect the calculated version/source/tool identity without mutating refs.
- [ ] `T006` (FR-038, SC-020) - Add removal tests in `test/Hx.Runner.Tests/RunnerCommandsTests.cs` and `test/Hx.Runner.Tests/VersionTests.cs` proving runner `describe --json` exposes only read-only version calculation and no standalone major/minor version-tagging command.
- [x] `T007` (FR-038, SC-020) - Delete the runner-side standalone major/minor version-tagging command from `tools/Hx.Runner.Cli/RunnerCommandFactory.Gates.cs`, `tools/Hx.Runner.Cli/RunnerCommands.Gates.cs`, and `tools/Hx.Version.Core/GitVersionTool.Calculate.cs`; remove `GitVersionTool.Bump`, `NextVersion`, and any tests that assert Doti-owned version arithmetic.
- [ ] `T008` (FR-026, SC-014) - Add scaffold command-model tests in `test/Hx.Scaffold.Tests` proving `hx --help`, `hx describe --json`, and nested help no longer expose `update`.
- [x] `T009` (FR-007, FR-008, FR-026, SC-014) - Remove `hx update` from `tools/Hx.Scaffold.Cli/ScaffoldCommandFactory.cs`, `tools/Hx.Scaffold.Cli/ScaffoldCommands.Update.cs`, `src/Hx.Scaffold.Core/Update/*`, and `test/Hx.Scaffold.Tests/ScaffoldUpdateCommandTests.cs`; preserve only repo-asset migration behavior that is explicitly moved into the `doti install` core service.
- [x] `T010` (FR-022, FR-023, FR-026, FR-038, SC-012, SC-014, SC-020) - Update source guidance in `doti/core/templates/commands/doti-release.md`, `doti/core/templates/agent-context-template.md`, `doti/core/skills.json`, `doti/profiles/dotnet-cli/profile.json`, `README.md`, `CHANGELOG.md`, and release docs so current guidance names `hx release --major|--minor|--patch`, Velopack installer/update artifacts, and no separate update or version-tagging command.
- [x] `T011` (FR-022, FR-026, FR-038, SC-012, SC-014, SC-020) - Run `doti render-skills --repo . --agents codex,claude --json` and include the rendered `.agents/**/SKILL.md`, `.claude/**`, `.doti/agent-context.md`, `AGENTS.md`, and `CLAUDE.md` changes; do not hand-edit rendered files.
- [ ] `T012` (FR-001, FR-002, FR-003, FR-004, FR-005, FR-006, FR-021, FR-027, FR-028, SC-001, SC-002, SC-003, SC-004, SC-005, SC-015) - Add Velopack dependency/tool metadata and tests: `Directory.Packages.props`, a pinned `vpk` manifest or equivalent release-defined tool metadata, package/tool verification tests, and failure cases for missing or untrusted Velopack tooling.
- [ ] `T013` (FR-001, FR-002, FR-003, FR-004, FR-005, FR-006, FR-021, FR-027, FR-028, SC-001, SC-002, SC-003, SC-004, SC-005, SC-015) - Implement Velopack staging and packaging services in `src/Hx.Scaffold.Core/Release/Velopack*` that publish the declared target, stage `.doti/`, `hx`, prerequisite policy, release metadata, and vendored tool assets, run pinned `vpk pack`, and inspect payload/update metadata before success.
- [ ] `T014` (FR-004, FR-021, SC-003, SC-004) - Integrate Velopack startup/update identity in the Doti `hx` executable project and `Hx.Scaffold.Cli version --repo <path> --json` reporting so installed `hx --version` and repo-aware version output include Velopack install/update identity when available.
- [ ] `T015` (FR-009, FR-010, FR-011, FR-012, FR-029, SC-006, SC-016) - Add generated-app Velopack release tests in `test/Hx.Templates.Tests` and `test/Hx.Scaffold.Tests`: scaffold release manifest fields, product executable packaging, app-declared vendored assets staged in `packDir`, and active-RID install/update smoke or advisory skip with explicit reason.
- [ ] `T016` (FR-009, FR-010, FR-011, FR-012, FR-029, SC-006, SC-016) - Update `scaffold/templates/dotnet-cli/**`, generated `.doti/release.json`, and template README/entrypoints so new scaffold apps include Velopack package identity, channel/update source, main executable, RID targets, and release proof defaults.
- [ ] `T017` (FR-013, FR-014, FR-015, FR-016, SC-007, SC-008) - Add `.doti`-only render/install tests in `test/Hx.Doti.Tests`, `test/Hx.Scaffold.Tests`, and `test/Hx.Templates.Tests`: new repo has `.doti/core` and `.doti/profiles`, no root `doti/`, rendered skills reference `.doti/core`, and `doti render-skills --check` fails on legacy root `doti/core` references.
- [ ] `T018` (FR-013, FR-014, FR-015, FR-016, SC-007, SC-008) - Move Doti render/source authority from `doti/core/*` and `doti/profiles/*` to `.doti/core/*` and `.doti/profiles/*`; update `tools/Hx.Doti.Core/DotiRenderer.cs`, `tools/Hx.Doti.Core/DotiInstaller.cs`, `src/Hx.Scaffold.Core/ScaffoldDotiInstaller.cs`, generated skill references, and root entrypoint rendering.
- [ ] `T019` (FR-017, FR-018, FR-019, FR-020, FR-039, FR-040, SC-009, SC-010, SC-011, SC-021) - Add legacy migration tests for `doti install --repo <legacy-repo> --agents codex,claude --json`: unmodified generated root `doti/` content migrates/removes safely, modified content fails with path-specific diagnostics unless forced, live `.doti/release.json`, `.doti/cycle-state.json`, `.doti/gate-proof.json`, prerequisite overrides, `.sentrux` baselines, docs, and product source remain byte-for-byte, and the command does not update installed executables or download release assets.
- [ ] `T020` (FR-017, FR-018, FR-019, FR-020, FR-039, FR-040, SC-009, SC-010, SC-011, SC-021) - Implement `.doti` migration in `tools/Hx.Doti.Core/ManagedAssets/*`, `tools/Hx.Doti.Core/DotiInstaller.cs`, and the shared Doti install/repair service used by `doti install` and scaffold finishing: classify generated vs modified legacy root `doti/`, report removed/moved/replaced/preserved/blocked paths, preserve live repo-owned configuration, and avoid any installed-tool update behavior.
- [ ] `T021` (FR-005, FR-018, FR-020, FR-024, FR-028, FR-029, SC-011, SC-015, SC-016) - Update managed-asset manifests and canonical hash baselines in `tools/Hx.Doti.Core/ManagedAssets/*` so `.doti/core`, `.doti/profiles`, `.doti/templates`, `.doti/memory`, `.doti/integrations`, and `.doti/workflows` are managed Doti assets and live configuration is excluded.
- [ ] `T022` (FR-001, FR-005, FR-006, FR-024, FR-027, FR-028, FR-029, SC-005, SC-015, SC-016) - Add release-gate proof tests in `test/Hx.Runner.Tests` and `test/Hx.Scaffold.Tests`: Velopack metadata freshness, tag identity, GitVersion identity, staged vendored asset hashes, generated scaffold release metadata, and failure when only raw archives are present.
- [ ] `T023` (FR-001, FR-005, FR-006, FR-024, FR-027, FR-028, FR-029, SC-005, SC-015, SC-016) - Extend `tools/Hx.Gate.Core/GateRunner.cs`, relevant gate contracts in `tools/Hx.Tooling.Contracts`, and release proof stores so `gate run --profile release` includes Velopack/tag/payload checks and persists change-set-bound proof.
- [ ] `T024` (FR-001, FR-002, FR-003, FR-004, FR-005, FR-006, FR-022, FR-023, SC-001, SC-002, SC-003, SC-004, SC-012, SC-015) - Update `.github/workflows/release.yml`, `.github/workflows/store-release.yml` if still retained, `packaging/*`, and README release sections so GitHub release uploads Velopack artifacts/update metadata as primary outputs, raw archives are fallback/debug only, and CI is triggered by pushing the Doti release tag created locally by `hx release`.
- [ ] `T025` (error contract) - Append stable error codes in `errorcodes/registry.json` for release intent mismatch, release gate proof missing/stale, tag conflict, tag verification failure, Velopack package failure, missing payload, and payload hash mismatch; run `errorcodes render` and `errorcodes check`.
- [ ] `T026` (architecture) - Run `/doti-arch-review`; if implementation adds any new project beyond existing core/CLI projects, update `scaffold-dotnet.slnx`, `.sentrux/rules.toml`, `rules/architecture.json`, and template architecture fixtures consistently before code lands.
- [ ] `T027` (coverage) - Run `/doti-analyze` against `docs/specs/005-velopack-install-update.md`, `docs/plans/005-velopack-install-update-plan.md`, and this task file; fix any uncovered FR/SC or orphan tasks before implementation.
- [ ] `T028` (verification) - Run focused tests after implementation: `test/Hx.Scaffold.Tests`, `test/Hx.Runner.Tests`, `test/Hx.Doti.Tests`, `test/Hx.Templates.Tests`, `test/Hx.Cli.Kernel.Tests`, and architecture tests.
- [ ] `T029` (verification) - Run command-backed checks: `dotnet restore .\scaffold-dotnet.slnx`, `dotnet build .\scaffold-dotnet.slnx -c Release --no-restore /m:1`, `dotnet test .\scaffold-dotnet.slnx -c Release --no-build /m:1`, `doti render-skills --check`, `architecture test`, `security scan`, `gate run --profile normal`, and `gate run --profile release`.
- [ ] `T030` (proof) - Run command/model proof: `Hx.Scaffold.Cli describe --json`, `Hx.Runner.Cli describe --json`, `hx --help`, nested release help, `doti install --repo <legacy-fixture> --agents codex,claude --json`, search proof for removed command identifiers/flags, search proof for removed update command surfaces, local Velopack package inspection for the active RID, and generated scaffold release package inspection.
- [ ] `T031` (cycle/release) - After implementation, run `/doti-drift-review`, stamp `implement` and `drift-review`, persist a fresh gate proof, stage only scoped files, commit through `doti cycle commit`, then run `/doti-release` using `hx release --minor`, push the verified local release tag to the configured remote, and verify GitHub CI/release artifacts.

## Dependencies

- `T001` blocks `T002`, `T003`, and `T004`.
- `T002` blocks `T003`, `T013`, and release result assertions in `T022`.
- `T003`, `T004`, and `T005` block `T023`, `T024`, `T030`, and `T031`.
- `T006` blocks `T007`; `T007` blocks `T010`, `T011`, and command/model proof in `T030`.
- `T008` blocks `T009`; `T009` blocks `T010`, `T011`, and command/model proof in `T030`.
- `T010` blocks `T011`.
- `T012` blocks `T013`; `T013` blocks `T014`, `T022`, `T023`, `T024`, and package proof in `T030`.
- `T015` blocks `T016`; `T016` blocks generated app proof in `T030`.
- `T017` blocks `T018`; `T018` blocks `T019`, `T020`, `T021`, `T011`, and render proof in `T029`.
- `T019` blocks `T020`; `T020` blocks migration acceptance proof.
- `T021` blocks `T022` and release gate proof.
- `T022` blocks `T023`; `T023` blocks `T029` release gate.
- `T025` must land before final CLI diagnostics are asserted.
- `T026` runs after implementation shape is concrete and before full verification.
- `T027` runs before implementation and again if task coverage changes.
- `T028`, `T029`, and `T030` depend on implementation tasks.
- `T031` depends on green verification and scoped staging.

## Coverage

| Requirement | Task(s) |
| --- | --- |
| FR-001 | T012, T013, T022, T023, T024, T029, T030 |
| FR-002 | T012, T013, T024, T030 |
| FR-003 | T012, T013, T024, T030 |
| FR-004 | T014, T024, T030 |
| FR-005 | T012, T013, T021, T022, T023, T030 |
| FR-006 | T012, T013, T022, T023, T024 |
| FR-007 | T009, T010, T030 |
| FR-008 | T009, T010, T024, T030 |
| FR-009 | T015, T016 |
| FR-010 | T015, T016 |
| FR-011 | T015, T016, T030 |
| FR-012 | T015, T016, T030 |
| FR-013 | T017, T018 |
| FR-014 | T017, T018 |
| FR-015 | T017, T018 |
| FR-016 | T017, T018, T029 |
| FR-017 | T019, T020 |
| FR-018 | T019, T020, T021 |
| FR-019 | T019, T020 |
| FR-020 | T019, T020, T021 |
| FR-039 | T019, T020, T030 |
| FR-040 | T019, T020 |
| FR-021 | T014 |
| FR-022 | T010, T011, T024 |
| FR-023 | T010, T024 |
| FR-024 | T021, T022, T023 |
| FR-025 | T003, T004, T005, T031 |
| FR-026 | T008, T009, T010, T011, T030 |
| FR-027 | T012, T013, T022, T023 |
| FR-028 | T012, T013, T021, T022, T023 |
| FR-029 | T015, T016, T022, T023 |
| FR-030 | T001, T002, T003, T004 |
| FR-031 | T001, T003, T005, T007 |
| FR-032 | T001, T003, T004, T005 |
| FR-033 | T001, T003, T004 |
| FR-034 | T001, T002, T003 |
| FR-035 | T001, T002, T003 |
| FR-036 | T001, T003, T005 |
| FR-037 | T001, T002, T003 |
| FR-038 | T006, T007, T010, T011, T030 |
| SC-001 | T012, T013, T024, T030 |
| SC-002 | T012, T013, T024, T030 |
| SC-003 | T014, T024, T030 |
| SC-004 | T014, T024, T030 |
| SC-005 | T022, T023, T024 |
| SC-006 | T015, T016, T030 |
| SC-007 | T017, T018 |
| SC-008 | T017, T018, T029 |
| SC-009 | T019, T020 |
| SC-010 | T019, T020 |
| SC-011 | T019, T020, T021 |
| SC-021 | T019, T020, T030 |
| SC-012 | T010, T011, T024 |
| SC-013 | T003, T004, T005, T031 |
| SC-014 | T008, T009, T010, T011, T030 |
| SC-015 | T012, T013, T022, T023, T030 |
| SC-016 | T015, T016, T022, T023, T030 |
| SC-017 | T001, T003, T004, T031 |
| SC-018 | T001, T003, T004, T005 |
| SC-019 | T001, T002, T003 |
| SC-020 | T006, T007, T010, T011, T030 |

## Gate Notes

- Manual file copying, manual tag creation, manual package inspection, and direct `dotnet test` transcripts are not release or commit proof.
- Planned Velopack checks are advisory until `hx release` and `gate run --profile release` emit command-backed proof.
- The old update and standalone version-tagging surfaces have been removed from live command registration/source; remaining references in this spec/plan/tasks are removal requirements or historical context.
- The `.doti/core` migration is high blast radius; run render, template, and migration tests before broader gates.
