# Tasks: Task Hash-Gated Velopack Completion

> Spec: `docs/specs/006-task-hash-gated-velopack-completion.md`.
> Plan: `docs/plans/006-task-hash-gated-velopack-completion-plan.md`.

## Tasks

Ordered so contracts and tests land before the production behavior they protect where practical. All tasks are initially unchecked because this file is the execution queue, not implementation proof. Checked tasks in this feature will eventually require Doti-generated task hash markers; manual hashes are not proof.

### Phase 1: Workflow Registry And Ordered Skills

- [x] `T001` (FR-023, FR-024, FR-025, FR-026, FR-027, FR-028, FR-029, FR-030, FR-044, FR-045, FR-046, SC-011, SC-012, SC-013, SC-014, SC-015, SC-023, SC-027) - Add workflow-registry tests in `test/Hx.Doti.Tests` and `test/Hx.Runner.Tests` proving stage order, display titles, skill ids, optional/conditional next actions, `07-Implement -> 08-Drift-Review`, `08-Drift-Review -> 09-Release|01-Specify`, and no normal `doti-commit` stage. <!-- doti-task-hash: 0b580844e3c50341db48e4d9baa132dae4af506fd250be9f395220115f314bcb -->
- [x] `T002` (FR-023, FR-024, FR-025, FR-026, FR-027, FR-028, FR-029, FR-030, SC-011, SC-012, SC-013, SC-014, SC-015, SC-027) - Implement the workflow-stage registry in `tools/Hx.Doti.Core/Workflow/*` with ordinal, stage id, command name, skill id, display title, stage status, next-stage relationships, alternate actions, and canonical next-step wording. <!-- doti-task-hash: ac809b19c80f53f586910c655fc521df8ea3febe4ce03b89a069fcedb55b7771 -->
- [x] `T003` (FR-023, FR-024, FR-025, FR-026, FR-027, FR-028, FR-030, FR-044, SC-011, SC-012, SC-015, SC-023, SC-027) - Update `tools/Hx.Doti.Core/DotiRenderer.cs`, `doti/core/skills.json`, and `doti/core/templates/commands/*.md` so rendered Codex/Claude skills and command endings are generated from the workflow registry, including `01-doti-specify` through `09-doti-release`. <!-- doti-task-hash: 35b4f0ca94bf986e900086d8843f399b5af838a6bb74f6a2bfd2b3dea6eac244 -->
- [x] `T004` (FR-029, FR-044, SC-014, SC-023, SC-026) - Update `tools/Hx.Runner.Cli/RunnerCommandFactory.Cycle.cs`, `tools/Hx.Runner.Cli/RunnerCommands.DotiCycle.cs`, and `describe --json` metadata so agents can discover workflow order, numeric skill names, and next-stage relationships, while `doti cycle commit`, `doti-commit`, and commit compatibility diagnostics are not discoverable. <!-- doti-task-hash: 75ff6fbfe67473da0dea44c39cb9cf366b9c38d838c6e398184662d61e0dd39c -->
- [x] `T005` (FR-021, FR-023, FR-026, FR-027, FR-028, FR-030, FR-044, SC-011, SC-012, SC-015, SC-023, SC-027) - Render and include generated workflow assets from the registry: `.agents/skills/**`, `.claude/skills/**`, `.doti/agent-context.md`, `AGENTS.md`, `CLAUDE.md`, `.doti/workflows/doti/workflow.yml`, and `doti/core/workflows/doti/workflow.yml`; do not hand-edit rendered outputs. <!-- doti-task-hash: a3c8e53f0d9d06ecc29f7c1d14c7bf32ad39e521e980ea75305f98aeebcc5b18 -->

### Phase 2: Task Hash Gate

