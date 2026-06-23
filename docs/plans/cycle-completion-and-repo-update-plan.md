# Plan: Completed cycle status and existing-repo update

> Spec: `docs/specs/cycle-completion-and-repo-update.md`. Complete the Doti cycle and release through the sanctioned release lane: implement the remaining hook/Sentrux deltas, pass gates, commit through `doti cycle commit`, run `/doti-release`, create the minor version release, and verify CI/release assets.

## Technical Context

The work spans eight scaffold surfaces, including the new prerequisite manifest/policy surface, auto-armed hook surface, and refreshed Sentrux fork:

1. **Cycle completion and recovery** in `Hx.Cycle.Core`. `doti cycle stamp`, `status`, `check`, and `commit` share `CycleService`, `CycleStateStore`, `FreshnessEvaluator`, `GateProofStore`, `CommitScopeInspector`, `PrecommitGuard`, and `GateProofValidator`. The current working tree persists completed-cycle records, writes pre-commit intent, recovers completed commits, and returns an explicit recovery-needed result if completion-state persistence fails after Git creates the commit; final full-gate proof is still pending.
2. **Gate proof provenance and bypass hardening** across `Hx.Gate.Core`, `Hx.Cycle.Core`, and `Hx.Tooling.Contracts`. The gate already persists a change-set-bound proof and recomputable affected-test hashes, but the planned feature needs stronger producer provenance, proof digest identity, execution artifact identity, staged-tree binding, final commit trailers, hook-health reporting, and external/bypass commit verdicts.
3. **Existing-repo update** in the standalone scaffold executable (`Hx.Scaffold.Cli`). `Hx.Scaffold.Cli new` delegates to `Hx.Scaffold.Core`; `hx update`, repo-aware version reporting, older-updater handoff, dry-run, worktree backup, manifest reconciliation, and live-config preservation are implemented in the current working tree and covered by focused tests. Network-backed GitHub behavior remains release-environment dependent until release assets exist with checksums.
4. **Managed Doti asset ownership** in `Hx.Doti.Core` and source assets under `doti/`, `.doti/`, `.agents/`, and `.claude/`. `doti install` and `doti render-skills` install and render assets; the current working tree adds a managed-asset manifest, canonical hash baseline, category-specific modification report, and source-format/canonicalizer/conflict-policy metadata.
5. **Version and release asset identity** in `Hx.Version.Core`, packaging assets, and release manifests. `version calculate` and `version bump` exist; the current working tree adds a canonical scaffold product version stamp, repo-aware version report, target-to-latest relation in update reports, and release asset name/hash identity when a GitHub release payload installs or updates a repo.
6. **Trusted prerequisite policy and preflight** in `Hx.Scaffold.Core` with optional contracts in `Hx.Tooling.Contracts`. No prerequisite implementation exists yet; this plan adds a manifest-driven preflight before `hx new`, `hx update`, repo-aware version reporting, and generated-repo validation, plus Windows-only operator-approved winget remediation. The first hard requirements are a compatible .NET SDK and Git. On 2026-06-23, `winget show --id Microsoft.DotNet.SDK.10 --exact` verified `Microsoft.DotNet.SDK.10` and `winget show --id Git.Git --exact` verified `Git.Git`; exact supported version ranges remain trusted manifest data.
7. **Auto-armed Doti insurance hook** across `Hx.Cycle.Core`, `Hx.Scaffold.Core`, update orchestration, and Doti install orchestration. `HookInstaller` and `PrecommitGuard` already exist in `Hx.Cycle.Core`; this plan wires that existing command-backed hook surface into `hx new`, `hx update`, and `doti install`, while adding hook ownership/conflict classification before any automatic write.
8. **Vendored Sentrux fork refresh** across `tools/sentrux/sentrux.version.json`, tool-store provisioning, Sentrux verification/check parsing, gate reporting, and release/generation docs. Current local context says Sentrux is vendored as `v0.5.10`; GitHub API verification on 2026-06-24 reported `heurexai/sentrux` latest release `v0.5.11` with platform asset digests and richer help/failure details requested by the operator.

Stack and constraints: .NET source only, existing `System.CommandLine` and `CliResult` envelope, existing shared rich/plain help renderer, YamlDotNet 18.0.0 already pinned, `System.Text.Json` already used for contracts, GitVersion already vendored, no shell runners, no committed release binaries, and no release action during this cycle. Network access is explicit for `hx update`; winget execution is explicit, Windows-only, and operator-approved; offline gates remain offline.

## Constitution Check (gate)

PASS before design:

- **Deterministic Ownership** - cycle recovery, update reconciliation, hash detection, version comparison, and bypass classification will be source-controlled .NET behavior, not agent convention.
- **Bootstrap Honesty** - implemented working-tree commands are identified as implemented pending final full-gate proof; broader proof-provenance and release-lane items remain advisory until separately implemented.
- **Template Boundary** - static template layout remains template-owned; scaffold finishing/update orchestration remains scaffold CLI/core-owned; generated skills remain rendered from `doti/core/skills.json`.
- **Public Hygiene** - release caches and downloaded assets stay outside the repo; reports avoid committing local machine paths except repo-relative diagnostics.
- **Cross-Platform Rule** - no PowerShell or Bash runner is added; Git operations, hashing, update, and recovery logic stay in .NET/dotnet-hosted tooling.
- **Codified Cycle** - the design strengthens cycle stamps, `gate run`, and `doti cycle commit`; final commits remain through the sanctioned path.
- **Engineering Discipline** - implementation must prove update/report/recovery behavior with tests and command-backed gates before claiming completion.
- **Channel Independence** - logic lives in core libraries; CLI projects only parse options, inject adapters, and render `CliResult`.
- **Prerequisite trust boundary** - prerequisite policy and winget package metadata are release-defined managed assets or immutable built-in anchors; repo-local extensions cannot weaken requirements or inject executable URLs.
- **Hook boundary** - automatic arming installs only the thin untracked insurance hook; Doti remains enforced by command-backed cycle checks, not hook trust.
- **Sentrux baseline boundary** - the Sentrux executable/manifest may be refreshed, but target-owned `.sentrux` baselines and live configuration stay outside managed update replacement.

