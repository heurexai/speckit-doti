# 022 — Doti repo version lifecycle — Plan

## Summary

Add four `hx doti` subcommands — `check-version`, `scan`, `update`, `update-all` — plus a fix to `hx version --repo`'s `targetRelation`. The design's central decision is **reuse over reinvention**: the repo already has the whitespace-insensitive customization detection (`CanonicalContentHasher` `normalized-text/v2` + `ManagedAssetScanner`), the customization-aware reconcile (`DotiInstaller` preserves operator-modified assets and writes `.new` sidecars), and the `.doti/payload.json` read/write. The new work is therefore: (a) **single-source** a version-relation calculator + a payload reader and point every surface (including `hx version --repo`) at them; (b) a **read-only repo discovery** type; (c) a thin **updater** that wraps the existing installer with before→after reporting; (d) one genuinely new primitive — a **git worktree session** that isolates the mutation, yields a reviewable preview (`--dry-run`), and applies back on acceptance; and (e) four thin CLI command surfaces.

## Technical Context

- **Stack:** .NET 10, the `Hx.*` toolkit. Contracts (order 0) → Core (order 1) → Cli (order 2) per `.sentrux/rules.toml`. `CliResult` envelope (`Hx.Cli.Kernel`), error codes from `errorcodes/registry.json` (frozen by `shipped.json`).
- **Reused, verified-existing building blocks:**
  - `Hx.Doti.Core/ManagedAssets/CanonicalContentHasher.HashFile(path, profile)` — `normalized-text/v2` collapses whitespace/EOL before SHA-256 (the whitespace-insensitive hash FR-010 needs).
  - `Hx.Doti.Core/ManagedAssets/ManagedAssetScanner.Scan()` + `ManagedAssetManifestStore` — the managed-asset baseline + drift categories.
  - `Hx.Doti.Core/DotiInstaller.Install(..., force, ...)` — already preserves operator-modified assets (`.new` sidecars), never resurrects operator-deleted assets without `force`, preserves operator-authored content, and stamps `.doti/payload.json` (`StampRepoPayload`/`ReadRepoPayloadVersion`, currently private).
  - `Hx.Runner.Core/Process/ProcessRunner.Run(ToolCommand)` — the process seam for invoking `git`.
  - `Hx.Tooling.Contracts/DistributionContracts.cs` `RepoPayloadStamp` (`PayloadVersion`/`ToolVersion`).
  - `Hx.Version.Core` `GitVersionTool.CompareVersions()` — SemVer ordering.
- **Verified defect (FR-003):** `src/Hx.Scaffold.Core/Versioning/ScaffoldVersionReport.cs` `ScaffoldVersionReporter.Report()` computes `TargetRelation` from `.doti/scaffold-version.json` (`ReadStamp`), which is empty for a Doti-*adopted* repo → it reports `newer` for a current repo. It already depends on `Hx.Doti.Core` (uses `ManagedAssetScanner`), so it can read `payload.json` + the new relation calculator with no new edge.
- No `[NEEDS CLARIFICATION]` remain — the two operator decisions were resolved at `/02-clarify` (repo-reconcile-only; worktree + git-required).

## Constitution Check (gate)

**§1 inherited invariants** (gate/ArchUnit/Sentrux/GitVersion-enforced — not re-litigated):
- **Deterministic Ownership** — PASS. Every command emits a deterministic `CliResult` proof; relation + customization are computed deterministically (canonical hash, SemVer compare).
- **Bootstrap Honesty** — PASS. All four commands are advisory/planned until `/07`; the spec + this plan mark them so.
- **Template Boundary** — PASS (n/a). Runtime `hx doti` commands; no `scaffold/templates/**` change.
- **Public Hygiene** — PASS. `scan`/`update-all` echo only operator-supplied roots/paths; worktrees live in temp; no secrets.
- **Cross-Platform** — PASS. `git` + `ProcessRunner` are cross-platform; win-x64 is the active gate target.
- **Engineering Discipline** — PASS. RCA'd the existing machinery and reuse it; modular single-responsibility Core types.
- **Operator Decisions** — PASS. Q1/Q2 resolved via the protocol and recorded in the spec's Clarifications.
- **Codified Cycle / Channel Independence** — PASS. This is a cycle feature; commands drive off the installed payload, channel-agnostic; logic in `*.Core`, CLI thin.

**§2 project declarations** (fetched fresh via `hx doti constitution --repo .`): the design uses the declared .NET stack, the `*.Core`/`*.Cli` split, the `CliResult` contract, and deterministic proofs — consistent with §2; no declaration bent. **Verdict: PASS (before design).**