- [x] `T006` (FR-001, FR-002, FR-003, FR-004, FR-005, FR-006, FR-007, FR-008, FR-009, FR-010, FR-011, FR-012, FR-013, FR-014, FR-015, FR-022, SC-001, SC-002, SC-003, SC-004, SC-005, SC-006, SC-009, SC-010) - Add task parser/hash tests in `test/Hx.Doti.Tests` and `test/Hx.Runner.Tests`, covering unchecked tasks, missing task file, checked task missing hash, mismatched hash, duplicate identity, whitespace/EOL-insensitive changes, and meaningful field changes. <!-- doti-task-hash: f71af7c6c829d846a1918a5e596f8c4baea53053a42aecc381303ac98c72dc96 -->
- [x] `T007` (FR-003, FR-004, FR-005, FR-006, FR-007, FR-008, FR-009, FR-010, FR-011, FR-012, FR-015, FR-022, SC-001, SC-003, SC-004, SC-005, SC-006, SC-009) - Implement Markdown task parsing and canonical task hashing in `tools/Hx.Cycle.Core/Tasks/*`, including stable feature/path/task identity, whitespace/EOL normalization, checkbox/hash-marker exclusion, and exact diagnostics with path, line, task id, and reason. <!-- doti-task-hash: 1907b9be702ba40f9d43e17175003adc5f02a9567c81d8ce4a0689cc6274c768 -->
- [x] `T008` (FR-001, FR-002, FR-003, FR-004, FR-005, FR-007, FR-008, FR-015, FR-016, FR-017, FR-020, FR-022, SC-001, SC-002, SC-007, SC-009, SC-010) - Add the `task-completion` gate step in `tools/Hx.Gate.Core/GateRunner.cs` and related gate services so normal and release profiles fail closed for unchecked, missing, or hash-invalid active feature tasks. <!-- doti-task-hash: d3a9669772bdf75a8c1e78934f27e6799453bfc359d5a940e7205107ef46a592 -->
- [x] `T009` (FR-006, FR-013, FR-014, FR-015, FR-021, SC-003, SC-004, SC-005, SC-006, SC-009) - Add `doti task-hash stamp --repo <path> --feature <NNN-slug> --json` in `tools/Hx.Runner.Cli/RunnerCommandFactory.Doti.cs` and `tools/Hx.Runner.Cli/RunnerCommands.Doti.TaskHash.cs`; refuse unchecked tasks as completed while stamping checked tasks, and report evaluated, updated, unchanged, and failed task ids. <!-- doti-task-hash: bdf065c17be9d3cb8f665682c03c9cc0e1d5be9aa16a0904d4c337a5608f5402 -->
- [x] `T010` (FR-001, FR-002, FR-006, FR-007, FR-008, FR-015, FR-016, FR-017, FR-020, FR-022, SC-001, SC-002, SC-003, SC-004, SC-007, SC-009, SC-010) - Extend `tools/Hx.Tooling.Contracts/GateProof.cs`, `GateStep.cs`, schemas under `schemas/*`, and persisted proof models so task-completion proof is machine-readable and recomputable by transition and release paths. <!-- doti-task-hash: 472238817259dfcb35c9a3e56b5fd304e6a6009808b7e836dc6c0a9f0006b18f -->
- [x] `T011` (FR-015, FR-021, FR-022, SC-009) - Append stable task-completion and task-hash diagnostics to `errorcodes/registry.json`, then run `errorcodes render` and `errorcodes check` so path-specific failures have stable codes. <!-- doti-task-hash: 0ec64bcc7198ed74b860eef159fb47615b6b0b608afe84de0d2c80a7f835590a -->

### Phase 3: Stage-Transition Commits And Release Trains

- [x] `T012` (FR-031, FR-032, FR-033, FR-034, FR-035, FR-036, FR-037, FR-038, FR-040, FR-041, FR-042, FR-043, FR-044, SC-016, SC-017, SC-018, SC-019, SC-020, SC-021, SC-022, SC-026) - Add Git fixture tests in `test/Hx.Runner.Tests` for starting the next stage, generated transition subject/trailers, baseline advancement, trailer recovery, no-file-change commits, unrelated path blockers, bare commit rejection, and old commit command non-discoverability/rejection. <!-- doti-task-hash: fa746893a911604bcbb8ff5149d20bda7ce6dae018c7f1c1f09ce07ffd495eaf -->
- [x] `T013` (FR-031, FR-032, FR-033, FR-034, FR-035, FR-036, FR-037, FR-038, FR-040, FR-041, FR-042, FR-043, SC-016, SC-017, SC-018, SC-019, SC-020, SC-021, SC-022) - Implement stage-transition commit services in `tools/Hx.Cycle.Core/CycleService.*`, `CycleStateStore.cs`, `ChangeSetIdentity.cs`, and new supporting types so non-initial stages finalize the previous stage before the requested stage can run. <!-- doti-task-hash: 747676ed72c4cddb98bbfe71123b5796272d191fe608db482a47f8da97556334 -->
- [x] `T014` (FR-016, FR-031, FR-034, FR-035, FR-036, FR-042, SC-016, SC-017, SC-018, SC-021) - Wire transition commit invocation into Doti cycle command entrypoints in `tools/Hx.Runner.Cli/RunnerCommands.DotiCycle.cs`, including gate-proof/task-completion verification for stages that require gate proof. <!-- doti-task-hash: c78105a638befb673560b28a0eb82d6c43c9d61558cced21988e08285752b846 -->
- [x] `T015` (FR-037, FR-038, FR-040, FR-041, FR-042, FR-043, SC-019, SC-020, SC-022) - Update `tools/Hx.Cycle.Core/PrecommitGuard.cs`, `HookInstaller.cs`, and hook tests so sanctioned transition commits and release/tag commits are allowed while raw commits, raw empty commits, and hidden/unrelated files are rejected with path diagnostics. <!-- doti-task-hash: e6e996d30f08d7092090255adcc8a879d87a99ddd2dc5c3519d5e247f1a10567 -->
- [x] `T016` (FR-044, FR-045, FR-046, FR-047, FR-048, FR-049, FR-050, SC-023, SC-024, SC-025, SC-026, SC-027) - Add release-train state tests for completed-unreleased cycles, starting a new `specify` after `drift-review`, two-feature release inclusion, invalid included-cycle failure, and no `specify` branch directly after `implement`. <!-- doti-task-hash: 214d9b38ec74cf2defd2e43b1fdaa0643241feaca5c7a51c8bbc04234ed9a88c -->
- [x] `T017` (FR-047, FR-048, FR-049, FR-050, SC-024, SC-025) - Implement completed-unreleased and released-cycle tracking in `tools/Hx.Cycle.Core/CycleReports.cs`, `CycleState.cs`, `CycleStateStore.cs`, and `tools/Hx.Tooling.Contracts/CycleState.cs`. <!-- doti-task-hash: 221a280ad8c9766c91af0cd448dfd573ef6adac679024f34b3defcdd89901b94 -->
- [x] `T018` (FR-039, FR-047, FR-048, FR-049, FR-050, SC-024, SC-025) - Update release-cycle aggregation in `tools/Hx.Runner.Cli` and `tools/Hx.Scaffold.Cli` integration points so `/doti-release` and `hx release` consume completed unreleased feature cycles without squashing stage commits. <!-- doti-task-hash: dc2b57f948fc37565f5656021d686f236981c4c531d5cb06e1c6490d08b60c96 -->
- [x] `T018A` (FR-087, SC-049) - Update `.doti/memory/constitution.md`, `doti/core/memory/constitution.md`, `doti/core/templates/agent-context-template.md`, and rendered constitution-bearing guidance so the codified-cycle principle names automatic Doti stage-transition commits as the sanctioned path and no longer says `doti cycle commit` is the sole sanctioned commit path. <!-- doti-task-hash: 64c1cbf49930a72137e1c97902073baaf5698e6c0bf22d449aba5e176990ad27 -->