No violation, so Complexity Tracking is empty.

## Research (resolve unknowns)

- **Decision:** Keep update orchestration in `src/Hx.Scaffold.Core` behind a new update service, with `Hx.Scaffold.Cli` adding thin `update` and repo-aware `version` command wiring.
- **Rationale:** The standalone scaffold executable owns `new` today and is the requested `hx.exe` surface for updating existing repos. `Hx.Scaffold.Core` already orchestrates dynamic finishing and references Doti/tooling cores.
- **Alternatives rejected:** Put update in `Hx.Runner.Cli` (wrong user-facing executable); create shell scripts (violates Cross-Platform Rule); hand-copy assets (violates Deterministic Ownership).

- **Decision:** Add a managed asset manifest to `Hx.Doti.Core` and target repo metadata under `.doti/` rather than deriving ownership from directory names at update time.
- **Rationale:** The spec requires deterministic ownership categories, canonical identity policy, conflict policy, generated/replaced metadata classification, legacy possible-orphan reporting, and precise modified-path diagnostics.
- **Alternatives rejected:** Filename heuristics only (unsafe for legacy repos); use Git tracked status as ownership (does not distinguish live config from managed Doti assets); agent judgment inside updater (explicitly excluded by the spec).

- **Decision:** Use named canonicalization profiles: byte-exact for binary/integrity assets, RFC 8785-compatible JSON canonicalization for JSON, and parser-backed YAML representation canonicalization via YamlDotNet for YAML.
- **Rationale:** The spec requires presentation-only whitespace/EOL changes not to count as customization for structured textual content, while structural/scalar changes must change the hash. YamlDotNet is already pinned, and RFC 8785 is the accepted JSON reference.
- **Alternatives rejected:** Raw bytes for all textual files (false positives on formatting-only changes); ad hoc whitespace stripping (unsafe for YAML); LLM semantic comparison (not deterministic).

- **Decision:** Treat non-Git doti-shaped directories as recognizable but unsupported by default, returning a no-Git-recovery diagnostic before mutation.
- **Rationale:** The default update contract requires a backup Git worktree from committed `HEAD`; supporting non-Git mutation would create a different recovery model and is not needed for the requested existing-repo workflow.
- **Alternatives rejected:** Allow non-Git update with `--noworktree` now (broadens scope and weakens recovery); silently treat as invalid path (hides useful migration diagnostics).

- **Decision:** The default backup worktree preserves committed `HEAD` only. Dirty non-managed edits in the original checkout are reported separately, not represented as recoverable by the backup worktree.
- **Rationale:** Git worktrees are commit/ref based. The spec now requires output to state this limit so the report does not overpromise recovery.
- **Alternatives rejected:** Attempt to copy uncommitted edits into the backup worktree (not a Git worktree backup and risks user-code mutation); require a completely clean repo (unnecessary for non-managed user edits).

- **Decision:** Older-updater handoff happens before managed-asset reconciliation and before backup-worktree creation whenever the selected compatible release is newer than the parent `hx`, the target is newer than the parent, or the installed executable may be locked.
- **Rationale:** The newer release may define manifest/hash/update semantics the parent must not interpret, and Windows executable locking can prevent self-replacement.
- **Alternatives rejected:** Let older parent mutate target directly (explicitly excluded); update the running executable in place (lock-prone); require manual download (fails product workflow).

- **Decision:** Completed-cycle recovery uses a durable commit-completion intent plus Git evidence and converges to completed, retryable-active, or ambiguous.
- **Rationale:** Once `git commit` created the sanctioned commit, users should not be trapped in a stale-proof loop or asked to create a second commit. Ambiguous history changes must still fail closed.
- **Alternatives rejected:** Restamp old stages after commit (breaks proof boundary); rerun gate as primary recovery (does not prove the already-created commit); auto-reset/amend/delete commits (explicitly excluded).

- **Decision:** Implement prerequisites as a release-defined manifest and core preflight service in `Hx.Scaffold.Core`, with shared result contracts only if needed for generated-repo validation.
- **Rationale:** `hx new`, `hx update`, and repo-aware `hx version` are `Hx.Scaffold.Cli` surfaces backed by `Hx.Scaffold.Core`; placing policy/probe/install planning there keeps CLI bodies thin while avoiding a new project. Generated repos can carry the same trusted manifest as a managed Doti asset.
- **Alternatives rejected:** Hard-code checks in each command body (violates manifest-driven requirement and thin CLI); allow target repos to provide package metadata (trojan risk); create shell bootstrap scripts (violates Cross-Platform Rule).

