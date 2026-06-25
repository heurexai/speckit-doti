# Plan: Task Hash-Gated Velopack Completion

> Spec: `docs/specs/006-task-hash-gated-velopack-completion.md`.

## Technical Context

This feature is a corrective release-governance pass over Doti and hx. It closes the gap where `v0.7.3` could release raw archive assets while Velopack work remained incomplete, and it also reshapes the Doti workflow so the cycle is code-enforced rather than agent-enforced.

Stack and constraints:

- .NET 10, `System.CommandLine`, existing `CliResult` envelope, existing `Hx.Cycle.Core`, `Hx.Gate.Core`, `Hx.Doti.Core`, `Hx.Scaffold.Cli`, `Hx.Tooling.Contracts`, and scaffold template projects.
- Microsoft.Extensions.Configuration becomes the configuration mechanism for hx and scaffold starter code.
- Velopack installer/update artifacts become the release product; source archives are not primary release assets.
- The self-hosted Doti assets in this repo and the scaffold-installed Doti payload must remain aligned by generation or drift checks.
- The old root `doti/` layout is obsolete after `.doti/` validation; migration must preserve repo-owned configuration and user/source files.
- The current cycle engine is still diff-bound and has an explicit `commit` stage; the new behavior is planned and must not be claimed as implemented until commands and tests exist.

Current implemented command surfaces:

- `Hx.Scaffold.Cli release --repo <path> [--major|--minor|--patch] [--release-root ...]` exists, but still exposes release-root flags and does not yet use executable-local Microsoft configuration.
- `Hx.Runner.Cli gate run --profile normal|release` exists, but does not yet include task-completion, Velopack-only artifact, documentation-update, or scaffold-payload-drift proof.
- `Hx.Runner.Cli doti cycle stamp/status/check/commit` exists, but the model is diff-bound and still contains the explicit commit stage.
- `doti render-skills --check` exists and currently renders from `doti/core/skills.json`.
- `DotiInstaller` currently copies root `doti/` into target repos before rendering.

Planned but absent command-backed behavior:

- Task-completion gate and task hash stamping/validation.
- Stage-transition commits at the start of the next stage.
- Multi-feature release-train tracking.
- Workflow-stage registry with two-digit ordered skill names and canonical next-step directives.
- Velopack-only installer/update release proof.
- Installer target classification and repo-directed Doti install/upgrade.
- Executable-local `hx.config.json` loaded through Microsoft.Extensions.Configuration.
- Documentation update proof during release.
- Scaffold payload parity/drift proof.
- Architecture-family truthfulness proof aligning `rules/architecture.json`, `architecture test` output, agent context, and arch-review skill guidance.

No `[NEEDS CLARIFICATION]` markers remain. The working default for the executable-local config file is `hx.config.json`; planning keeps it unless implementation finds an existing stronger .NET packaging convention.

## Constitution Check (Before Design)

PASS.

- **Deterministic Ownership:** all new enforcement moves into .NET commands, core services, persisted proof, and render/drift checks.
- **Bootstrap Honesty:** absent task-completion, transition-commit, release-train, installer, and config behavior is marked planned.
- **Template Boundary:** static scaffold starter files and generated payload layout stay in template assets; dynamic install/upgrade/migration/release proof stays in core services.
- **Public Hygiene:** release output moves away from source archives and must avoid developer-local paths in docs/proofs; local release roots live in local executable-adjacent config, not source defaults.
- **Cross-Platform:** no shell runners are introduced; OS differences stay behind .NET and pinned tools.
- **Codified Cycle:** this feature intentionally changes the codified cycle from explicit `doti cycle commit` to automatic transition commits; the constitution source and rendered constitution-bearing guidance must be migrated in the same implementation so the principle remains code-enforced. Until implemented, current cycle commands remain the actual gate.
- **Engineering Discipline:** the plan keeps tests/proof before behavior where practical, preserves user files during migration, and fails closed on ambiguous or modified managed assets.
- **Channel Independence:** behavior belongs in core libraries; CLIs remain parse/delegate/render.
- **Architecture Truthfulness:** current architecture guidance overclaims nine ArchUnitNET families while `rules/architecture.json` and `architecture test` expose two; this must be corrected before implementation is accepted.