### Phase 4: Velopack Release Product And Installer/Update

- [x] `T019` (FR-018, FR-019, FR-020, FR-063, SC-007, SC-008, SC-034) - Add release proof tests in `test/Hx.Scaffold.Tests` and `test/Hx.Runner.Tests` proving releases fail with raw-archive-only output, fail with unchecked carried-forward Velopack tasks, and succeed only when Velopack installer/update metadata includes type, RID/channel, version, package id, and hash. <!-- doti-task-hash: f13474c5e35a2043e7fafcde9c9225c73195b16843a2d95388298133afc6819f -->
- [x] `T020` (FR-018, FR-019, FR-020, FR-063, SC-008, SC-034) - Add pinned Velopack CLI/tool metadata and verification support in `tools/velopack/velopack.version.json`, `tools/velopack/bin/<rid>/`, `Directory.Packages.props`, and release tool-loading code, matching the existing vendored-tool manifest/hash pattern used by Gitleaks, Sentrux, and GitVersion. <!-- doti-task-hash: fed3d2c103dd734fc329396d886ca24a0cc669d511c2738f27396afe70945317 -->
- [x] `T021` (FR-018, FR-019, FR-063, SC-008, SC-034) - Implement Velopack staging, `vpk pack`, payload inspection, update metadata inspection, and artifact hashing in `src/Hx.Scaffold.Core/Release/*` and `tools/Hx.Scaffold.Cli`; do not publish source archives as the primary product. <!-- doti-task-hash: b9aafc6020467e9faf5236ef560b3d9866e101ba862abb202c0f90d406e5a21f -->
- [x] `T022A` (FR-020, SC-007) - Add the carried-forward release-intent and GitVersion validation work from `docs/tasks/005-velopack-install-update-tasks.md` into `test/Hx.Scaffold.Tests`, `GitVersion.yml`, and `tools/Hx.Version.Core/GitVersionTool.Calculate.cs`: release intent tests, increment-signal validation, calculated version/source/tool identity, and pre-mutation failure cases. <!-- doti-task-hash: 0c223c670f288827f164c7506fe33389f2f60bb17eae657dfb7701ca23b529a5 -->
- [x] `T022B` (FR-019, FR-020, SC-007, SC-034) - Add the carried-forward installed-hx Velopack identity work into `tools/Hx.Scaffold.Cli`, `src/Hx.Scaffold.Core/Release/*`, `tools/Hx.Tooling.Contracts/LocalReleaseResult.cs`, and version-report tests so installed `hx` identity and Velopack package/update identity are reported. <!-- doti-task-hash: 192bbf307c88c16cc6eccf78b0a714b72f00ba27b2108f93fcf73a7b7664677d -->
- [x] `T022C` (FR-018, FR-019, FR-020, FR-063, SC-008, SC-034) - Add the carried-forward generated-app Velopack defaults and package-inspection work into `scaffold/templates/dotnet-cli/**`, `test/Hx.Templates.Tests`, and `test/Hx.Scaffold.Tests`, covering release manifest fields, product executable packaging, vendored assets, active-RID package inspection, and raw-archive rejection. <!-- doti-task-hash: 5ca2c0d911d0fd40e64ed621d1e60bfc065476645e674f67f255a2ba299fd690 -->
- [x] `T022D` (FR-020, FR-056, FR-057, FR-058, FR-059, FR-060, FR-061, FR-062, SC-007, SC-031, SC-032, SC-033) - Add the carried-forward `.doti` migration and managed-baseline work into `tools/Hx.Doti.Core/ManagedAssets/*`, `tools/Hx.Doti.Core/DotiInstaller.cs`, and migration tests, covering legacy root `doti/`, canonical baselines, modified managed asset failure, force behavior, and preserved live configuration. <!-- doti-task-hash: a878d8931a81acb77c78ea77ebf5b4887cbb93bf28278313447e5850fe2f4366 -->
- [x] `T022E` (FR-018, FR-019, FR-020, FR-063, SC-007, SC-008, SC-034) - Add the carried-forward final command/model proof into `test/Hx.Runner.Tests`, `test/Hx.Scaffold.Tests`, `.github/workflows/release.yml`, and proof scripts/searches, covering release-gate proof, GitHub workflow primary Velopack outputs, package inspection, and removed archive-only guidance. <!-- doti-task-hash: abc5f41a74fdbc1b63801bc4176317491cc662610d944d97297ed084d4a067e2 -->
- [x] `T023` (FR-018, FR-019, FR-020, FR-063, SC-007, SC-008, SC-034) - Extend `tools/Hx.Tooling.Contracts/LocalReleaseResult.cs` and release identity metadata with release train features, tag identity, Velopack artifacts, update metadata, staged vendored asset hashes, and source-archive exclusion proof. <!-- doti-task-hash: 2a900b27b9d21ca56564cb611a81c0d18f324ef03aa68b5465f8720ee86c55f3 -->
- [x] `T024` (FR-018, FR-019, FR-063, SC-008, SC-034) - Update `.github/workflows/release.yml`, `.github/workflows/store-release.yml` if retained, and `packaging/*` so GitHub CI publishes Velopack installer/update artifacts plus checksums/metadata as primary outputs and does not present source archives as the Doti product. <!-- doti-task-hash: db62e879b122a7ca5925e8136a410a75b411627acf4d623611f411082895aa31 -->