- **Decision:** Add a separate `hx prereq check` / `hx prereq install` command group while also running read-only preflight automatically inside `new`, `update`, and repo-aware `version`.
- **Rationale:** Ordinary commands must fail before side effects when hard prerequisites are missing, but installation must be intentional, separately discoverable, and not implied by `--force`, JSON mode, dry-run, or an agent retry. Non-interactive/JSON install flows should emit an install plan with a digest and `requiresOperator=true`; executing the plan requires an explicit confirmation bound to that digest.
- **Alternatives rejected:** `hx new --install-missing` (too easy for agents to retry into mutation); silent install on failure (explicitly excluded); instructions-only forever on Windows (does not meet the approved winget automation requirement).

- **Decision:** The initial Windows winget mappings are `Microsoft.DotNet.SDK.10` for the .NET SDK and `Git.Git` for Git, both from the default `winget` source.
- **Rationale:** These identifiers were verified from the local winget source on 2026-06-23 with `winget show --id Microsoft.DotNet.SDK.10 --exact` and `winget show --id Git.Git --exact`. The manifest records package/source identifiers and supported version policy; it must not trust repo-local executable URLs.
- **Alternatives rejected:** Record raw installer URLs from winget output (stale and easier to subvert); use broad search terms like `dotnet` or `git` (ambiguous); add Linux/macOS package-manager automation now (operator explicitly withdrew that expansion).

- **Decision:** Wire automatic hook arming through the existing `Hx.Cycle.Core.HookInstaller` and add hook ownership classification beside that hook surface, with Scaffold/Core calling it after generated repos are Git-initialized and after update mutation succeeds; `doti install` should install the hook from the command orchestration layer when the target is a Git repo.
- **Rationale:** The hook content and sentinel behavior already live in `Hx.Cycle.Core`; duplicating hook text in scaffold/update/Doti install code would drift. `Hx.Scaffold.Core` can reference `Hx.Cycle.Core` without creating a cycle under the current core-layer rules. `Hx.Doti.Core` should stay focused on assets/rendering; `Hx.Runner.Cli doti install` can compose `DotiInstaller.Install` with hook arming without forcing `Hx.Doti.Core` to depend on cycle enforcement.
- **Alternatives rejected:** Hand-write hook files in `Hx.Scaffold.Core` (duplicate policy); silently overwrite any existing pre-commit hook (violates spec safety); chain arbitrary hooks automatically (unproven semantics and hidden user-code execution); make hook presence a hard commit proof (hooks are bypassable local insurance).

- **Decision:** Refresh `tools/sentrux/sentrux.version.json` to `heurexai/sentrux` `v0.5.11` using GitHub release-provided asset digests, then make Sentrux verify/check/gate reporting preserve richer failure detail.
- **Rationale:** The user explicitly said the forked Sentrux has been updated and should replace the older vendored version. GitHub API verification found `v0.5.11`, published 2026-06-23, with release-provided digests for `sentrux-windows-x86_64.exe` (`ed387706d10fc2708507939d5390016b256cc4a725afcf9e209021cbab2bf88c`), `sentrux-linux-x86_64` (`6da9bede77654a54425c101d37d1bbb192a51c8cf7bd2823ad9c0da45ee34fae`), `sentrux-linux-aarch64` (`e59fafd687aaa980705cbbd20020b59f8647b9e9bed92d54d1e2ffb033e31fcb`), `sentrux-darwin-x86_64` (`034062226733c8f56660f59a4c73e10c66effaeafe598e90fbb37331ff101189`), and `sentrux-darwin-arm64` (`0389d2b84075bf02a5f53707f589e5a999b65b7b31acc47e622417d23745dad2`).
- **Alternatives rejected:** Keep `v0.5.10` until a later release (does not meet operator request); download latest at runtime without pinning (not deterministic); refresh `.sentrux` baselines with the tool (violates live-configuration preservation).

## Design

### 1. Cycle completion record and recovery evaluator

Likely files:

- `tools/Hx.Cycle.Core/CycleService.cs`
- `tools/Hx.Cycle.Core/CycleStateStore.cs`
- `tools/Hx.Cycle.Core/FreshnessEvaluator.cs`
- `tools/Hx.Cycle.Core/GitRefs.cs`
- new `tools/Hx.Cycle.Core/Completion/CycleCompletionIntent.cs`
- new `tools/Hx.Cycle.Core/Completion/CycleCompletionRecord.cs`
- new `tools/Hx.Cycle.Core/Completion/CycleCompletionRecovery.cs`
- `tools/Hx.Tooling.Contracts/CycleState.cs`
- tests in `test/Hx.Runner.Tests` or a new focused cycle test file

Approach:

- Persist a commit-completion intent before invoking `git commit`, atomically enough to survive process termination.
- Bind the intent to feature/stage, pre-commit `HEAD`, staged-tree identity, change-set identity, gate proof identity/digest, authorizing stage proof identities, commit message digest, and expected completed-record shape.
- After `git commit` succeeds, write a completed-cycle record carrying commit SHA, feature/stage, proof identities, staged-tree identity, change-set identity, and completion timestamp/run id.
- Run a shared recovery evaluator at the start of `status`, `check`, `stamp`, and `commit` so each command reaches the same completed/retryable/ambiguous result.
- Treat repeated `commit` after a proven completed commit as idempotent completed-cycle reporting, not as another `git commit`.
- Treat new edits after recovery as a new unstamped change set while preserving the prior completed-cycle verdict.

Architecture delta: no new project. New recovery types live in `Hx.Cycle.Core`; public JSON records live in `Hx.Tooling.Contracts` only if shared outside cycle core. No `rules/architecture.json` change is expected. `.sentrux/rules.toml` remains valid because all work stays in the existing core layer.