## Research (resolve unknowns)

**1. Single-sourcing the version relation (FR-020).**
- **Decision:** one `DotiVersionRelationCalculator` in `Hx.Doti.Core` computes a `DotiVersionRelation` enum (`Current`/`Outdated`/`Ahead`/`Unknown`) from `(repoVersion, toolVersion)` via `GitVersionTool.CompareVersions`. The four new commands use the enum's labels; `hx version --repo` keeps its existing `targetRelation` string contract (`unknown`/`behind`/`equal`/`newer`) but maps it from the same calculator and the same `payload.json` source.
- **Rationale:** one comparison primitive (FR-020) with per-surface labels avoids breaking the existing `version --repo` JSON contract while fixing its source.
- **Alternatives rejected:** changing `version --repo`'s label vocabulary (breaks existing consumers); a second relation computation in the new commands (the divergence FR-020 forbids).

**2. Customization detection (FR-010/011/012).**
- **Decision:** reuse `ManagedAssetScanner` + `CanonicalContentHasher` (`normalized-text/v2`) and the existing `DotiInstaller` reconcile, which already preserves operator-modified assets (`.new` sidecar) and honors `force`. `update` surfaces these as the customization report (the `Preserved`/`.new` set from `DotiInstallResult`) and the before→after version.
- **Rationale:** the spec's "detect customization via whitespace-insensitive hashing, preserve + report, `--force` to override" is already the installer's behavior — building a second scheme would drift (two hashes, two reconcile policies).
- **Alternatives rejected:** a new content-diff/hashing for `update` (duplicate of `CanonicalContentHasher`); reading raw bytes (loses the whitespace-insensitivity the operator asked for).

