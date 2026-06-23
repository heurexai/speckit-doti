# Tasks: Completed cycle status and existing-repo update

> Plan: `docs/plans/cycle-completion-and-repo-update-plan.md`.

## Tasks

Ordered so prerequisites come first. Tests are placed before the implementation they protect where practical.

- [ ] `T001` (FR-001, FR-002, FR-003, FR-004, FR-005, FR-006, FR-088, FR-089, FR-090, FR-091, FR-092, FR-093, FR-094, FR-095, FR-096, FR-097, FR-098, FR-099, FR-100, FR-113, SC-001, SC-002, SC-034, SC-035, SC-036, SC-037, SC-038, SC-039, SC-040, SC-046) - Add cycle completion/recovery tests in `test/Hx.Runner.Tests`: successful commit completion, crash after commit before completed state write, failed commit before object creation, repeated status/check/commit convergence, new edits after completion, ambiguous HEAD movement, corrupt state recovery, concurrent commit lock behavior, and stamping old/new stages after completion.
- [ ] `T002` (FR-001, FR-002, FR-003, FR-004, FR-005, FR-006, FR-088, FR-089, FR-090, FR-091, FR-092, FR-093, FR-094, FR-095, FR-096, FR-097, FR-098, FR-099, FR-100, FR-113) - Implement completed-cycle contracts and atomic stores in `tools/Hx.Cycle.Core/Completion/*`, `tools/Hx.Cycle.Core/CycleStateStore.cs`, and `tools/Hx.Tooling.Contracts` where shared JSON records are required.
- [ ] `T003` (FR-001, FR-002, FR-003, FR-004, FR-005, FR-006, FR-088, FR-089, FR-090, FR-091, FR-092, FR-093, FR-094, FR-095, FR-096, FR-097, FR-098, FR-099, FR-100, FR-113) - Wire the shared completion recovery evaluator into `tools/Hx.Cycle.Core/CycleService.cs`, `tools/Hx.Cycle.Core/FreshnessEvaluator.cs`, and `tools/Hx.Cycle.Core/GitRefs.cs` before normal freshness/prerequisite checks.
- [ ] `T004` (FR-119, FR-120, FR-121, FR-122, FR-123, FR-124, FR-125, FR-126, FR-127, FR-128, FR-129, FR-130, FR-131, SC-052, SC-053, SC-054, SC-055, SC-056, SC-057, SC-058, SC-059) - Add proof-provenance and bypass-hardening tests in `test/Hx.Runner.Tests` and `test/Hx.Impact.Tests`: direct `dotnet test` rejected, hand-edited gate proof rejected, staged scope drift rejected, stale test output identity rejected, forged hook sentinel rejected, missing hook reported, external commit classified, and clean-checkout validation does not trust local proof files.
- [ ] `T005` (FR-119, FR-120, FR-121, FR-122, FR-123, FR-124, FR-125) - Extend proof contracts in `tools/Hx.Tooling.Contracts/GateProof.cs`, `tools/Hx.Tooling.Contracts/PersistedGateProof.cs`, and `tools/Hx.Tooling.Contracts/AffectedTestProof.cs` with producer provenance, canonical proof digest, staged-tree identity, and execution artifact identity fields.
- [ ] `T006` (FR-119, FR-120, FR-121, FR-122, FR-123, FR-124, FR-130, FR-131) - Update `tools/Hx.Gate.Core/GateRunner.cs`, `tools/Hx.Cycle.Core/GateProofStore.cs`, and `tools/Hx.Cycle.Core/GateProofValidator.cs` to mint, persist, recompute, and refuse mismatched proof provenance/digest/execution identity.
- [ ] `T007` (FR-125, FR-126, FR-127, FR-128, FR-129) - Update `tools/Hx.Cycle.Core/CycleService.cs`, `tools/Hx.Cycle.Core/CommitScopeInspector.cs`, `tools/Hx.Cycle.Core/PrecommitGuard.cs`, and `tools/Hx.Cycle.Core/HookInstaller.cs` with final commit trailers, staged-tree binding, hook-health reporting, and external/bypass commit verdicts.
- [ ] `T008` (FR-029, FR-030, FR-031, FR-032, FR-033, FR-034, FR-035, FR-049, FR-050, FR-051, FR-052, FR-053, FR-054, FR-055, FR-056, FR-057, FR-058, SC-012, SC-017, SC-018, SC-019, SC-020) - Add version identity/report tests in `test/Hx.Scaffold.Tests`: normalized SemVer/tag comparison, running versus target repo report, `--repo` absolute path, clean/modified managed state, missing stamp, unsupported hash schema, and read-only side-effect checks.
- [ ] `T009` (FR-029, FR-030, FR-031, FR-032, FR-033, FR-034, FR-035, FR-049, FR-050, FR-051, FR-052, FR-053, FR-054, FR-055, FR-056, FR-057, FR-058) - Implement canonical scaffold version identity contracts in `tools/Hx.Tooling.Contracts`, `tools/Hx.Version.Core/GitVersionTool.cs`, and `src/Hx.Scaffold.Core/Versioning/*`.
- [ ] `T010` (FR-049, FR-050, FR-051, FR-052, FR-053, FR-054, FR-055, FR-056, FR-057, FR-058) - Add repo-aware version report command wiring in `tools/Hx.Scaffold.Cli/Program.cs` and `tools/Hx.Scaffold.Cli/ScaffoldCommands.cs`, preserving scalar `hx --version` behavior and `CliResult` JSON output.
- [ ] `T011` (FR-036, FR-037, FR-038, FR-039, FR-040, FR-041, FR-042, FR-043, FR-044, FR-070, FR-071, FR-072, FR-073, FR-074, FR-075, FR-076, FR-077, FR-116, FR-117, SC-013, SC-014, SC-015, SC-026, SC-027, SC-029, SC-030, SC-050, SC-061) - Add managed manifest and canonical-hash tests in `test/Hx.Doti.Tests`: workflow-template modification, skill/generated-instruction modification, force replacement planning, YAML formatting-equivalence, YAML structural change, JSON property-order equivalence, JSON fail-closed cases, binary byte-exact hashing, missing/corrupt metadata, and unsupported schema.
- [ ] `T012` (FR-036, FR-037, FR-038, FR-039, FR-040, FR-041, FR-042, FR-043, FR-044, FR-070, FR-071, FR-072, FR-073, FR-074, FR-075, FR-076, FR-077, FR-116, FR-117) - Implement managed asset manifest and classifier in `tools/Hx.Doti.Core/ManagedAssets/*`, plus a source manifest such as `doti/core/managed-assets.json`.
- [ ] `T013` (FR-070, FR-071, FR-072, FR-073, FR-074, FR-075, FR-076, FR-077) - Implement canonical content hashing profiles in `tools/Hx.Doti.Core/ManagedAssets/CanonicalContentHasher.cs`, using YamlDotNet for YAML, RFC 8785-compatible JSON behavior for JSON, and byte-exact hashing for binary/integrity profiles.
- [ ] `T014` (FR-036, FR-037, FR-038, FR-039, FR-040, FR-041, FR-044, FR-116, FR-117) - Update `tools/Hx.Doti.Core/DotiInstaller.cs` and `tools/Hx.Doti.Core/DotiRenderer.cs` so `new`, `install`, and successful `update` write target repo scaffold version and canonical managed-asset hash metadata.
- [ ] `T015` (FR-007, FR-008, FR-009, FR-010, FR-011, FR-012, FR-013, FR-014, FR-015, FR-016, FR-017, FR-018, FR-020, FR-021, FR-022, FR-023, FR-024, FR-025, FR-026, FR-027, FR-028, FR-045, FR-046, FR-047, FR-048, FR-059, FR-060, FR-061, FR-062, FR-063, FR-064, FR-065, FR-101, FR-102, FR-103, FR-104, FR-105, FR-106, FR-107, FR-108, FR-109, FR-110, FR-111, FR-112, FR-114, FR-115, FR-118, SC-003, SC-004, SC-005, SC-006, SC-007, SC-008, SC-009, SC-010, SC-011, SC-021, SC-022, SC-023, SC-028, SC-041, SC-042, SC-043, SC-044, SC-045, SC-047, SC-048, SC-049, SC-051, SC-060, SC-062, SC-063) - Add `hx update` tests in `test/Hx.Scaffold.Tests` using temporary Git repos: default repo target, `--repo` absolute path, dry-run no mutation, current/no-op idempotence, dirty planned-write refusal, worktree creation failure, `--noworktree`, cache reuse/prune, GitHub failure latest mode, release asset integrity, older-updater handoff, recursion prevention, live config preservation, Sentrux baseline preservation, legacy pre-versioned mode, no-Git doti-shaped diagnostics, and target-root containment.
- [ ] `T016` (FR-021, FR-022, FR-023, FR-024, FR-025, FR-026, FR-027, FR-028, FR-101, FR-102, FR-103, FR-104, FR-105, FR-106, FR-107, FR-108, FR-109, FR-110, FR-111, FR-112, FR-114, FR-115) - Implement GitHub latest-release resolution, update cache, release asset verification, extracted executable at-use verification, older-updater handoff, and cache-prune safety in `src/Hx.Scaffold.Core/Update/*`.
- [ ] `T017` (FR-010, FR-011, FR-012, FR-013, FR-014, FR-015, FR-016, FR-017, FR-018, FR-020, FR-045, FR-046, FR-047, FR-048, FR-059, FR-060, FR-061, FR-062, FR-063, FR-064, FR-065, FR-078, FR-079, FR-080, FR-081, FR-082, FR-083, FR-084, FR-085, FR-086, FR-087, FR-118, SC-032) - Implement target repo validation, target-root containment, dirty planned-write collision detection, backup worktree creation/reporting, `--noworktree`, live configuration preservation, managed-asset reconciliation, and legacy pre-versioned conservative mode in `src/Hx.Scaffold.Core/Update/*`.
- [ ] `T018` (FR-007, FR-008, FR-009, FR-014, FR-018, FR-019, FR-020, FR-031, FR-034, FR-042, FR-043, FR-046, FR-048, SC-003, SC-004, SC-011, SC-016) - Wire `hx update` into `tools/Hx.Scaffold.Cli/Program.cs` and `tools/Hx.Scaffold.Cli/ScaffoldCommands.cs`; ensure rich/plain help and `describe --json` expose repository, dry-run, force, cache, `--noworktree`, version, JSON, and help-mode controls.
- [ ] `T019` (FR-048, FR-057, FR-074, FR-109, FR-117, FR-127, SC-016, SC-056, SC-057, SC-059, SC-061, SC-063) - Append stable diagnostics to `errorcodes/registry.json`, run `errorcodes render`, and update `errorcodes/shipped.json` only through the sanctioned stability path.
- [ ] `T020` (FR-019, FR-032, FR-036, FR-037, FR-078, FR-085, FR-087) - Update `src/Hx.Scaffold.Core/ScaffoldNewRunner.cs`, `src/Hx.Scaffold.Core/SourceVendor.cs`, `src/Hx.Scaffold.Core/StoreProvisioner.cs`, and related installer paths so generated repos carry version stamps, managed-asset metadata, and current Doti/update sources.
- [ ] `T021` (FR-132, FR-136, FR-137, FR-138, FR-139, FR-140, FR-141, FR-142, FR-143, FR-150, SC-066, SC-067, SC-070) - Add prerequisite manifest/trust tests in `test/Hx.Scaffold.Tests`: release manifest loads with schema/version identity, `.NET SDK` and Git are hard requirements for the right commands, tampered/missing/schema-invalid manifests fail closed, repo-local extensions cannot downgrade requirements or inject executable URLs, and `describe --json`/help expose preflight surfaces.
- [ ] `T022` (FR-132, FR-136, FR-137, FR-138, FR-139, FR-140, FR-141, FR-142, FR-143, FR-150) - Implement trusted prerequisite manifest and contracts in `src/Hx.Scaffold.Core/Prerequisites/*`, optional `tools/Hx.Tooling.Contracts/PrerequisiteResult.cs`, `doti/core/prerequisites.json`, and generated `.doti/prerequisites.json`; include `Microsoft.DotNet.SDK.10` and `Git.Git` winget mappings as release-defined data only.
- [ ] `T023` (FR-133, FR-134, FR-135, FR-144, FR-145, FR-146, FR-147, FR-148, SC-064, SC-065, SC-068, SC-069) - Add prerequisite/directory preflight tests in `test/Hx.Scaffold.Tests`: `hx new` missing .NET SDK/Git simulation leaves no output mutation, `hx update` missing Git leaves no backup worktree or target mutation, directory failures report before side effects, and repo-aware version/dry-run reports prerequisite health while remaining read-only.
- [ ] `T024` (FR-133, FR-134, FR-135, FR-144, FR-145, FR-146, FR-147, FR-148) - Wire prerequisite and directory preflight into `src/Hx.Scaffold.Core/ScaffoldNewRunner.cs`, `src/Hx.Scaffold.Core/Update/*`, `src/Hx.Scaffold.Core/Versioning/ScaffoldVersionReport.cs`, `tools/Hx.Scaffold.Cli/ScaffoldCommands.cs`, and `tools/Hx.Scaffold.Cli/ScaffoldCommandFactory.cs` before any side effects.
- [ ] `T025` (FR-149, FR-151, FR-152, FR-153, FR-154, FR-155, FR-156, FR-157, FR-158, FR-159, FR-160, SC-071, SC-072, SC-073, SC-074, SC-075, SC-076) - Add Windows winget install-flow tests in `test/Hx.Scaffold.Tests`: exact plan/digest, explicit approval required, `--force`/JSON/retry do not approve install, winget unavailable/blocked, no trusted mapping, repo-local mapping override refused, failed/cancelled winget, post-install probe failure, non-Windows unsupported, and dry-run/preview executes no package manager.
- [ ] `T026` (FR-149, FR-151, FR-152, FR-153, FR-154, FR-155, FR-156, FR-157, FR-158, FR-159, FR-160, SC-071, SC-072, SC-073, SC-074, SC-075, SC-076) - Implement `hx prereq check` and `hx prereq install` in `src/Hx.Scaffold.Core/Prerequisites/*`, `tools/Hx.Scaffold.Cli/Program.cs`, `tools/Hx.Scaffold.Cli/ScaffoldCommandFactory.cs`, and `tools/Hx.Scaffold.Cli/ScaffoldCommands.cs`; verify winget identity, bind operator approval to a plan digest, execute only trusted release-defined winget package/source actions, rerun probes, and report structured provenance without secrets.
- [x] `T027` (FR-161, FR-164, FR-165, FR-166, FR-167, FR-168, FR-169, SC-077, SC-078, SC-079, SC-080, SC-081) - Add hook ownership and auto-arming tests in `test/Hx.Runner.Tests` and `test/Hx.Scaffold.Tests`: absent hook installs, expected hook no-ops, older Doti hook refreshes, modified/non-Doti hook fails before managed update mutation, dry-run reports planned hook write without mutation, resolved `core.hooksPath` is honored, and normal bare commits are redirected for both Codex/Claude-shared checkouts.
- [x] `T028` (FR-161, FR-162, FR-163, FR-164, FR-165, FR-166, FR-167, FR-168, FR-169) - Implement hook health classification and auto-arming by reusing `tools/Hx.Cycle.Core/HookInstaller.cs`/`PrecommitGuard.cs`, adding hook ownership verdicts if needed, wiring `src/Hx.Scaffold.Core/ScaffoldNewRunner.cs`, `src/Hx.Scaffold.Core/Update/*`, and `tools/Hx.Runner.Cli/RunnerCommands.Doti.cs`; keep full verification logic in the Doti cycle.
- [x] `T029` (FR-170, FR-171, FR-172, SC-082, SC-083, SC-085) - Update Sentrux vendoring tests and manifests: assert `tools/sentrux/sentrux.version.json` records `heurexai/sentrux` `v0.5.11`, release-provided SHA-256 values for declared assets, tool fetch/store verification fails closed on hash/tag mismatch, and existing `.sentrux/baseline.json` / live config remain unchanged.
- [x] `T030` (FR-170, FR-171, FR-172) - Refresh `tools/sentrux/sentrux.version.json` to `v0.5.11` with the verified GitHub release asset URLs and byte-exact SHA-256 digests; update any shared-store or provisioning metadata needed so `tools fetch`, `hx new`, `hx update`, and generated-repo validation no longer target the older Sentrux release.
- [x] `T031` (FR-173, FR-174, SC-083, SC-084, SC-085) - Update Sentrux parser/gate tests and implementation in `tools/Hx.Sentrux.Core/*`, `tools/Hx.Gate.Core/GateRunner.cs`, and contracts so richer Sentrux help/failure detail is preserved in human and JSON output without reducing existing structured diagnostics or touching baselines/config.
- [x] `T032` (FR-019, FR-031, FR-066, FR-067, FR-068, FR-069, FR-119, FR-131, FR-132, FR-150, FR-151, FR-161, FR-162, FR-163, FR-170, FR-173, SC-016, SC-024, SC-025, SC-059, SC-070, SC-076, SC-077, SC-082, SC-084) - Update single-sourced Doti guidance in `doti/core/skills.json` and `doti/core/templates/agent-context-template.md`; render installed `.agents`, `.claude`, `.doti/agent-context.md`, and root entrypoints; do not hand-edit rendered files.
- [x] `T033` (FR-066, FR-067, FR-068, FR-069, FR-132, FR-149, FR-161, FR-170, SC-024, SC-025, SC-070, SC-077, SC-082) - Update release-readiness notes or docs that describe implemented behavior, prerequisite preflight/install status, auto hook arming, Sentrux `v0.5.11`, and minor-release classification.
- [x] `T034` (all FR/SC) - Run `/doti-analyze` coverage review against `docs/specs/cycle-completion-and-repo-update.md`, this task file, and the plan; fix zero-coverage requirements or orphan tasks before implementation.
- [x] `T035` (architecture) - Run `/doti-arch-review` after implementation design is concrete; if a new project such as `Hx.Update.Core` or a prerequisite core project appears, update `scaffold-dotnet.slnx`, `.sentrux/rules.toml`, and `rules/architecture.json` consistently before code lands.
- [x] `T036` (verification) - Run focused tests: `test/Hx.Runner.Tests`, `test/Hx.Doti.Tests`, `test/Hx.Scaffold.Tests`, `test/Hx.Impact.Tests`, `test/Hx.Cli.Kernel.Tests`, and architecture tests.
- [x] `T037` (verification) - Run command-backed checks: `dotnet restore .\scaffold-dotnet.slnx`, `dotnet build .\scaffold-dotnet.slnx -c Release --no-restore /m:1`, `dotnet test .\scaffold-dotnet.slnx -c Release --no-build /m:1`, `doti render-skills --check`, `architecture test`, `security scan`, `gate run --profile normal`, and `gate run --profile release` as release-readiness proof.
- [ ] `T038` (release artifact) - Discover and run the existing packaging/publish path to produce the release `hx.exe` archive/MSIX artifacts after the gates are green.
- [ ] `T039` (cycle and release) - Run `/doti-drift-review`, stamp `implement` and `drift-review`, persist a fresh gate proof, stage the scoped files, commit through `doti cycle commit`, then run `/doti-release` and the sanctioned minor-release path: `version bump --minor`, release gate, tag push, GitHub CI/release verification, release asset/MSIX confirmation, and README/docs consistency checks.