### 2. Proof provenance, staged-tree binding, and bypass classification

Likely files:

- `tools/Hx.Tooling.Contracts/GateProof.cs`
- `tools/Hx.Tooling.Contracts/PersistedGateProof.cs`
- `tools/Hx.Tooling.Contracts/AffectedTestProof.cs`
- `tools/Hx.Gate.Core/GateRunner.cs`
- `tools/Hx.Cycle.Core/GateProofStore.cs`
- `tools/Hx.Cycle.Core/GateProofValidator.cs`
- `tools/Hx.Cycle.Core/CommitScopeInspector.cs`
- `tools/Hx.Cycle.Core/HookInstaller.cs`
- `tools/Hx.Cycle.Core/PrecommitGuard.cs`
- tests in `test/Hx.Runner.Tests`, `test/Hx.Impact.Tests`, and affected proof tests

Approach:

- Add producer provenance and a canonical accepted-field proof digest to persisted gate proof.
- Add staged-tree identity and final trailer binding to `doti cycle commit`.
- Expand affected-test proof execution identity so `cycle commit` can reject stale build outputs, hand-edited proof files, and direct diagnostic command transcripts.
- Add hook-health reporting for missing/modified/installed pre-commit hook, without treating the hook as a security boundary.
- Detect external/bypass commits when `HEAD` advances without matching completion intent, proof digest, staged-tree identity, and Doti trailers.
- Keep direct `dotnet test`, direct impact planning, and direct hygiene commands diagnostic-only; only `gate run` writes commit-acceptable proof.

Architecture delta: no new project/layer. Contracts remain lowest layer; gate/cycle/impact stay core. No rule file change is expected unless implementation introduces new projects.

### 3. Canonical scaffold version identity and repo-aware version report

Likely files:

- `tools/Hx.Tooling.Contracts/VersionResult.cs`
- new `tools/Hx.Tooling.Contracts/ScaffoldVersionIdentity.cs`
- `tools/Hx.Version.Core/GitVersionTool.cs`
- new `src/Hx.Scaffold.Core/Versioning/ScaffoldVersionReporter.cs`
- `tools/Hx.Scaffold.Cli/Program.cs`
- `tools/Hx.Scaffold.Cli/ScaffoldCommands.cs`
- packaging/release manifest files under `packaging/`
- tests in `test/Hx.Scaffold.Tests` and CLI describe tests

Approach:

- Centralize normalized SemVer, release tag, source commit, build metadata, and release asset identity into one comparable identity model.
- Record target repo version stamps under `.doti/` on `new`, `install`, and successful `update`.
- Expose running `hx` version through `hx --version`, `describe --json`, update dry-run/report, and a repo-aware version report with `--repo <path>`.
- Version report reuses the managed-asset hash classifier but remains strictly read-only: no worktree, cache, downloads, pruning, or file writes.
- Treat newer target repo stamps as no-direct-mutation cases for older updaters.

Architecture delta: no new project. `Hx.Scaffold.Core` may add a reference to `Hx.Version.Core`; this is an allowed within-core dependency and should be reflected in code comments if needed. No Sentrux layer change is expected.

### 4. Managed asset manifest and semantic hash profiles

Likely files:

- new `tools/Hx.Doti.Core/ManagedAssets/ManagedAssetManifest.cs`
- new `tools/Hx.Doti.Core/ManagedAssets/ManagedAssetScanner.cs`
- new `tools/Hx.Doti.Core/ManagedAssets/CanonicalContentHasher.cs`
- `tools/Hx.Doti.Core/DotiInstaller.cs`
- `tools/Hx.Doti.Core/DotiRenderer.cs`
- source manifest under `doti/core/managed-assets.json` or equivalent
- target metadata under `.doti/managed-assets.json` and `.doti/scaffold-version.json`
- tests in `test/Hx.Doti.Tests`

Approach:

- Define managed path entries with ownership category, canonical identity policy, update conflict policy, generated/replaced metadata classification, live-config exclusion, and legacy possible-orphan handling.
- Record canonical managed-asset hashes after `new`, `install`, and successful `update`.
- Recompute and classify current target paths before update or repo-aware version reporting.
- Distinguish modified workflow templates, modified doti skills/root entrypoints, missing managed files, unmodified managed files, generated/replaced metadata, live-preserved files, and possible-orphan legacy files.
- Use named hash profiles in recorded metadata, including profile version, source format, library/version, and canonicalization result identity.
- Fail closed for malformed YAML/JSON, duplicate YAML/JSON keys where the profile cannot represent them deterministically, unsupported JSONC/trailing commas unless named, unsupported encodings/BOMs, unsupported numeric values, and unsupported hash schema versions.

Architecture delta: `Hx.Doti.Core` adds YamlDotNet dependency. This remains in the core layer. No new Sentrux layer is needed.

### 5. `hx update` orchestration, cache, handoff, worktree backup, and legacy mode

Likely files:

- new `src/Hx.Scaffold.Core/Update/ScaffoldUpdateService.cs`
- new `src/Hx.Scaffold.Core/Update/ReleaseClient.cs`
- new `src/Hx.Scaffold.Core/Update/UpdateCache.cs`
- new `src/Hx.Scaffold.Core/Update/ReleaseAssetVerifier.cs`
- new `src/Hx.Scaffold.Core/Update/OlderUpdaterHandoff.cs`
- new `src/Hx.Scaffold.Core/Update/WorktreeBackup.cs`
- new `src/Hx.Scaffold.Core/Update/ManagedAssetReconciler.cs`
- `tools/Hx.Scaffold.Cli/Program.cs`
- `tools/Hx.Scaffold.Cli/ScaffoldCommands.cs`
- `errorcodes/registry.json`
- `packaging/PUBLISHING.md` and release manifest generation inputs if checksum metadata is missing
- tests in `test/Hx.Scaffold.Tests` using temporary Git repos

Approach:

- Add `hx update` with `--repo <path>`, `--dry-run`, `--force`, `--noworktree`, JSON/plain/help-mode controls, and target-root containment.
- Default target repo to current working directory.
- Resolve latest non-prerelease GitHub release from `heurexai/speckit-doti`; select host RID asset; verify release checksum/manifest; cache under a speckit-doti-owned temp root.
- Reuse verified latest cache entries; prune older cached versions only after newer asset verification succeeds; never prune the payload currently executing a delegated updater.
- Delegate to a verified temporary `hx` before mutation when the parent is older than target, the selected compatible release is newer than parent, or executable locking can apply. Pass `--repo <target>` and preserve `--dry-run`, `--force`, `--noworktree`, JSON/plain/help-mode intent.
- Create backup Git worktree from committed `HEAD` before mutating the original checkout unless `--noworktree` is supplied. Report that the backup is not the mutation target and does not include dirty edits.
- Refuse dirty managed planned-write collisions, including staged, unstaged, untracked, and ignored paths. Keep any dirty-path override separate from `--force`.
- Preserve live configuration and baselines exactly, including Sentrux baselines and repo-local runtime gate state.
- For legacy pre-versioned targets, replace/create only current manifest-managed paths, preserve live config, leave unknown old files untouched, report possible-orphan files, write new version/hash metadata after success, and include LLM follow-up sweep instructions.
- Keep a versioned repo with broken hash metadata fail-closed by default.

Architecture delta: no new project expected. `Hx.Scaffold.Core` may reference `Hx.Version.Core`, `Hx.Doti.Core`, and existing runner/tool helpers. If cache/release clients need an abstraction for network/process execution, define interfaces in `Hx.Scaffold.Core` and inject from CLI wiring without moving logic into `Hx.Scaffold.Cli`. `.sentrux/rules.toml` remains valid if no new project is added. If implementation introduces `Hx.Update.Core`, add it to `scaffold-dotnet.slnx`, `.sentrux/rules.toml` core layer paths, and document that in `rules/architecture.json`.

### 6. Trusted prerequisite policy, preflight, and Windows winget remediation

Likely files:

- new `src/Hx.Scaffold.Core/Prerequisites/PrerequisiteManifest.cs`
- new `src/Hx.Scaffold.Core/Prerequisites/PrerequisitePolicyLoader.cs`
- new `src/Hx.Scaffold.Core/Prerequisites/PrerequisiteProbeRunner.cs`
- new `src/Hx.Scaffold.Core/Prerequisites/PrerequisitePreflight.cs`
- new `src/Hx.Scaffold.Core/Prerequisites/PrerequisiteInstallPlanner.cs`
- new `src/Hx.Scaffold.Core/Prerequisites/WingetPrerequisiteInstaller.cs`
- optional `tools/Hx.Tooling.Contracts/PrerequisiteResult.cs`
- source manifest under `doti/core/prerequisites.json`
- target metadata under `.doti/prerequisites.json` so generated repos carry the release-defined policy they were created with
- `tools/Hx.Scaffold.Cli/ScaffoldCommandFactory.cs`
- `tools/Hx.Scaffold.Cli/ScaffoldCommands.cs`
- tests in `test/Hx.Scaffold.Tests`

Approach:

- Define a release-managed prerequisite manifest with schema version, manifest identity/hash, command applicability, hard/advisory classification, probes, version policy, directory checks, trusted instruction text, and Windows winget mappings.
- Treat compatible .NET SDK and Git as initial hard requirements. The initial Windows winget mappings are `Microsoft.DotNet.SDK.10` and `Git.Git` from source `winget`; minimum supported versions are manifest data and must be tested by probe output, not by trusting installer output.
- Load the manifest through the same managed-asset trust boundary as other Doti assets. A repo-local extension may add project-specific probes and text-only instructions, but cannot weaken release-defined hard requirements, replace package/source metadata, or add executable URLs accepted by `hx`.
- Run preflight before `new` mutates output, before `update` creates a backup worktree/download-prunes/reconciles files, and inside repo-aware `version` as read-only health reporting. Directory checks must run before side effects and report no-coder-friendly diagnostics.
- Add `hx prereq check --for <new|update|version|generated-validation> [--repo <path>] --json` and `hx prereq install --for <new|update> [--repo <path>] [--confirm-plan <digest>] --json`. The check command never installs. The install command is Windows-only, emits the exact install plan, requires explicit operator approval tied to a plan digest, executes only trusted winget package/source actions, reruns probes, and continues only when the hard requirement verifies.
- Non-Windows automatic install requests return platform-unsupported diagnostics and trusted manual instructions without invoking a package manager. Windows without winget, blocked winget, failed winget, cancelled prompts, changed plan digest, or missing package mapping all fail before scaffold/update mutation.