### Phase 5: Doti Asset Migration And Installer Target Classification

- [x] `T025` (FR-056, FR-057, FR-058, FR-059, FR-060, FR-061, FR-062, FR-064, FR-065, FR-066, FR-067, FR-068, FR-069, FR-070, FR-071, SC-031, SC-032, SC-033, SC-035, SC-036, SC-037, SC-038, SC-039) - Add Doti installer/update fixture tests in `test/Hx.Doti.Tests`, `test/Hx.Scaffold.Tests`, and `test/Hx.Templates.Tests` for missing target, empty target, non-empty no-Doti target, existing Doti repo, legacy root `doti/`, modified obsolete managed asset, preserved custom/live config, and no target argument. <!-- doti-task-hash: b9d16dbe02cc5b1acb3b91562c21e7d72e188bba08774d03eb831b3a1792d76b -->
- [x] `T026` (FR-056, FR-057, FR-058, FR-059, FR-060, FR-061, FR-062, SC-031, SC-032, SC-033) - Move supported Doti source authority from root `doti/` into `.doti/` by updating source assets, `tools/Hx.Doti.Core/DotiRenderer.cs`, `tools/Hx.Doti.Core/DotiInstaller.cs`, `src/Hx.Scaffold.Core/ScaffoldDotiInstaller.cs` if present, rendered skill references, and root entrypoint rendering. <!-- doti-task-hash: f8a6497949ab62eccdf4109c482955b1bbb3b4206f7a346f227fac82b899de22 -->
- [x] `T027` (FR-056, FR-057, FR-058, FR-059, FR-060, FR-061, FR-062, SC-031, SC-032, SC-033) - Implement managed asset baselines and obsolete-removal manifests in `tools/Hx.Doti.Core/ManagedAssets/*`, covering `.doti/core`, `.doti/profiles`, `.doti/templates`, `.doti/memory`, `.doti/integrations`, `.doti/workflows`, and obsolete root `doti/` paths while excluding live configuration. <!-- doti-task-hash: 576c2e3333c83358b7c5e1392c61ea77793ddb3409112f9df11f61f1b4932ec2 -->
- [x] `T028` (FR-056, FR-057, FR-058, FR-059, FR-060, FR-061, FR-062, FR-068, SC-031, SC-032, SC-033, SC-037) - Implement migration/removal behavior in `tools/Hx.Doti.Core/DotiInstaller.cs` so existing older repos install validated `.doti/` assets first, remove only manifest-proven obsolete Doti-owned files, preserve repo-owned files and live config, and fail closed on modified managed assets unless forced. <!-- doti-task-hash: 0afac36c6234f77b2aaa07fa0753c7c5a459b4cfa739cc51a42a2905f084594c -->
- [x] `T029` (FR-064, FR-065, FR-066, FR-067, FR-068, FR-069, FR-070, FR-071, SC-035, SC-036, SC-037, SC-038, SC-039) - Implement explicit installer target classification and proof output in `tools/Hx.Doti.Core` and the Velopack installer/bootstrap entrypoint, including `installed-new-target`, `installed-empty-target`, `installed-non-empty-non-doti-target`, and `upgraded-existing-doti-repo`. <!-- doti-task-hash: a3f42085f9057a992a88d62b5a9d6c757c9cafa53f5472f1463970053a78caac -->
- [x] `T030` (FR-060, FR-062, FR-070, SC-031, SC-032, SC-033, SC-037, SC-038) - Extend install/update JSON and human output contracts in `tools/Hx.Tooling.Contracts` and renderer code so removed, preserved, skipped, blocked, installed, and upgraded paths are reported with exact reasons. <!-- doti-task-hash: 279f3dd6ecd2390d7d1b08df683a6d807a23ac730496400f6c679664191cadd6 -->

### Phase 6: hx Local Configuration And Scaffold Starter Configuration