## Dependencies

- `T001` blocks `T002` and `T003`.
- `T004` blocks `T005`, `T006`, and `T007`.
- `T008` blocks `T009` and `T010`.
- `T011` blocks `T012`, `T013`, and `T014`.
- `T012` and `T014` block update reconciliation in `T017`.
- `T015` blocks `T016`, `T017`, and `T018`.
- `T016` blocks older-updater and cache verification portions of `T031`.
- `T017` blocks legacy/live-config/update report verification portions of `T031`.
- `T018` blocks `describe --json` and help verification.
- `T019` must land before final diagnostics are asserted in tests.
- `T020` depends on manifest/hash metadata from `T012`-`T014`.
- `T021` blocks `T022`, `T023`, `T024`, `T025`, and `T026`.
- `T022` blocks generated-repo prerequisite carriage in `T024` and guidance in `T027`.
- `T023` blocks `T024`; `T025` blocks `T026`.
- `T024` and `T026` block prerequisite help/describe assertions in `T027`.
- `T027` blocks `T028`.
- `T029` blocks `T030`; `T030` blocks Sentrux verify/check portions of `T031`.
- `T031` depends on the refreshed manifest and any updated Sentrux output fixtures from `T029`/`T030`.
- `T032` depends on implemented command availability and proof boundaries, including hook and Sentrux behavior.
- `T033` depends on implemented release-readiness behavior but not on actual release publication.
- `T034` runs before implementation and again after task changes if coverage changes.
- `T035` runs after implementation shape is known and before full gate.
- `T036` and `T037` depend on implementation tasks.
- `T038` depends on a green release-readiness gate.
- `T039` depends on `T037`, `T038`, and scoped staging.