Architecture delta: no new project expected. Prerequisite policy/probe/install planning lives in `Hx.Scaffold.Core`; `Hx.Scaffold.Cli` only parses options, injects process/console adapters, and renders `CliResult`. If generated-repo validation needs shared records, contracts go in `Hx.Tooling.Contracts`. No `rules/architecture.json` or `.sentrux/rules.toml` layer change is expected unless implementation introduces a new core project.

### 7. Installed generated surfaces and docs

Likely files:

- `doti/core/skills.json`
- `doti/core/templates/agent-context-template.md`
- rendered `.doti/agent-context.md`
- rendered `.agents/skills/doti-*`
- rendered `.claude/skills/doti-*`
- root `AGENTS.md` / `CLAUDE.md` if generated entrypoints need additive command availability notes
- README/contributor docs only if they describe implemented behavior after implementation

Approach:

- Update source skill/context definitions only; render installed skills through `doti render-skills`.
- Keep docs honest: implemented update/version/hash/cycle behavior may be described as current working-tree behavior after focused tests pass; release publication, broader proof-provenance, hook-health, and clean-checkout merge proof remain advisory until separately implemented and proven.
- Do not update Sentrux baselines or live target config in generated repos.

Architecture delta: no source-layer change.

### 8. Auto-armed Doti hook

Likely files:

- `tools/Hx.Cycle.Core/HookInstaller.cs`
- new `tools/Hx.Cycle.Core/HookHealth.cs` or equivalent hook classifier
- `src/Hx.Scaffold.Core/Hx.Scaffold.Core.csproj`
- `src/Hx.Scaffold.Core/ScaffoldNewRunner.cs`
- `src/Hx.Scaffold.Core/Update/ScaffoldUpdateService*.cs`
- `tools/Hx.Runner.Cli/RunnerCommands.Doti.cs`
- `tools/Hx.Runner.Cli/RunnerCommands.DotiCycle.cs`
- tests in `test/Hx.Runner.Tests` and `test/Hx.Scaffold.Tests`

Approach:

- Keep hook script generation and sentinel policy in `Hx.Cycle.Core`.
- Add a deterministic hook classifier that resolves the Git hooks path, computes the expected Doti hook identity, and classifies the current pre-commit hook as absent, expected, older-Doti, modified-Doti, non-Doti, or not-a-Git-repo.
- Make automatic writes fail before managed update mutation when a non-Doti or unproven modified hook exists.
- Wire `hx new` to arm the hook only after the target repo has been Git-initialized by first smoke, then record hook path/status in the scaffold proof or emitted events.
- Wire `hx update` dry-run to report planned hook work without writing, and successful update/no-op update to install or refresh the hook after managed reconciliation succeeds.
- Wire `doti install` command orchestration to install or refresh the hook when the target is a Git repo; `Hx.Doti.Core.DotiInstaller` remains focused on file assets and metadata.

Architecture delta: `Hx.Scaffold.Core` adds a within-core reference to `Hx.Cycle.Core` unless implementation factors hook installation into a lower shared core. No `.sentrux/rules.toml` or `rules/architecture.json` change is expected because both projects are already in the core layer and Sentrux enforces no cycles. If a cycle appears, move hook classification/installer to a shared lower core before continuing.

### 9. Vendored Sentrux `v0.5.11` refresh and richer failure reporting

Likely files:

- `tools/sentrux/sentrux.version.json`
- `tools/Hx.Sentrux.Core/SentruxManifestValidator*.cs`
- `tools/Hx.Sentrux.Core/SentruxOutputParser.cs`
- `tools/Hx.Sentrux.Core/SentruxChecker*.cs`
- `tools/Hx.Gate.Core/GateRunner.cs`
- `tools/Hx.Tooling.Contracts/SentruxCheckResult.cs`
- tests in `test/Hx.Runner.Tests` and `test/Hx.Scaffold.Tests`

Approach:

- Update the Sentrux manifest to `v0.5.11` with release URLs and byte-exact executable digests for every supported RID in the GitHub release.
- Keep grammar assets and any grammar manifest entries explicit; if `v0.5.11` changes grammar requirements, fail closed with a clear verify diagnostic rather than creating a baseline or weakening the check.
- Ensure `tools fetch`, shared tool store, `sentrux verify`, `sentrux check`, and `gate run` consume the refreshed manifest.
- Preserve richer Sentrux failure output by parsing structured output when available and keeping raw-but-sanitized details as evidence when no structured field exists.
- Do not refresh, rewrite, or normalize `.sentrux/baseline.json`, `.sentrux/rules.toml`, or target-owned live Sentrux configuration as part of the executable refresh.

Architecture delta: no project/layer change. Work stays in existing Sentrux/Gate/Contracts cores. No Sentrux baseline update is allowed.

## CLI surface & error contract

New operation: `hx update`.

- **Options:** `--repo <path>`, `--dry-run`, `--force`, `--noworktree`, `--json`, shared help-mode controls.
- **Exit classes:** Success when current/update/dry-run succeeds; Usage for invalid arguments; Validation for unsupported target state, dirty planned-write collisions, modified managed assets without `--force`, no-Git-recovery, unsupported RID, too-new target without compatible handoff, and network/latest failures in latest mode; Integrity for release/cache/executable/hash/proof mismatches; Internal for unexpected download/extract/process failures.
- **Planned error-code registry additions:** validation update target not repository; validation update target no Git recovery; validation update dirty managed path; validation update modified templates; validation update modified skills; validation update hash metadata invalid; validation update too-new target; validation update handoff failed; validation update unsupported RID; validation update network latest unavailable; integrity update asset hash mismatch; integrity update executable hash mismatch; integrity update cache tampered; integrity managed asset hash unsupported.
- **`describe` entry:** add `update` with all options and diagnostics in `Hx.Scaffold.Cli describe --json`.
- **Envelope:** every result emits `CliResult` with target repo, running/delegated version, latest version, cache action, worktree/no-worktree decision, file plan or changed paths, preserved live paths, possible-orphan legacy files, diagnostics, and follow-up validation commands.