- [x] `T031` (FR-072, FR-073, FR-074, FR-075, FR-076, FR-077, FR-078, FR-079, FR-080, SC-040, SC-041, SC-042, SC-043, SC-045) - Add hx configuration tests in `test/Hx.Scaffold.Tests`: missing executable-adjacent `hx.config.json` fails operational commands such as `version`, `new`, `release`, and prerequisite install; help/describe remain non-mutating discovery; enabled output with missing/blank/relative directory fails; disabled output without directory succeeds; current-directory independence; removed release-root flags in help and `describe --json`; and pre-mutation/pre-inspection failure. <!-- doti-task-hash: 8effd9db73d50f78afe19710843335f0c9b0eba2c6bc01c735ba44cb3462ef5d -->
- [x] `T032` (FR-072, FR-073, FR-074, FR-075, FR-076, FR-077, FR-078, FR-079, FR-080, SC-040, SC-041, SC-042, SC-043, SC-045) - Add Microsoft.Extensions.Configuration packages in `Directory.Packages.props` and `tools/Hx.Scaffold.Cli/Hx.Scaffold.Cli.csproj`, then implement executable-adjacent `hx.config.json` loading and validation in `tools/Hx.Scaffold.Cli` and `src/Hx.Scaffold.Core/Configuration/*` so only non-mutating help/describe discovery can run without config. <!-- doti-task-hash: 46d6ae17c55964e1022994de39d8389b337e85f6a34de3518c88179bcfc49d33 -->
- [x] `T033` (FR-075, FR-076, FR-077, FR-078, FR-079, FR-080, SC-040, SC-041, SC-042, SC-045) - Remove `--release-root`, `--release-root-env`, and `--save-release-root` from `tools/Hx.Scaffold.Cli/ScaffoldCommandFactory.cs`, `ScaffoldCommands.Release.cs`, help text, `describe --json`, tests, README, generated docs, and scaffold templates. <!-- doti-task-hash: 6eeec50454338a957313ec025bc92242aefdc0c67a6c84101a1049f9496b99a5 -->
- [x] `T034` (FR-075, FR-077, FR-078, FR-080, FR-081, SC-040, SC-041, SC-043, SC-045) - Update local release copying in the release service to read the validated local configuration, report config source and disabled-copy reason in `LocalReleaseResult`, and fail before tag/artifact/filesystem mutation when enabled configuration is invalid. <!-- doti-task-hash: e18fd511a2d2c38fd9a6c4a69c60377de0fb1d7f8085e6d0ff418155dc6b4dc1 -->
- [x] `T035` (FR-081, FR-082, FR-083, SC-044, SC-046) - Add scaffold starter configuration tests in `test/Hx.Templates.Tests` for Microsoft.Extensions.Configuration package references, executable-adjacent JSON config file, required config missing failure, and generated/released hx config placement. <!-- doti-task-hash: 0127f7f489b98b31a46e0914af57db6422cf15c9319d3a35f776456db5ebc344 -->
- [x] `T036` (FR-081, FR-082, FR-083, SC-044, SC-046) - Update `scaffold/templates/dotnet-cli/src/**`, `scaffold/templates/dotnet-cli/test/**`, and template docs so generated scaffold starter code uses executable-adjacent JSON configuration with the same fail-hard posture as hx. <!-- doti-task-hash: bbbbef1136081b8123add1da9446ffa97e7395f2d925c31d07e2c6bb300e9a7d -->

### Phase 7: Release Documentation Proof

- [x] `T037` (FR-051, FR-052, FR-053, FR-054, FR-055, SC-028, SC-029, SC-030) - Add release documentation proof tests in `test/Hx.Runner.Tests` and `test/Hx.Scaffold.Tests` for README/docs inventory, updated/no-change reasons, stale documentation failure, release-notes reuse, and docs being included before release artifacts are accepted. <!-- doti-task-hash: a91f3a3c65c793b0fc92c0c6ce89119069d6322ae6991461a954e3d37114ebf2 -->
- [x] `T038` (FR-051, FR-052, FR-053, FR-054, FR-055, SC-028, SC-029, SC-030) - Implement release notes and documentation update proof contracts in `tools/Hx.Tooling.Contracts`, `tools/Hx.Gate.Core`, and the release services so README and relevant repo Markdown docs are inspected and release proof fails on documentation debt. <!-- doti-task-hash: 12484d7aa164ad341a05c034fa575375f73c3c55bd002b52feb39cef3c13ce69 -->
- [x] `T039` (FR-021, FR-051, FR-052, FR-053, FR-054, FR-055, SC-028, SC-029, SC-030) - Update `doti/core/templates/commands/doti-release.md`, workflow registry release next-step wording, README, CHANGELOG, and docs so release-stage guidance names documentation update proof and does not present docs as optional follow-up. <!-- doti-task-hash: d3d35d381ef42c9e3bac666c73b66b5a625f797b1455ebcd6976aeba0a848c07 -->

### Phase 8: Scaffold Payload Parity And Generated Repo Proof

- [x] `T040` (FR-084, FR-085, FR-086, SC-047, SC-048) - Add scaffold payload parity tests in `test/Hx.Doti.Tests`, `test/Hx.Scaffold.Tests`, and `test/Hx.Templates.Tests` proving generated repos receive numbered skills, registry next-step wording, transition-commit behavior/guidance, release-train guidance, installer/update migration rules, and hx configuration guidance. <!-- doti-task-hash: 108101ab54d55a8b7794a6fb1d0cf8557333f90bfa562ba3945bc0f9fcf74d23 -->
- [x] `T041` (FR-084, FR-085, FR-086, SC-047, SC-048) - Implement `doti payload check --repo <path> --json` in `tools/Hx.Runner.Cli` and core parity services so this repo's self-hosted Doti assets and scaffold-installed Doti payload are rendered or canonical-hash equivalent for every managed file. <!-- doti-task-hash: 430de6bfbab80e7828e62c088fe44325482745b3536a7eefb7350214b2d78182 -->
- [x] `T042` (FR-084, FR-085, FR-086, SC-047, SC-048) - Wire scaffold payload parity into `doti render-skills --check` and `gate run --profile normal|release`, with exact drift diagnostics for source path, scaffold payload path, and expected hash/source. <!-- doti-task-hash: b457281ae1df898448c6b89957f9e127787fd503567935e49768867768965df4 -->
- [x] `T043` (FR-084, FR-085, FR-086, SC-047, SC-048) - Update `Hx.Scaffold.Cli new`, template pack assets, and scaffold finishing code so generated repos install the same `.doti` payload, numbered skills, workflow registry metadata, and hx configuration guidance as this repo. <!-- doti-task-hash: 8f0d23dbdca0b3a9a5648d94ecd868f718e5a5b1fbbf151a5f9b113a8946a367 -->