## Research

- **Decision:** Build a workflow-stage registry in Doti core and render both agent skills and command endings from it.
  **Rationale:** `doti/core/skills.json` and command templates currently duplicate next-step wording. The registry can hold ordinal, display title, skill id, command, prerequisite/alternate actions, optionality, and canonical footer text.
  **Alternatives rejected:** continuing duplicated `nextStage` strings; asking agents to remember the order.

- **Decision:** Use two-digit ordered labels for user-facing stage names and skill identifiers.
  **Rationale:** `01-Specify` through `09-Release` sort naturally in agent skill selection and make the workflow obvious.
  **Alternatives rejected:** single-digit prefixes, unprefixed names, or prose-only order.

- **Decision:** Replace the explicit commit stage with automatic transition commits performed by the next stage command.
  **Rationale:** The operator does not want agents invoking commit commands. The next stage can finalize the previous stage before advancing, with generated subject `<stage>: <NNN-feature-slug>` and Doti trailers.
  **Alternatives rejected:** keeping `/doti-commit`; asking agents to run `git commit`; asking agents to run a separate Doti commit command.

- **Decision:** Treat multi-feature release as a release-train state in cycle core.
  **Rationale:** After `08-Drift-Review`, the operator can either release or begin another `01-Specify`. Completed unreleased feature cycles must remain discoverable and verifiable until one release includes them.
  **Alternatives rejected:** forcing immediate release after every feature; losing completed-cycle proof when a new spec starts.

- **Decision:** Add task-completion as a normal/release gate step and add a command-backed task hash updater.
  **Rationale:** The previous release passed with unchecked Velopack tasks. Gate proof must parse task files and fail on unchecked, missing-hash, or hash-mismatched tasks.
  **Alternatives rejected:** relying on agent honesty; making task checks advisory only.

- **Decision:** Canonicalize task hashes by parsing Markdown task records and normalizing whitespace/EOL before hashing identity + meaningful content.
  **Rationale:** The operator wants whitespace/EOL changes ignored but meaningful content changes detected. A structured record avoids raw-line hash brittleness while still catching changed ids, references, files, commands, and proof text.
  **Alternatives rejected:** raw file hashing; ignoring task content after checkboxing.

- **Decision:** Use Microsoft.Extensions.Configuration with executable-adjacent `hx.config.json` as the hx operational configuration source.
  **Rationale:** Configuration must be local to the hx executable, stable across current-directory/repo moves, and fail hard when absent.
  **Alternatives rejected:** `--release-root` flags, environment variables, repo-local config, user/global config, or implicit defaults when the config file is absent.

- **Decision:** Keep installer target behavior repo-directed and explicit.
  **Rationale:** The installer always receives a target directory. Missing/empty/non-empty-without-Doti targets get first-time Doti install; existing Doti-enabled targets get upgrade; no target fails before mutation.
  **Alternatives rejected:** global install; guessing current directory; treating non-empty/no-Doti targets as upgrades.

- **Decision:** Remove root `doti/` through manifest/hash-backed migration only after `.doti/` is installed and validated.
  **Rationale:** Current `DotiInstaller` copies root `doti/`; removing broadly would risk deleting local/user assets. Managed asset identity and canonical hashes give a safe cleanup boundary.
  **Alternatives rejected:** blanket directory delete; leaving root `doti/` orphaned; deleting modified managed files without force.

- **Decision:** Validate self-hosted Doti and scaffold-installed Doti payload parity in code.
  **Rationale:** The operator expects every Doti change to land in this repo and the scaffold payload. A drift check prevents generated repos from receiving stale workflows.
  **Alternatives rejected:** manual review only; updating one surface and relying on later cleanup.

- **Decision:** Make architecture-family truthfulness a release-blocking implementation task.
  **Rationale:** The command-backed architecture gate currently reports two families, while rendered guidance claims nine. This feature changes core/cycle/gate/release/config surfaces and cannot rely on overstated architecture proof.
  **Alternatives rejected:** leaving the mismatch as advisory; relying on Sentrux alone while ArchUnitNET guidance claims broader coverage.

## Design

### Phase 1: Workflow Registry And Ordered Skills

Files likely to change:

- `doti/core/skills.json`
- `doti/core/workflows/doti/workflow.yml`
- `.doti/workflows/doti/workflow.yml`
- `doti/core/templates/commands/*.md`
- `tools/Hx.Doti.Core/DotiRenderer.cs`
- new `tools/Hx.Doti.Core/Workflow/*` types
- `tools/Hx.Cycle.Core/StageModel.cs`
- `tools/Hx.Runner.Cli/RunnerCommandFactory.Cycle.cs`
- generated `.agents/skills/**`, `.claude/skills/**`, `.doti/agent-context.md`, `AGENTS.md`, `CLAUDE.md`

Approach:

- Introduce a workflow registry model with ordinal, stage id, command, display title, skill id, required/optional/conditional status, next actions, and generated footer text.
- Render skill directories using sortable identifiers such as `01-doti-specify` through `09-doti-release` and display titles `01-Specify` through `09-Release`.
- Remove `doti-commit` from the workflow registry and CLI surface entirely. Do not keep a compatibility command or diagnostic command surface.
- Generate command-template endings or make templates consume registry tokens so next-step wording is single-sourced.
- Encode these next-step rules:
  - `07-Implement` -> `08-Drift-Review` only.
  - `08-Drift-Review` -> `09-Release` or `01-Specify`.
  - `06-Arch-Review` is conditional/advisory unless architecture impact exists.
- Update `describe --json` for Doti workflow metadata so agents can discover stage order and next actions.

Architecture delta:

- Add a Doti workflow registry model under `tools/Hx.Doti.Core`.
- `Hx.Cycle.Core` can read the installed registry/stage model but must not depend on runner or scaffold CLI.
- If a new namespace is added under `Hx.Doti.Core.Workflow`, no new ArchUnit family is needed; existing thin-CLI and layer rules should cover it. Update `.sentrux/rules.toml` only if a new project is introduced.

### Phase 2: Task Hash Gate

Files likely to change:

- new `tools/Hx.Cycle.Core/Tasks/*` or `tools/Hx.Gate.Core/TaskCompletion/*`
- `tools/Hx.Gate.Core/GateRunner.cs`
- `tools/Hx.Tooling.Contracts/GateProof.cs`, `GateStep.cs`, and new task completion proof contracts
- `tools/Hx.Runner.Cli/RunnerCommandFactory.Cycle.cs`
- `tools/Hx.Runner.Cli/RunnerCommands.DotiCycle.cs`
- `errorcodes/registry.json`
- tests under `test/Hx.Cycle.Tests`, `test/Hx.Runner.Tests`, and `test/Hx.Gate.Tests` if present

Approach:

- Add a Markdown task parser that discovers required `- [ ]` / `- [x]` tasks for the active numbered feature task file.
- Define canonical task content as a structured record: repo-relative task path, feature slug, task id, task text, requirement refs, success criteria refs, declared files, commands, dependencies, and proof expectations.
- Normalize whitespace and EOL in record fields before SHA-256 hashing.
- Exclude checkbox marker and hash marker from content hash.
- Add a command-backed way to stamp checked task hashes and refuse unchecked tasks.
- Add a `task-completion` `GateRunner` step for normal and release profiles.
- Gate failures report path, line, task id, and reason for unchecked, missing hash, duplicate hash, or mismatch.
- Extend persisted gate proof validation so transition/release paths can require passing task-completion evidence.

Architecture delta:

- Prefer putting parser/hash logic in `Hx.Cycle.Core` if it is primarily cycle/task state, with `Hx.Gate.Core` consuming a pure service.
- If `Hx.Gate.Core` owns the step, keep reusable parser types in a core namespace and avoid CLI dependencies.
- Existing thin-CLI rules apply; no CLI business logic.

### Phase 3: Stage-Transition Commits And Release Trains

Files likely to change:

- `tools/Hx.Cycle.Core/CycleService.Stamp.cs`
- `tools/Hx.Cycle.Core/CycleService.Check.cs`
- `tools/Hx.Cycle.Core/CycleService.Commit*.cs` removed/compatibilized
- `tools/Hx.Cycle.Core/CycleStateStore.cs`
- `tools/Hx.Cycle.Core/CycleReports.cs`
- `tools/Hx.Cycle.Core/ChangeSetIdentity.cs`
- `tools/Hx.Cycle.Core/PrecommitGuard.cs`
- `tools/Hx.Cycle.Core/HookInstaller.cs`
- `tools/Hx.Runner.Cli/RunnerCommandFactory.Cycle.cs`
- `tools/Hx.Runner.Cli/RunnerCommands.DotiCycle.cs`
- `tools/Hx.Tooling.Contracts/CycleState.cs`
- `.doti/memory/constitution.md`
- `doti/core/memory/constitution.md`
- `doti/core/templates/agent-context-template.md`
- `.doti/workflows/doti/workflow.yml`
- `doti/core/workflows/doti/workflow.yml`

Approach:

- Add a transition entrypoint used when a non-initial stage starts. It finalizes the previous current stage before running/checking the requested stage.
- Generate transition commit subjects as `<stage>: <NNN-feature-slug>` and trailers with feature slug, stage id, ordinal, stage proof hash, previous stage commit, staged tree, gate proof where required, and runner identity.
- Advance the cycle baseline after each transition commit so the requested next stage evaluates the previous stage as fresh even though the diff was committed.
- Reconstruct freshness from Git trailers if `.doti/cycle-state.json` is missing or stale.
- Support no-file-change transition commits for review stages with explicit Doti trailers.
- Remove `commit` from workflow stages and rendered skills.
- Remove old `doti cycle commit` runner command registration, help, describe output, rendered guidance, and source command assets so it is not discoverable as a supported command.
- Update the Doti constitution and rendered constitution-bearing guidance so they name automatic stage-transition commits as the sanctioned path and no longer say `doti cycle commit` is the only sanctioned commit path.
- Track completed unreleased feature cycles. Starting `01-Specify` after `08-Drift-Review` finalizes drift-review, stores that feature as unreleased, and creates a new active feature.
- Release aggregates all completed unreleased features into a release train and marks them released only after release proof succeeds.

Architecture delta:

- This is a meaningful `Hx.Cycle.Core` model change. It may require replacing `kind: commit` with richer stage metadata.
- Update ArchUnit/Sentrux only if new projects or cross-layer dependencies are introduced. Keep Git process interaction inside existing cycle core/process adapter patterns.

### Phase 4: Velopack Installer/Update And Doti Asset Migration

Files likely to change:

- `tools/Hx.Doti.Core/DotiInstaller.cs`
- `tools/Hx.Doti.Core/ManagedAssets/*`
- new Doti installer/update target classifier types
- `tools/Hx.Scaffold.Cli` installer/update entrypoint or Velopack bootstrap wiring
- `src/Hx.Scaffold.Core/Release/*`
- `tools/Hx.Tooling.Contracts/LocalReleaseResult.cs`
- `.github/workflows/release.yml`
- `doti/core/templates/commands/doti-release.md`
- tests under `test/Hx.Doti.Tests`, `test/Hx.Scaffold.Tests`, `test/Hx.Templates.Tests`

Approach:

- Make release output Velopack installer/update artifacts and metadata only; source archives are not primary products.
- Store Velopack release-owned tool metadata at `tools/velopack/velopack.version.json`, following the existing vendored-tool manifest shape used by Gitleaks, Sentrux, and GitVersion; downloaded binaries belong under `tools/velopack/bin/<rid>/` and must be hash verified.
- Add an installer/update entrypoint that requires explicit target directory and classifies:
  - missing directory -> create directory, install Doti, print scaffold next-step instruction;
  - empty directory -> install Doti, print scaffold next-step instruction;
  - non-empty without Doti -> first-time Doti install preserving files and refusing unsafe overwrites;
  - existing Doti-enabled repo -> upgrade/migrate Doti assets in place.
- Move supported Doti source authority to `.doti/` and stop installing root `doti/`.
- Add a managed removal manifest for obsolete Doti-owned paths, including root `doti/`.
- Remove obsolete Doti-owned files only when canonical managed hash/identity proves safety, or when an explicit supported force option is used.
- Preserve repo-owned specs, plans, tasks, docs, source, `.doti/release.json`, prerequisite policy state, Sentrux config/baselines, cycle/gate state, and live configuration.
- Emit update proof with installed, upgraded, removed, preserved, skipped, and blocked paths.
- Add scaffold payload parity check so generated repos receive the same Doti assets as this repo.