## Coverage

| Requirement | Task(s) |
| --- | --- |
| FR-001..FR-006 | T001, T002, T003 |
| FR-007..FR-020 | T015, T017, T018 |
| FR-021..FR-028 | T015, T016 |
| FR-029..FR-035 | T008, T009, T010 |
| FR-036..FR-044 | T011, T012, T013, T014 |
| FR-045..FR-048 | T015, T017, T018, T019 |
| FR-049..FR-058 | T008, T009, T010, T019 |
| FR-059..FR-065 | T015, T017 |
| FR-066..FR-069 | T021, T022 |
| FR-070..FR-077 | T011, T012, T013, T019 |
| FR-078..FR-087 | T017, T020 |
| FR-088..FR-100 | T001, T002, T003 |
| FR-101..FR-112 | T015, T016 |
| FR-113..FR-118 | T001, T003, T011, T015, T016, T017 |
| FR-119..FR-131 | T004, T005, T006, T007, T019, T032 |
| FR-132..FR-143 | T021, T022, T023, T024, T032, T033 |
| FR-144..FR-148 | T023, T024 |
| FR-149..FR-160 | T025, T026, T032, T033 |
| FR-161..FR-169 | T027, T028, T032, T033 |
| FR-170..FR-174 | T029, T030, T031, T032, T033 |
| SC-001..SC-002 | T001, T002, T003 |
| SC-003..SC-011 | T015, T017, T018 |
| SC-012..SC-020 | T008, T009, T010 |
| SC-021..SC-023 | T015, T017 |
| SC-024..SC-025 | T032, T033, T039 |
| SC-026..SC-030 | T011, T012, T013 |
| SC-031..SC-033 | T017, T020 |
| SC-034..SC-040 | T001, T002, T003 |
| SC-041..SC-048 | T015, T016 |
| SC-049..SC-051 | T011, T012, T017 |
| SC-052..SC-059 | T004, T005, T006, T007, T019, T032 |
| SC-060..SC-063 | T015, T017, T019 |
| SC-064..SC-070 | T021, T022, T023, T024, T032, T033 |
| SC-071..SC-076 | T025, T026, T032 |
| SC-077..SC-081 | T027, T028, T032, T033 |
| SC-082..SC-085 | T029, T030, T031, T032, T033 |