### Phase 9: Error Codes, Help, Describe, And Public Docs

- [x] `T044` (FR-015, FR-021, FR-029, FR-042, FR-049, FR-052, FR-061, FR-071, FR-076, FR-077, FR-080, FR-085, SC-009, SC-014, SC-028, SC-039, SC-040, SC-042, SC-045, SC-048) - Append stable error codes in `errorcodes/registry.json` for transition commit blockers, release-train invalidity, task-hash failures, docs proof stale, missing hx config, invalid local release directory, installer target missing, managed asset modification, obsolete removal blocked, Velopack artifact missing, and scaffold payload drift; run `errorcodes render` and `errorcodes check`. <!-- doti-task-hash: bd8cecaca676cce077f6cf08b8e1a350a7e53749249269566cde0f7b899ab58e -->
- [x] `T045` (FR-021, FR-029, FR-051, FR-080, SC-014, SC-028, SC-042) - Update `README.md`, `CHANGELOG.md`, repo docs, command help, and `describe --json` notes for task hash workflow, numbered workflow, automatic transition commits, multi-spec release trains, Velopack installer/update release products, explicit installer target behavior, and executable-local hx configuration. <!-- doti-task-hash: 192a3a4e9a2f167dec46b7d95f116119e313346e2ae57671b1a462c8c59ef101 -->
- [x] `T046` (FR-021, FR-029, FR-044, FR-079, SC-014, SC-023, SC-026, SC-042) - Add search-proof tests or scripted checks that no live normal workflow docs/help/describe surfaces expose `/doti-commit`, `doti cycle commit` as a normal stage, `--release-root`, `--release-root-env`, `--save-release-root`, or source-archive-only release guidance except as historical/removal context. <!-- doti-task-hash: b24519418ce59a823e94683fbc7f685fb243638cd3ee9bc2a08309e41d49cc4c -->
- [x] `T046A` (FR-088, SC-050) - Add architecture-family truthfulness tests and implementation in `test/Hx.Architecture.Tests`, `rules/architecture.json`, `tools/Hx.Runner.Core/ArchitectureGate/*`, `doti/core/templates/commands/doti-arch-review.md`, `doti/core/templates/agent-context-template.md`, `.doti/agent-context.md`, and rendered skill files so architecture guidance, configured family ids, and `architecture test --json` agree; if guidance documents nine families, the command-backed proof must report those nine families instead of two. <!-- doti-task-hash: 87f0f20b72d08dd72cd43b966a204557a854e234e6fd9751f1e90f10a7061917 -->

### Phase 10: Verification And Cycle Proof

- [x] `T047` (process gate; FR-001, SC-001) - Run `/doti-analyze` against `docs/specs/006-task-hash-gated-velopack-completion.md`, `docs/plans/006-task-hash-gated-velopack-completion-plan.md`, and this task file; fix uncovered FR/SC mappings, hidden advisory-only gates, or missing command-backed proof before implementation. <!-- doti-task-hash: d4fb50db8335b1e5a541a1c0f098f1b5cc8f57ef372d175f1dd68c5e1ba37fc9 -->
- [x] `T048` (process gate; FR-087, SC-049) - Run `/doti-arch-review`; update `scaffold-dotnet.slnx`, `.sentrux/rules.toml`, `rules/architecture.json`, and architecture fixtures for every implementation-introduced project or cross-layer dependency before implementation is accepted. <!-- doti-task-hash: 1aa10c23859af7374c285accd4bea70f54888a39b1e0ce4e898d5b5efd63a529 -->
- [x] `T049` (verification gate; FR-022, SC-010) - Run focused tests after implementation: `test/Hx.Doti.Tests`, `test/Hx.Runner.Tests`, `test/Hx.Scaffold.Tests`, `test/Hx.Templates.Tests`, `test/Hx.Cli.Kernel.Tests`, and `test/Hx.Architecture.Tests`. <!-- doti-task-hash: 384a6af526c23b3c7d0b806ba5debd8b21adc2bae32e568e1d86f94b1e349491 -->
- [x] `T050` (verification gate; FR-001, FR-002, FR-017, FR-085, SC-010, SC-048) - Run command-backed checks after implementation: `dotnet restore .\scaffold-dotnet.slnx`, `dotnet build .\scaffold-dotnet.slnx -c Release --no-restore /m:1`, `dotnet test .\scaffold-dotnet.slnx -c Release --no-build /m:1`, `doti render-skills --check`, `doti payload check --repo . --json`, `architecture test`, `security scan`, `gate run --profile normal`, and `gate run --profile release`. <!-- doti-task-hash: 97ebf100d93a777eaad048fb9b143e50ae01ecd9b5ea10938292652d2a4db2cb -->
- [x] `T051` (proof gate; FR-006, FR-013, FR-018, FR-019, FR-029, FR-063, FR-076, FR-079, SC-008, SC-014, SC-034, SC-042, SC-045) - Run command/model proof after implementation: `Hx.Scaffold.Cli describe --json`, `Hx.Runner.Cli describe --json`, `hx --help`, nested release/help surfaces, `doti task-hash stamp --repo . --feature 006-task-hash-gated-velopack-completion --json`, installer target fixture runs, local Velopack package inspection, GitHub workflow artifact proof, search proof for removed flags/stages, and generated scaffold package inspection. <!-- doti-task-hash: 17b0d491098bb770c4b096a7af3b7621604653ab29b863f100f5f578056f977c -->
- [x] `T052` (release gate; FR-039, FR-047, FR-048, FR-049, FR-055, FR-063, SC-024, SC-025, SC-030, SC-034) - Complete the Doti workflow using the new coded transition behavior once implemented: run drift review, verify stage-transition commits and task hashes, aggregate any completed unreleased cycles into the release train, run the minor release proof, push the verified tag to GitHub CI, and verify the GitHub release exposes Velopack installer/update artifacts instead of source archives. <!-- doti-task-hash: 807bc26479cfb045edf4ead745ade1d4f07bb47e671d87c6bc76da1a5567ce0a -->