Architecture delta:

- Keep installer/migration classification in `Hx.Doti.Core`; scaffold CLI and Velopack entrypoints delegate to that core.
- If an installer host project is introduced for Velopack, add it to solution, Sentrux layer config, and architecture rules in the same change.

### Phase 5: hx Local Configuration And Scaffold Starter Configuration

Files likely to change:

- `tools/Hx.Scaffold.Cli/Hx.Scaffold.Cli.csproj`
- `tools/Hx.Scaffold.Cli/ScaffoldCommandFactory.cs`
- `tools/Hx.Scaffold.Cli/ScaffoldCommands.Release.cs`
- new `src/Hx.Scaffold.Core/Configuration/*` or equivalent core service
- `tools/Hx.Tooling.Contracts/LocalReleaseResult.cs`
- `scaffold/templates/dotnet-cli/src/**`
- `scaffold/templates/dotnet-cli/test/**`
- `Directory.Packages.props`
- README/docs/help

Approach:

- Add `Microsoft.Extensions.Configuration`, JSON provider packages, and options binding/validation as needed.
- Define executable-adjacent config file name `hx.config.json` unless implementation finds a stronger convention.
- Load config from `AppContext.BaseDirectory` / executable directory, not current directory, repo directory, environment, user profile, or machine-global config.
- Missing config file fails hard for every hx operational command, including `new`, `version`, `release`, prerequisite install, and installer/update target commands. Non-mutating help/describe discovery commands may run without config only to explain command shape and the config requirement.
- Local release output defaults enabled from config; if enabled, `directory` must be present, absolute, and valid before release mutation.
- If disabled, no directory is required and release result says local copy disabled by config.
- Remove `--release-root`, `--release-root-env`, and `--save-release-root` from help, `describe --json`, docs, and tests.
- Generate/install a scaffold-local hx config file beside the scaffolded/released hx executable.
- Add Microsoft.Extensions.Configuration support to the scaffold starter app using the same executable-local JSON posture and fail-hard required config behavior.

Architecture delta:

- Configuration loading should live in a core service so CLI remains thin.
- Generated scaffold starter may add a small configuration abstraction in the app/template; update template architecture tests if new config types affect project boundaries.

### Phase 5A: Architecture Contract Alignment

Files likely to change:

- `rules/architecture.json`
- `test/Hx.Architecture.Tests/**`
- `tools/Hx.Runner.Core/ArchitectureGate/*`
- `doti/core/templates/commands/doti-arch-review.md`
- `doti/core/templates/agent-context-template.md`
- `.doti/agent-context.md`
- rendered `.agents/skills/**/SKILL.md` and `.claude/skills/**/SKILL.md`

Approach:

- Add a failing test that detects architecture-family overclaim: `rules/architecture.json`, `architecture test --json`, agent context, and rendered arch-review guidance must agree on family count and ids.
- Either restore the nine intended ArchUnitNET families as command-backed tests or change generated guidance to describe only the implemented families. If nine families remain documented, `architecture test` must report those nine families, not two.
- Keep Sentrux as the path/layer/cycle boundary engine, but do not let ArchUnitNET guidance claim Sentrux-only checks as ArchUnitNET proof.
- Ensure any new project/namespace/layer introduced by this feature updates `scaffold-dotnet.slnx`, `.sentrux/rules.toml`, `rules/architecture.json`, and architecture fixtures in the same change.

Architecture delta:

- This is an architecture-gate correction, not product behavior. The implementation should live in `test/Hx.Architecture.Tests` and runner architecture proof/reporting code only where needed to expose accurate family metadata.

### Phase 6: Release Documentation Proof

Files likely to change:

- `doti/core/templates/commands/doti-release.md`
- release workflow/registry surfaces
- `tools/Hx.Gate.Core/GateRunner.cs`
- `tools/Hx.Tooling.Contracts/*`
- README and docs
- tests for documentation proof

Approach:

- Generate release notes from included release train features.
- Require release-stage guidance to update README and relevant repo Markdown docs before release.
- Add documentation update proof listing inspected docs, updated/no-change status, and reasons.
- Fail release when release notes describe user-facing, workflow, CLI, installer/update, compatibility, security, or operational behavior not reflected in README/relevant docs.
- Ensure docs updates are part of codified release/transition commits and not left as advisory follow-up.