New operation group: `hx prereq`.

- **Options:** `check --for <new|update|version|generated-validation> [--repo <path>] --json`; `install --for <new|update> [--repo <path>] [--confirm-plan <digest>] --json`; shared help-mode controls.
- **Exit classes:** Success when all hard prerequisites pass or an approved install completes and probes verify; Usage for invalid command/target combinations; Validation for missing/unsupported prerequisites, unsupported platform install, winget unavailable/blocked, install not approved, changed plan digest, missing trusted package mapping, directory failures, or failed post-install probe; Integrity for untrusted/tampered prerequisite manifest or package/source metadata mismatch; Internal for unexpected process or I/O failures.
- **Planned error-code registry additions:** validation prerequisite missing; validation prerequisite unsupported version; validation prerequisite directory unavailable; validation prerequisite install unsupported platform; validation prerequisite install not approved; validation prerequisite winget unavailable; validation prerequisite winget failed; validation prerequisite winget mapping missing; integrity prerequisite manifest untrusted; integrity prerequisite package source mismatch.
- **`describe` entry:** add `prereq` group and expose automatic preflight behavior on `new`, `update`, and repo-aware `version`, including whether failures happen before side effects.
- **Envelope:** includes manifest identity, command requested, per-prerequisite probe result, directory readiness, trusted next actions, install-plan digest when present, operator permission provenance, winget execution result when attempted, and post-install probe result. It must not include secrets, private feeds, or full environment dumps.

New operation: repo-aware version report.

- **Options:** `--repo <path>`, `--json`, shared help-mode controls.
- **Exit classes:** Success for running-only or repo-aware report; Validation for invalid path, no version stamp, missing/corrupt hash metadata, missing managed files, unsupported hash schema.
- **Planned error-code registry additions:** validation version target invalid; validation version stamp missing; validation version hash metadata invalid.
- **`describe` entry:** add repo-aware version report command. Keep scalar `hx --version` terse.
- **Envelope:** reports running `hx` identity, target repo identity when resolved, and managed-asset modification categories with repo-relative paths and hash metadata.

Changed operation: `doti cycle status/check/stamp/commit`.

- **Exit classes:** existing Success/Validation behavior remains; ambiguous recovery is Validation; malformed/tampered proof identity is Integrity when detected before Git mutation.
- **Planned error-code registry additions:** validation cycle completed; validation cycle ambiguous recovery; validation cycle external bypass commit; integrity gate proof digest mismatch; integrity affected test artifact mismatch; validation hook missing or modified.
- **`describe` entry:** update command summaries/diagnostics to distinguish diagnostic-only commands from proof-minting commands.
- **Envelope:** completed-cycle, recoverable-completion, ambiguous-recovery, hook-health, and external/bypass verdicts appear in JSON output.

Changed operation: `hx new`, `hx update`, and `doti install`.

- **Exit classes:** Success includes hook absent/expected/older-Doti refresh success; Validation for non-Doti or unproven modified hook conflicts; Integrity for a hook classified as Doti-owned but hash-inconsistent with all known supported Doti hook identities.
- **Planned error-code registry additions:** validation hook conflict non-Doti; validation hook modified; validation hook not Git repo when required; integrity hook identity mismatch.
- **`describe` entry:** document automatic hook arming on `new`/`update`/`doti install`, and dry-run hook-health reporting on update/version/status.
- **Envelope:** hook path, hook health verdict, expected hook identity, and planned/actual hook action appear in JSON without including raw non-Doti hook contents.

Changed operation: `sentrux verify/check` and `gate run`.

- **Exit classes:** existing success/validation/integrity behavior remains; manifest/hash mismatch is Integrity; Sentrux rule/regression failure is Validation/Blocked according to existing gate semantics.
- **Planned error-code registry additions:** integrity sentrux manifest stale; integrity sentrux executable hash mismatch; validation sentrux failure details unavailable when the updated fork emits an unsupported shape.
- **Envelope:** include Sentrux release tag/source remote/executable identity and richer file/rule/failure details when available.

## Command Availability