## Current Status Notes

- Implemented in the current working tree and covered by focused tests plus normal/release gate proof: completed-cycle persisted status, completion intent, recovery convergence for status/check/stamp/commit, post-commit completion-write failure reporting, staged-tree binding, repo-aware version report, canonical scaffold identity with release asset fields, managed-asset manifest/hash metadata, parser-backed YAML/JSON hashing, safer normalized text hashing, category-specific template/skill modification reporting, `hx update`, dry-run, `--force`, `--noworktree`, dirty planned-path refusal, worktree backup/reversal reporting, live configuration preservation, legacy pre-versioned conservative update reporting, GitHub release cache resolution, older-updater handoff with at-use delegated executable verification, trusted prerequisite manifest/preflight, generated prerequisite-policy carriage, repo-aware prerequisite health, Windows-only operator-approved winget prerequisite installation, automatic hook arming from `hx new`/`hx update`/`doti install`, hook ownership conflict handling, Sentrux `v0.5.11` replacement, richer Sentrux failure detail preservation, and project-level release SCA scanning.
- Still advisory until separately implemented and proven: full appended error-code registry coverage for every update/version/cycle diagnostic, broader persisted `GateProof` producer provenance and proof digest beyond the current commit-intent/trailer binding, per-test execution artifact identity, external/bypass commit classification beyond local refusal, clean-checkout merge/release proof, local review artifact packaging, and broader repo-local prerequisite extension handling beyond refusing executable install metadata.

## Gate Notes

- Manual review is not deterministic gate proof.
- `hx update`, repo-aware version reporting, completed-cycle recovery, managed hash metadata, auto hook arming, Sentrux `v0.5.11`, and project-level security SCA are implemented in the current working tree and have normal/release gate proof.
- New update/version/cycle diagnostics currently appear as structured report diagnostics plus existing `VAL0001` CLI diagnostics; prerequisite-specific CLI diagnostics have appended registry codes and pass `errorcodes render/check`.
- Network access is allowed only for `hx update` latest-release behavior and explicit tool/release checks; it is not part of the offline gate.
- The active thread goal now includes completing the Doti cycle and release. The release deliverable includes trusted prerequisite preflight/install behavior, auto hook arming, Sentrux `v0.5.11`, a local `hx.exe`/MSIX artifact, a green release-readiness proof, sanctioned `/doti-release` work, minor version bump, release tag, push, GitHub CI/release verification, and README/docs consistency checks.