Architecture delta:

- Documentation proof can be a `Hx.Gate.Core` release step consuming a pure doc inventory service. Avoid direct CLI writes.

### Phase 7: Gate Integration, Error Codes, And Proof

Files likely to change:

- `tools/Hx.Gate.Core/GateRunner.cs`
- `tools/Hx.Tooling.Contracts/GateProof.cs`
- `tools/Hx.Runner.Cli/RunnerCommandFactory.Gates.cs`
- `errorcodes/registry.json`
- `schemas/*`
- README/docs/agent context
- `.github/workflows/release.yml`

Approach:

- Add gate steps for task completion, Velopack artifact proof, documentation update proof, and scaffold payload drift.
- Extend release proof with release-train features, Velopack artifacts, update metadata, hx config status, docs proof, and installer target/update proof where applicable.
- Add stable error codes for task hash failures, release train invalidity, transition commit failures, missing hx config, invalid local release directory, installer target missing, managed asset modification, obsolete removal blocked, docs proof stale, Velopack artifact missing, and scaffold payload drift.
- Update `describe --json` for all changed/removed commands and options.
- Update GitHub release workflow to publish installer/update artifacts only.

Architecture delta:

- Existing gate and CLI architecture families should remain valid if new logic lands in core libraries, but architecture-family guidance must no longer overclaim rule families that are not reported by the architecture proof.
- If new contracts are added, keep them additive and schema-versioned where needed.

## CLI Surface & Error Contract

### Changed: `hx release`

Command surface:

```text
hx release --repo <path> [--rid <rid>] [--major|--minor|--patch] [--json]
```

Removed options:

- `--release-root`
- `--release-root-env`
- `--save-release-root`

Configuration:

- Loads `hx.config.json` from the running executable directory through Microsoft.Extensions.Configuration.
- Missing config file for operational commands: `Validation` before mutation or repo inspection, with a stable missing-config code. Help and `describe --json` remain non-mutating discovery surfaces and must document the config requirement.
- Local release output enabled with no valid absolute directory: `Validation`, before tag/artifact/filesystem mutation.
- Local release output disabled: `Success` may proceed without local copy and must report disabled status.

Exit classes:

- `Success`: release train valid, docs proof valid, local hx config valid or local release disabled, tag/version/artifacts verified.
- `Usage`: invalid CLI flags, mutually exclusive release intent flags, invalid repo/rid syntax.
- `Validation`: missing hx config, invalid local release directory, missing/stale gate proof, release train invalid, task/docs proof failure, GitVersion intent mismatch, installer target validation failure.
- `Integrity`: tag identity conflict, managed asset hash mismatch, Velopack payload hash mismatch, scaffold payload drift.
- `Internal`: unexpected process/serialization failure.

Describe entry:

- Must expose release intent flags and `--repo`/`--rid`/`--json`.
- Must not expose removed release-root options.
- Must report config file name/path policy and failure semantics in command metadata/notes if describe supports notes.

Envelope:

- Continue standard `CliResult`.
- Extend `LocalReleaseResult` with config source, local release setting, release train feature list, documentation proof, Velopack artifacts/update metadata, tag identity, and skipped-copy reason when disabled.

### Changed: Doti workflow/cycle commands

Surfaces:

- Stage transition behavior is invoked internally when a next Doti stage starts.
- `doti cycle commit` is removed from the normal workflow and is not retained as a compatibility diagnostic.
- `doti cycle status --json` reports active feature, completed unreleased cycles, released cycles, current stage, and freshness reconstructed from transition trailers.
- Task hash command is added for stamping/refreshing checked tasks.

Exit classes:

- `Usage`: unknown stage, invalid feature slug, invalid task hash command usage.
- `Validation`: prerequisite stale/missing, previous-stage transition commit cannot be made, unchecked tasks, missing/mismatched task hashes, release train invalid.
- `Integrity`: transition trailer/hash mismatch, managed proof mismatch, duplicate task hash identity.
- `Internal`: unexpected git/IO/serialization failure.

Core boundary:

- Stage transition commit logic lives in `Hx.Cycle.Core`.
- Task parsing/hash logic lives in a core library.
- CLI command code remains wiring-only.

### Changed/New: Doti installer/update entrypoint

Surface:

```text
doti install --repo <target-directory> --agents <agents> [--force] [--json]
```

or the equivalent Velopack installer target argument.

Contract:

- No target directory fails hard before mutation.
- Missing/empty/non-empty-without-Doti targets are first-time installs.
- Existing Doti-enabled targets are upgrades.
- Root `doti/` and obsolete Doti assets are removed only through managed manifest/hash proof.
- Output includes target classification and install/update proof.

Exit classes:

- `Usage`: missing target, invalid agents, unsupported option combination.
- `Validation`: unsafe overwrite, modified managed asset without force, invalid target path, missing required source payload.
- `Integrity`: managed hash mismatch, scaffold payload drift, package payload identity mismatch.
- `Success`: install/upgrade completed with proof.

## Command Availability

| Area | Command | Status |
| --- | --- | --- |
| Restore | `dotnet restore .\scaffold-dotnet.slnx` | implemented |
| Build | `dotnet build .\scaffold-dotnet.slnx -c Release --no-restore /m:1` | implemented |
| Test | `dotnet test .\scaffold-dotnet.slnx -c Release --no-build /m:1` | implemented |
| Platform probe | `dotnet run --project tools/Hx.Runner.Cli -- platform probe` | implemented |
| Hygiene changed | `dotnet run --project tools/Hx.Runner.Cli -- hygiene scan --repo . --scope changed --source staged --json` | implemented |
| Sentrux verify | `dotnet run --project tools/Hx.Runner.Cli -- sentrux verify --repo . --json` | implemented |
| Architecture | `dotnet run --project tools/Hx.Runner.Cli -- architecture test --repo . --json` | implemented |
| Architecture family truthfulness | architecture family ids/count match across `rules/architecture.json`, `architecture test`, agent context, and rendered arch-review skill | planned |
| Gate normal | `dotnet run --project tools/Hx.Runner.Cli -- gate run --repo . --profile normal --json` | implemented; must gain task-completion/scaffold-payload proof |
| Gate release | `dotnet run --project tools/Hx.Runner.Cli -- gate run --repo . --profile release --json` | implemented; must gain task-completion, Velopack, docs, release-train proof |
| Skill render | `dotnet run --project tools/Hx.Runner.Cli -- doti render-skills --repo . --agents codex,claude --check --json` | implemented; must gain registry/numbering/payload parity checks |
| Cycle stamp/status/check/commit | `dotnet run --project tools/Hx.Runner.Cli -- doti cycle ...` | implemented; must move from explicit commit stage to transition commits/release trains |
| Task hash command | `dotnet run --project tools/Hx.Runner.Cli -- doti task-hash stamp --repo . --feature <NNN-slug> --json` | planned |
| hx release | `dotnet run --project tools/Hx.Scaffold.Cli -- release --repo . --major|--minor|--patch --json` | implemented partially; remove release-root flags and add hx config/Velopack/docs proof |
| Doti install/upgrade target | `doti install --repo <target> --agents ... --json` / Velopack installer target | implemented partially for repo assets; must gain target classification, `.doti`-only migration, obsolete removal |
| Velopack pack | pinned `vpk pack` via release service | planned |
| Scaffold payload drift | `dotnet run --project tools/Hx.Runner.Cli -- doti payload check --repo . --json` and gate-integrated parity proof | planned |

## Constitution Re-Check (After Design)

PASS.

- Deterministic ownership is preserved by moving checks into core services, gates, render checks, and release proof.
- Bootstrap honesty is preserved by marking absent transition, task hash, config, installer, Velopack, docs, and release-train proof as planned.
- Template boundary is preserved: template starter files are static, dynamic finishing/install/release/migration stays in core services.
- Public hygiene is preserved by avoiding source archives as product output and keeping local release paths in local config.
- Cross-platform remains .NET-hosted; no shell runners are added.
- Codified cycle is changed by design, but still enforced by code, not agent convention; the constitution update is part of the planned implementation and is not optional.
- Channel independence remains intact by keeping CLI classes thin.
- Architecture truthfulness is now explicitly planned so guidance cannot claim nine ArchUnitNET families while the command-backed proof reports two.