## Dependencies

- `T001` blocks `T002`, `T003`, `T004`, and `T005`.
- `T002` blocks `T003`, `T004`, `T013`, `T014`, and `T039`.
- `T003` blocks `T005`, `T040`, `T041`, `T042`, and `T043`.
- `T006` blocks `T007`, `T008`, `T009`, and `T010`.
- `T007` blocks `T008`, `T009`, `T010`, and `T011`.
- `T008` and `T010` block transition and release proof tasks `T014`, `T018`, `T019`, `T023`, and `T050`.
- `T012` blocks `T013`, `T014`, `T015`, `T016`, and `T017`.
- `T013` and `T014` block all release-train tasks `T016`, `T017`, and `T018`.
- `T013` and `T018A` block rendered constitution-bearing guidance in `T005`.
- `T019` blocks `T020`, `T021`, `T023`, and `T024`.
- `T020` blocks `T021`; `T021` blocks release artifact proof in `T023`, `T024`, `T022E`, and `T051`.
- `T022A`, `T022B`, `T022C`, `T022D`, and `T022E` carry forward the unfinished 005 work and must be complete before `T050`, `T051`, and `T052`.
- `T025` blocks `T026`, `T027`, `T028`, `T029`, and `T030`.
- `T026` blocks render/payload parity work in `T040`, `T041`, `T042`, and `T043`.
- `T027` blocks obsolete-removal behavior in `T028` and installer proof in `T030`.
- `T031` blocks `T032`, `T033`, and `T034`.
- `T032` and `T033` block `T034`, `T045`, and command-model proof in `T051`.
- `T035` blocks `T036`; `T036` blocks scaffold parity proof in `T040` and `T043`.
- `T037` blocks `T038`; `T038` blocks `T039` and release gate proof in `T050`.
- `T040` blocks `T041`, `T042`, and `T043`; `T041` blocks `T042`.
- `T044` must land before final help/describe/gate assertions in `T045`, `T046`, `T050`, and `T051`.
- `T046A` must land before `T049`, `T050`, and `T051` so architecture guidance and command-backed architecture proof agree before final verification.
- `T047` runs before implementation and again if task coverage changes.
- `T048` runs after implementation shape is concrete and before full verification.
- `T049`, `T050`, and `T051` depend on all implementation tasks.
- `T052` depends on green verification, valid task hashes, release documentation proof, Velopack artifact proof, and scoped stage-transition commits.

## Coverage