| Area | Command | Status |
| --- | --- | --- |
| Restore | `dotnet restore .\scaffold-dotnet.slnx` | implemented |
| Build | `dotnet build .\scaffold-dotnet.slnx -c Release --no-restore /m:1` | implemented; local sandbox required escalation once to write build `obj`, then `--no-build` runner commands worked |
| Test | `dotnet test .\scaffold-dotnet.slnx -c Release --no-build /m:1` | implemented |
| Runner describe | `dotnet run --project tools/Hx.Runner.Cli -c Debug --no-build -- describe --json` | implemented and verified during planning |
| Cycle stamp/status/check/commit | `dotnet run --project tools/Hx.Runner.Cli -- doti cycle ... --json` | implemented; completed-cycle persistence/recovery and post-commit write-failure reporting implemented in current working tree, pending final full-gate proof |
| Gate run | `dotnet run --project tools/Hx.Runner.Cli -- gate run --repo . --profile normal --json` | implemented; broader persisted proof provenance/digest/artifact identity remains advisory beyond the current commit-intent/trailer digest binding |
| Impact plan | `dotnet run --project tools/Hx.Impact.Cli -- plan --repo . --base <ref> --head <ref> --json` | implemented; proof identity changes planned if needed |
| Doti render/install | `dotnet run --project tools/Hx.Runner.Cli -- doti render-skills/install ... --json` | implemented; managed manifest/hash metadata implemented in current working tree |
| Version calculate/bump | `dotnet run --project tools/Hx.Runner.Cli -- version calculate/bump ... --json` | implemented; canonical scaffold product identity implemented in current working tree |
| Scaffold new | `dotnet run --project tools/Hx.Scaffold.Cli -- new ... --json` | implemented; version/hash metadata recording implemented in current working tree |
| Scaffold update | `dotnet run --project tools/Hx.Scaffold.Cli -- update ... --json` / standalone `hx update` | implemented in current working tree with dry-run, force, noworktree, cache, handoff, worktree, live-preservation, and report metadata; GitHub latest behavior depends on published release assets/checksums |
| Repo-aware version report | `dotnet run --project tools/Hx.Scaffold.Cli -- version ... --repo <path> --json` or equivalent | implemented in current working tree |
| GitHub latest-release client | update-only network path | implemented in current working tree; network-enabled and not part of offline gate |
| Prerequisite preflight | `dotnet run --project tools/Hx.Scaffold.Cli -- prereq check ... --json` plus automatic preflight in `new`/`update`/repo-aware `version` | NEW (advisory until built); manifest-driven and fail-before-side-effects |
| Windows prerequisite install | `dotnet run --project tools/Hx.Scaffold.Cli -- prereq install ... --json` | NEW (advisory until built); Windows-only, winget-only, explicit operator approval required |
| Auto hook arming | `hx new`, `hx update`, and `doti install` installing/refreshing the Doti pre-commit hook | NEW (advisory until built); existing `doti install-hooks` command-backed installer/guard already exists |
| Sentrux fork refresh | `tools/sentrux/sentrux.version.json` -> `heurexai/sentrux` `v0.5.11`; `sentrux verify/check`, `gate run` | NEW (advisory until manifest/tool and parser updates are built); current command availability still reports `v0.5.10` |
| Release | `/doti-release`, tag, publish assets | intentionally not run in this cycle; user review comes first |

## Constitution Check (after design)

PASS after design:

- The design keeps update/recovery/hash behavior in .NET core libraries and CLI projects thin.
- It encodes deterministic ownership through manifests, metadata, hashes, and proof records.
- It preserves the template boundary by rendering skills from source and treating generated files as generated.
- It keeps network and release-cache effects outside offline gates and outside the repo.
- It keeps prerequisite install effects outside ordinary command retries and binds them to a trusted release manifest plus explicit operator approval.
- It uses the existing hook installer/guard as local insurance while keeping command-backed Doti cycle checks authoritative.
- It treats Sentrux executable refresh as pinned managed tooling and preserves target-owned baseline/config state.
- It does not add shell runners or trust local hooks as security boundaries.
- Earlier non-release stop guidance is superseded by the active thread goal to complete the Doti cycle and release; release work still must use `/doti-release`, sanctioned version bump/tag, CI/release proof, and GitHub release publication gates rather than manual tags or direct pushes.

## Complexity Tracking

No justified constitution violations.

## Risks

- **Scope risk:** The feature is broad. Implement in testable vertical slices and keep planned-but-absent surfaces advisory until their slice is command-backed.
- **Compatibility risk:** Existing target repos may have partial/legacy Doti assets. The manifest classifier must be conservative and report unknowns without deleting them.
- **Hash false-positive risk:** YAML/JSON canonicalization must use parser-backed semantics and explicit fail-closed cases; byte-exact must remain for binary/integrity assets.
- **Recovery false-positive risk:** Completed-cycle recovery must prove the current `HEAD` is the sanctioned commit before writing completed state; otherwise it must report ambiguous recovery.
- **Handoff recursion risk:** Temporary updater delegation must carry recursion metadata and only re-delegate to a strictly newer compatible verified release.
- **Worktree recovery risk:** Reports must not imply dirty edits are preserved by the backup worktree.
- **Gate runtime risk:** Full normal/release gates may be slow or sensitive to local build servers. Use command-backed proof, isolate build output if needed, and report any gate that cannot be completed.
- **Release boundary risk:** Do not run `version bump`, create tags, publish assets, or invoke `/doti-release` until the operator has reviewed the final deliverable.
- **Prerequisite trust risk:** A mutable repo/cache manifest could turn remediation into arbitrary code execution. Treat release-defined package/source metadata as the only executable install source, hash/sign the manifest as a managed asset, and reject repo-local executable URL or winget overrides.
- **Install side-effect risk:** Winget may require elevation, UI, policy approval, or PATH/session refresh. The installer must report this honestly, rerun probes after completion, and refuse scaffold/update mutation unless the new process environment can verify the prerequisite.
- **Hook conflict risk:** Existing user hooks may encode important repo behavior. Automatic arming must fail hard on non-Doti or unproven modified hooks instead of chaining or overwriting them.
- **Sentrux output-shape risk:** The updated fork may emit richer details in a different structure than the current parser expects. Parser updates must preserve existing pass/fail semantics and keep raw sanitized evidence available when structured extraction is partial.