## Complexity Tracking

None.

## Risks

- **Cycle model migration risk:** Changing from diff-bound final commit to transition commits affects every stage. Mitigate with isolated Git fixture tests for transition commits, trailer recovery, no-file-change stages, and multi-feature release trains before touching release logic.
- **Scope size risk:** This feature spans cycle, gate, release, installer, renderer, templates, docs, and CI. Mitigate by implementing in phases and keeping task coverage explicit.
- **Root `doti/` removal risk:** Broad deletion could destroy modified or repo-owned files. Mitigate with managed asset manifests, canonical hashes, exact diagnostics, and force-only replacement.
- **Velopack proof risk:** Package shape can vary by RID. Mitigate with active RID hard proof and advisory/fail-closed behavior for undeclared RIDs.
- **hx config packaging risk:** Executable-adjacent config path differs between dev, published, and Velopack install layouts. Mitigate with unit tests over `AppContext.BaseDirectory`/injected base path and packaged smoke tests.
- **Scaffold parity risk:** Updating this repo without scaffold payload drift checks would ship stale workflows to generated repos. Mitigate with scaffold payload drift gate and generated repo fixture assertions.
- **Docs proof risk:** Detecting stale docs is partly semantic. Mitigate with a release notes inventory plus explicit inspected/updated/no-change docs proof; keep semantic review advisory where pure code cannot prove meaning.

## Verification Plan

- Unit and fixture tests:
  - task parser/hash canonicalization with whitespace/EOL-insensitive cases and meaningful-change cases;
  - checked task missing/mismatched hash failures;
  - task hash stamping refuses unchecked tasks;
  - stage transition commit subject/trailers and no-file-change transitions;
  - trailer-based cycle recovery when `.doti/cycle-state.json` is missing;
  - multi-feature completed-unreleased release train state;
  - workflow registry rendering for `01-*` skill names and canonical next-step text;
  - `07-Implement` cannot branch directly to `01-Specify`;
  - `08-Drift-Review` can branch to `09-Release` or `01-Specify`;
  - `doti-commit` removed with no compatibility diagnostic;
  - constitution source and rendered guidance no longer state that `doti cycle commit` is the sole sanctioned commit path;
  - hx local config missing for operational commands, help/describe discovery without config, disabled local release output, enabled-without-directory, enabled-with-directory;
  - removed release-root flags absent from help and `describe --json`;
  - installer target classification for missing/empty/non-empty-no-Doti/existing-Doti/no-target;
  - managed obsolete asset removal preserves custom/live config and blocks modified managed files;
  - scaffold starter Microsoft.Extensions.Configuration support and missing required config failure;
  - scaffold payload parity drift failure.
  - architecture-family truthfulness: `rules/architecture.json`, `architecture test --json`, `.doti/agent-context.md`, and rendered arch-review skill agree on family count and ids.
- Command proof:
  - `dotnet restore .\scaffold-dotnet.slnx`;
  - `dotnet build .\scaffold-dotnet.slnx -c Release --no-restore /m:1`;
  - focused `dotnet test` for cycle/gate/doti/scaffold/template tests;
  - `dotnet run --project tools/Hx.Runner.Cli -c Release --no-build -- doti render-skills --repo . --agents codex,claude --check --json`;
  - `dotnet run --project tools/Hx.Runner.Cli -c Release --no-build -- gate run --repo . --profile normal --json`;
  - `dotnet run --project tools/Hx.Runner.Cli -c Release --no-build -- gate run --repo . --profile release --json`;
  - `dotnet run --project tools/Hx.Scaffold.Cli -c Release --no-build -- describe --json`;
  - `dotnet run --project tools/Hx.Runner.Cli -c Release --no-build -- describe --json`;
  - local Velopack package inspection for active RID.
- Search proof:
  - no live `doti-commit` skill or normal workflow stage;
  - no live `--release-root`, `--release-root-env`, or `--save-release-root` help/describe/docs guidance;
  - no live source-archive-only release workflow;
  - no generated skill command endings outside workflow registry;
  - no scaffold-installed Doti payload drift from self-hosted assets.