**3. Worktree-isolated mutation + apply-back (FR-013/014).**
- **Decision:** new `GitWorktree` primitive in `Hx.Runner.Core` (git is general infra): `git worktree add --detach <temp> HEAD` → run the reconcile in `<temp>` → capture the change set (`git -C <temp> add -A` then `git -C <temp> diff --cached HEAD` → a patch + file summary) as the **preview** → on acceptance apply it to the repo working tree (`git -C <repo> apply`) → `git worktree remove`. `--dry-run` returns the preview and leaves the repo untouched. Absent git (`git --version` fails or no `.git`) → fail hard.
- **Rationale:** a git worktree gives real isolation sharing the object store (efficient), matches the operator's explicit "git worktree" + "git required", and the apply-back is a git-native patch. Landing the change as **uncommitted working-tree edits** (not a commit) keeps it consistent with how `install` already mutates a repo and avoids triggering the target repo's pre-commit hook or fighting its gitignored `.doti` runtime state.
- **Alternatives rejected:** reconcile in-place then diff (no isolation — the spec's whole point); commit-in-the-target-repo merge (trips the target's Doti pre-commit hook, and `.doti` runtime files are gitignored, making a clean commit fragile); a plain temp-dir copy instead of a worktree (the operator specifically required git/worktree, and a copy loses git's tracked/ignored awareness).

**4. Worktree primitive placement (cross-core edge).**
- **Decision:** `GitWorktree` in `Hx.Runner.Core`; `Hx.Doti.Core` gains a project reference to `Hx.Runner.Core`.
- **Rationale:** `.sentrux/rules.toml:62-64` explicitly sanctions within-core dependencies (`Hx.Sentrux.Core → Hx.Runner.Core`); `Hx.Runner.Core` does not depend on `Hx.Doti.Core`, so the edge is acyclic (`max_cycles=0` enforces it). No rule change required.
- **Alternatives rejected:** duplicating `ProcessRunner`/git invocation inside `Hx.Doti.Core` (a second process seam to maintain); orchestrating the worktree in the CLI handler (logic in `*.Cli` — violates `cliDelegation`).

## Design

**Selection rule applied:** the simplest correct, modular design that maximally reuses the existing reconcile/hash/payload machinery; one named `*.Core` type per behavior; CLI stays parse→delegate→render.

### Contracts — `Hx.Tooling.Contracts` (order 0)
- `enum DotiVersionRelation { Current, Outdated, Ahead, Unknown }`
- `record DotiRepoVersion(string? PayloadVersion, string ToolVersion, DotiVersionRelation Relation, string? UnknownReason)`
- `record DotiScanEntry(string RepoPath, DotiRepoVersion Version)` and `record DotiScanResult(IReadOnlyList<DotiScanEntry> Repos)`
- `record DotiAssetOutcome(string Path, string Disposition)` (`updated`/`kept-customized`/`force-updated`/`preserved-operator-owned`)
- `record DotiUpdateOutcome(string RepoPath, string? BeforeVersion, string? AfterVersion, StageOutcome Outcome, IReadOnlyList<DotiAssetOutcome> Assets, string? Reason)`
- `record DotiUpdateAllSummary(IReadOnlyList<DotiUpdateOutcome> Repos, int Updated, int AlreadyCurrent, int CustomizedSkipped, int Failed)`

### Core — `Hx.Doti.Core` (order 1)
- `RepoPayloadStore` — read/write `.doti/payload.json` (extracted from `DotiInstaller.ReadRepoPayloadVersion`/`StampRepoPayload`, which call it instead of duplicating). **Single source** of the payload read (FR-001/020).
- `DotiVersionRelationCalculator` — `(repoVersion, toolVersion) → DotiVersionRelation`. **Single source** of the relation (FR-002/003/020).
- `DotiVersionInspector` — for one repo: classify `not-a-doti-repo` (no `.doti`) vs `version-unknown` (`.doti` but no/parse-failed `payload.json`) vs a `DotiRepoVersion` (FR-001/004), via `RepoPayloadStore` + `DotiVersionRelationCalculator`.
- `DotiRepoScanner` — discover `.doti/payload.json`-bearing repos under a root: recursive, skips `.git`/vendored internals + does not descend into a discovered repo, error-tolerant (unreadable/malformed → `Unknown` + reason), read-only (FR-005/006/007).
- `DotiUpdater` — update one repo path *(a clean working tree — the worktree)*: precondition is-doti (else fail), capture before-version, run `DotiInstaller.Install(force)`, capture after-version, project `DotiInstallResult` (installed/preserved/`.new`) into `DotiUpdateOutcome` asset dispositions (FR-008/009/010/011/015).
- `DotiWorktreeUpdate` — orchestrate `GitWorktree` + `DotiUpdater`: create worktree → update inside it → capture preview → `--dry-run` returns preview / else apply back; git-required (FR-013/014).
- `DotiBatchUpdater` — `DotiRepoScanner` → `DotiWorktreeUpdate` per repo, fail-soft, aggregate `DotiUpdateAllSummary` (FR-016/017/018).

### Core — `Hx.Runner.Core` (order 1)
- `GitWorktree` — `Create(repoRoot) → WorktreeHandle`, `CaptureChanges(handle) → WorktreeChangeSet`, `ApplyBack(repoRoot, changeSet)`, `Remove(handle)`, `EnsureGitAvailable(repoRoot)` (fail hard if absent). Uses `ProcessRunner`.

### Core — `src/Hx.Scaffold.Core`
- `ScaffoldVersionReporter.Report()` (FR-003): read the target version from `.doti/payload.json` via `RepoPayloadStore` first, fall back to `.doti/scaffold-version.json` only when payload is absent; compute `TargetRelation` through `DotiVersionRelationCalculator` (mapped to the existing `unknown`/`behind`/`equal`/`newer` labels). No new edge (it already references `Hx.Doti.Core`).

### Cli — `Hx.Runner.Cli` (order 2) — wiring only
- `RunnerCommandFactory.Doti.cs`: `AddDotiCheckVersion`, `AddDotiScan`, `AddDotiUpdate` (`--force`,`--dry-run`), `AddDotiUpdateAll` (`--force`,`--dry-run`).
- `RunnerCommands.Doti.CheckVersion.cs` → `DotiVersionInspector`; `.Scan.cs` → `DotiRepoScanner`; `.Update.cs` → `DotiWorktreeUpdate`; `.UpdateAll.cs` → `DotiBatchUpdater`. Each: parse → delegate → `CliResults.FromStage/Ok/Fail`.

### Architecture delta (encoded, not just described)
- **New within-core edge** `Hx.Doti.Core → Hx.Runner.Core` (project reference). **No `.sentrux/rules.toml` change** — within-core deps are sanctioned (precedent `Hx.Sentrux.Core → Hx.Runner.Core`, lines 62-64); acyclic, held by `max_cycles=0` (the reverse `Hx.Runner.Core → Hx.Doti.Core` must never appear).
- **No new ArchUnit family.** New roles (`*Scanner`/`*Updater`/`*Calculator`/`*Inspector`/`*Store`) live in `*.Core`, satisfying `cliSurfaceConfinement`; the four `RunnerCommands` delegate, satisfying `cliDelegation` — `rules/architecture.json` unchanged.
- **Refactor (no behavior change):** `DotiInstaller`'s private payload read/stamp move to `RepoPayloadStore`; existing `DotiReconciliationTests` must stay green.
- **Docs are a required, gate-enforced deliverable (not optional polish):** `README.md` (the `hx doti` command map / the status boxes), `.doti/agent-context.md` (command availability), and `CHANGELOG.md` MUST document the four new commands + the `targetRelation` fix, describing only implemented behavior. `/04-tasks` carries an explicit, ordered docs task for each, and the `/08-drift-review` code↔docs axis blocks the cycle if any drifts from the shipped commands — so the README update cannot be skipped. `CONTRIBUTING.md` also gains the `feature→dev` (squash) / `dev→main` (merge) branch-flow note.

## CLI surface & error contract

| Command | Delegates to (Core) | Exit class(es) | Error codes (new → `errorcodes/registry.json`) |
| --- | --- | --- | --- |
| `hx doti check-version --repo <p> --json` | `DotiVersionInspector` | Success / Validation | `doti-not-a-repo` (validation), `doti-version-unknown` (validation, soft) |
| `hx doti scan --root <d> --json` | `DotiRepoScanner` | Success (even empty) | — (per-repo `unknown` carried in `data`, not a process error) |
| `hx doti update --repo <p> [--force] [--dry-run] --json` | `DotiWorktreeUpdate` | Success / Validation / Integrity | `doti-not-a-repo`, `git-required` (validation), `doti-update-failed` (integrity) |
| `hx doti update-all --root <d> [--force] [--dry-run] --json` | `DotiBatchUpdater` | Success / Partial / Validation | `git-required` (validation); per-repo failures → `Partial` + `doti-update-failed` in `data` |

- **Envelope:** all return `CliResult`, JSON-first (no console writes); `data` carries `DotiRepoVersion`/`DotiScanResult`/`DotiUpdateOutcome`/`DotiUpdateAllSummary`. Human rendering (rich + plain) via the shared kernel renderer; `--plain-help`/`NO_COLOR` honored (FR-019).
- **`describe`:** the four commands + their options + exit classes + the new codes join the capability model automatically via the command tree + registry.
- **Error codes** appended to `errorcodes/registry.json` then `hx errorcodes render` + `errorcodes check` (append-only `shipped.json`).

## Command Availability

| Area | Command | Status |
| --- | --- | --- |
| Build | `dotnet build .\scaffold-dotnet.slnx -c Release --no-restore /m:1` | implemented |
| Test | `dotnet test .\scaffold-dotnet.slnx -c Release --no-build /m:1` | implemented |
| Gate | `hx gate run --repo . --profile normal --json` | implemented |
| Architecture | `hx architecture test --repo . --json` | implemented |
| `hx doti check-version` / `scan` / `update` / `update-all` | — | **planned (this cycle); advisory until `/07`** |
| `hx version --repo` targetRelation fix | `hx version --repo --json` | implemented (command exists; behavior fixed this cycle) |

## Constitution Check (re-check, after design)

Re-evaluated: the design introduces no §1 violation (deterministic proofs preserved, nothing downgraded enforced→advisory, logic in Core, git/cross-platform, customization-safe) and bends no §2 declaration. **Verdict: PASS.** No Complexity-Tracking entries required.

## Complexity Tracking

None — no constitution violation; the one structural addition (within-core edge) is a sanctioned, precedented pattern requiring no rule change.

## Risks

- **Worktree apply-back scope:** `git diff --cached` captures tracked + newly-added managed assets; a managed asset that is *gitignored* in the target repo would not travel in the patch. Mitigation: updates touch the tracked `.doti` payload (core/skills/agent-context/payload.json); the gitignored `.doti` runtime files (`cycle-state.json`/`gate-proof.json`) are not managed assets and are intentionally out of scope — `/06`/`/07` verify no managed asset is gitignored.
- **Acyclic invariant:** the new `Hx.Doti.Core → Hx.Runner.Core` edge requires `Hx.Runner.Core` never to reference `Hx.Doti.Core`; `max_cycles=0` + Sentrux hold this, but `/06` must confirm no reverse edge is introduced.
- **`version --repo` contract stability:** the label set (`unknown`/`behind`/`equal`/`newer`) is preserved; only the source (`payload.json`) and computation change — `/07` adds a regression test that a clean adopted repo reads `equal`, not `newer`.