| Requirement | Task(s) |
| --- | --- |
| FR-001 | T006, T008, T010, T050 |
| FR-002 | T006, T008, T010, T050 |
| FR-003 | T006, T007, T008 |
| FR-004 | T006, T007, T008 |
| FR-005 | T006, T007, T008 |
| FR-006 | T006, T007, T009, T010, T051 |
| FR-007 | T006, T007, T008, T010 |
| FR-008 | T006, T007, T008, T010 |
| FR-009 | T006, T007 |
| FR-010 | T006, T007 |
| FR-011 | T006, T007 |
| FR-012 | T006, T007 |
| FR-013 | T006, T009, T051 |
| FR-014 | T006, T009 |
| FR-015 | T006, T007, T009, T011, T044 |
| FR-016 | T008, T010, T014 |
| FR-017 | T008, T010, T019, T050 |
| FR-018 | T019, T021, T023, T024 |
| FR-019 | T019, T021, T022B, T022C, T022E, T023 |
| FR-020 | T008, T019, T022A, T022B, T022C, T022D, T022E, T023 |
| FR-021 | T009, T011, T039, T045 |
| FR-022 | T006, T007, T008 |
| FR-023 | T001, T002, T003, T005 |
| FR-024 | T001, T002, T003 |
| FR-025 | T001, T002, T004 |
| FR-026 | T001, T003, T005 |
| FR-027 | T001, T003, T005 |
| FR-028 | T001, T002, T003 |
| FR-029 | T001, T004, T044, T045 |
| FR-030 | T001, T003, T005 |
| FR-031 | T012, T013, T014 |
| FR-032 | T012, T013 |
| FR-033 | T012, T013 |
| FR-034 | T012, T013, T014 |
| FR-035 | T012, T013, T014 |
| FR-036 | T012, T013, T014 |
| FR-037 | T012, T013, T015 |
| FR-038 | T012, T013, T015 |
| FR-039 | T018, T052 |
| FR-040 | T012, T015 |
| FR-041 | T012, T013, T015 |
| FR-042 | T012, T013, T014, T044 |
| FR-043 | T012, T013, T014 |
| FR-044 | T001, T004, T005, T046 |
| FR-045 | T001, T016 |
| FR-046 | T001, T016, T017 |
| FR-047 | T016, T017, T018 |
| FR-048 | T016, T017, T018 |
| FR-049 | T016, T017, T018, T044 |
| FR-050 | T016, T017, T018 |
| FR-051 | T037, T038, T039, T045 |
| FR-052 | T037, T038, T044 |
| FR-053 | T037, T038 |
| FR-054 | T037, T038, T039 |
| FR-055 | T037, T038, T052 |
| FR-056 | T022D, T025, T026, T028 |
| FR-057 | T022D, T025, T027, T028 |
| FR-058 | T022D, T025, T027, T028 |
| FR-059 | T022D, T025, T027, T028 |
| FR-060 | T022D, T025, T027, T028, T030 |
| FR-061 | T022D, T025, T027, T028, T044 |
| FR-062 | T022D, T025, T028, T030 |
| FR-063 | T019, T021, T022C, T022E, T024 |
| FR-064 | T025, T029 |
| FR-065 | T025, T029 |
| FR-066 | T025, T029 |
| FR-067 | T025, T029 |
| FR-068 | T025, T028, T029 |
| FR-069 | T025, T029 |
| FR-070 | T025, T029, T030 |
| FR-071 | T025, T029, T044 |
| FR-072 | T031, T032 |
| FR-073 | T031, T032 |
| FR-074 | T031, T032 |
| FR-075 | T031, T032, T034 |
| FR-076 | T031, T032, T044 |
| FR-077 | T031, T034, T044 |
| FR-078 | T031, T034 |
| FR-079 | T031, T033, T046 |
| FR-080 | T031, T033, T045 |
| FR-081 | T034, T035, T036 |
| FR-082 | T035, T036 |
| FR-083 | T035, T036 |
| FR-084 | T040, T041, T042, T043 |
| FR-085 | T040, T041, T042, T044 |
| FR-086 | T040, T041, T042, T043 |
| FR-087 | T018A, T048 |
| FR-088 | T046A |
| SC-001 | T006, T008 |
| SC-002 | T006, T008 |
| SC-003 | T006, T007, T009 |
| SC-004 | T006, T007, T009 |
| SC-005 | T006, T007 |
| SC-006 | T006, T007 |
| SC-007 | T008, T019, T022A, T022B, T022D, T022E |
| SC-008 | T019, T021, T022C, T022E, T024 |
| SC-009 | T006, T007, T009, T044 |
| SC-010 | T006, T008, T050 |
| SC-011 | T001, T003, T005 |
| SC-012 | T001, T003, T005 |
| SC-013 | T001, T002, T003 |
| SC-014 | T001, T004, T044, T045 |
| SC-015 | T001, T003, T005 |
| SC-016 | T012, T013, T014 |
| SC-017 | T012, T013, T014 |
| SC-018 | T012, T013, T014 |
| SC-019 | T012, T013, T015 |
| SC-020 | T012, T015 |
| SC-021 | T012, T013, T014 |
| SC-022 | T012, T015 |
| SC-023 | T001, T004, T005, T046 |
| SC-024 | T016, T017, T018 |
| SC-025 | T016, T017, T018 |
| SC-026 | T004, T012 |
| SC-027 | T001, T003, T005 |
| SC-028 | T037, T038, T039, T045 |
| SC-029 | T037, T038 |
| SC-030 | T037, T038, T039 |
| SC-031 | T022D, T025, T026, T027, T028 |
| SC-032 | T022D, T025, T027, T028 |
| SC-033 | T022D, T025, T028, T030 |
| SC-034 | T019, T021, T022B, T022C, T022E, T024 |
| SC-035 | T025, T029 |
| SC-036 | T025, T029 |
| SC-037 | T025, T028, T029, T030 |
| SC-038 | T025, T029, T030 |
| SC-039 | T025, T029, T044 |
| SC-040 | T031, T034, T044 |
| SC-041 | T031, T034 |
| SC-042 | T031, T033, T045, T046 |
| SC-043 | T031, T032, T034 |
| SC-044 | T035, T036 |
| SC-045 | T031, T032, T044 |
| SC-046 | T035, T036 |
| SC-047 | T040, T041, T043 |
| SC-048 | T040, T041, T042, T044 |
| SC-049 | T018A, T048 |
| SC-050 | T046A |

## Gate Notes

- Manual checklist review, manual hash insertion, manual package inspection, raw `git commit`, direct `dotnet test`, and raw GitHub release uploads are not gate proof.
- Planned task-hash, stage-transition, Velopack, documentation-proof, and scaffold-payload checks remain advisory until their production commands and gate steps exist.
- Until the new stage-transition model is implemented, the current cycle engine still uses `doti cycle stamp` and the existing explicit commit chokepoint.
- The task-completion gate is intentionally expected to fail once implemented until this task file's required tasks are checked and hash-stamped by Doti tooling.
