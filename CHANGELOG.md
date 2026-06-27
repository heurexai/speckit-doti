# Changelog

All notable changes to speckit-doti are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); the project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html) (pre-1.0: minor versions may include breaking changes).

## [Unreleased]

- **Added** `008-doti-review-recovery-and-change-context`: review **recovery** and deterministic **change context** for the Doti cycle. New `hx doti cycle refresh-plan`/`refresh --apply-safe` recovers a stale cycle by re-binding only the safe-to-reinterpret stamps — classifying each stage `SafeReinterpret` / `RerunRequired` / `NotBound` with typed stale reasons and `nextActions` hints — instead of hand-guessing a stamp. A reusable status-rich `ChangeSetContext` (Added/Modified/Deleted/Renamed) drives `hx doti review-context` (arch-review lens applicability as data), `impact plan --for change-context`, and a docs-only gate/Sentrux scope skip (`GateScope`, recomputed at validation).
- **Changed** Sentrux is now source/code scoped (`.sentruxignore` excludes `.md`/docs/Doti prose/rendered skills/payload metadata), gains an escalation-band **two-try** optimization diagnostic, and gates rebaseline behind explicit operator intent **plus** a change-set-fresh arch-review record classifying the growth as functionality-driven — the baseline is **never** removed. Adds cross-feature **release-train drift** detection (a later feature changing an earlier completed-unreleased feature's owned paths), single-source materialized `.doti/templates` (the committed twin is removed; templates are materialized from `.doti/core/templates`), and the unnumbered utility skills `doti-amend` / `doti-drift-fix`.
- **Added** an **advisory, never-gating, fully-offline** semantic drift finder — `hx doti drift-candidates` embeds changed code vs reference prose on a local CPU model (primary **Qwen3-Embedding-0.6B** via GGUF/LLamaSharp, fallback **BGE-M3** via ONNX), pinned + hash-verified before native load, reporting the active engine; an empty candidate list is explicitly not a clean-bill signal. The reusable `Hx.Embedding.Core` library carries zero workflow dependencies, and `Hx.Gate.Core`/`Hx.Cycle.Core` are compile-checked to never depend on the semantic stack.
- **Added** `007-standalone-hx-source-independent-cli`: installed `hx` runs the full workflow **source-free** (`gate run`, `architecture test`, `sentrux verify`/`check`, `hygiene`/`security scan`, `version calculate`, `doti cycle`/`render-skills`/`install`/`payload check`/`install-hooks`, and `impact plan` — FR-045), resolved from a payload beside the executable (`PayloadRoot`, `HX_PAYLOAD_ROOT`). Two distribution channels — the **NuGet .NET global tool** `Heurex.SpeckitDoti` and the **Microsoft Store MSIX** — three install tiers (`workflow-only` / `dotnet-lib` / `dotnet-cli-heurex`) that scale the gate ladder to how much governance a repo adopts, and absorbed utilities: the enforced bug mini-cycle (`hx doti bug`), `hx doti converge` (spec↔tasks drift), the "unit tests for English" requirement checklist, and machine-readable arch-review changed-files context (`impact plan --for arch-review`).
- **Removed** Velopack and `vpk` entirely (superseding 006's Velopack-only release output): no installer/update packages, no `vpk pack`, and scaffolded apps carry no Velopack stub. Installed `hx` updates via its channel (`dotnet tool update -g Heurex.SpeckitDoti`, or the Microsoft Store); `hx doti install` remains the repo workflow-asset install/repair path.
- **Changed** `hx release` now packs the `Heurex.SpeckitDoti` global tool (`dotnet pack`) with a source-free install smoke and records the Microsoft Store MSIX channel proof, instead of staging Velopack artifacts. The default version intent is cycle-type-aware (feature → `minor`, bug-fix-only → `patch`); a pushed `v*` tag drives NuGet Trusted Publishing (`release.yml`) and the Store workflow (`store-release.yml`).
- **Added** `006-task-hash-gated-velopack-completion`: task-hash-gated implementation proof, multi-feature release-train handling, Velopack-only installer/update release artifacts, scaffold payload parity proof, executable-local `hx.config.json`, generated starter configuration, release documentation proof, and stable diagnostics for the new fail-closed surfaces.
- **Changed** `hx release` now owns release intent and local tag creation (`--major|--minor|--patch`) and records tag/GitVersion/Velopack payload identity in the local release result.
- **Removed** the bespoke repository updater command and the runner-side standalone tag command; Velopack owns installed executable updates, while installed `hx doti install` is the released repo workflow asset install/repair path. The source-runner `doti install` path remains developer-only.
- **Added released `hx doti install`** so installed `hx` can install, repair, or migrate repo-owned Doti workflow assets from the `.doti` payload beside `hx.exe`, with explicit `--repo`, target classification proof, hook arming, and no source checkout or `dotnet run` requirement.

## [0.4.0] - 2026-06-23

- **Added** trusted prerequisite preflight: `hx prereq check` reports .NET SDK, Git, directory readiness, and manifest identity; `hx prereq install` runs only a release-defined, digest-approved Windows winget plan.
- **Changed** `hx new` and `hx update` now run prereq/directory preflight before side effects, generated repos carry `.doti/prerequisites.json`, and `hx version --repo` reports read-only prereq health.
- **Fixed** standalone archives can run `hx new` from the extracted payload without a `.git` checkout; the template pack no longer imports GitVersion, and scaffold failures include stage evidence.

## [0.3.1] - 2026-06-23

### Added
- **Existing-repo update path** — `hx version` and `hx update` now report the running toolkit, target repo scaffold stamp, and exact managed template/skill modification state; update resolves the latest GitHub Release asset, verifies checksums, uses a temporary cache, creates a backup worktree by default, and preserves live repo configuration such as Sentrux baselines.
- **Cycle recovery hardening** — completed doti cycles now recover cleanly from post-commit completion-state failures, repeated completion is idempotent, and `doti cycle commit` re-verifies the gate proof, affected-test hashes, staged tree, and Doti trailers before accepting a commit.

### Changed
- **Version stamping is GitVersion-backed and release-clean** — all scaffold CLIs now report the public product version only (`0.3.1` for this release), without branch names, commit SHA, prerelease height, or build metadata.
- **Release and Store packaging are version-guarded** — GitHub Release and Store workflows fetch full tag history and fail if the packaged `hx` version does not exactly match the release/MSIX version.
- **Human help is fully shared** — root commands, command groups, and leaf-command help use the same rich/plain renderer and support ANSI-free output through `--help-mode plain`, `--plain-help`, `HX_HELP_MODE=plain`, and `NO_COLOR`.

### Fixed
- Release hygiene now scans the working tree instead of sticky git history for full-scope checks and excludes generated release/package artifacts.
- Public docs and packaging notes avoid local-machine path markers that can trip hygiene or secret-scan heuristics.

## [0.3.0] - 2026-06-23

### Added
- **Heurex-branded CLI experience (Spectre.Console)** — human/TTY output now renders a branded figlet banner + a rounded command table for help, and `hx new` shows **live per-step progress bars** with a final summary panel instead of a long silent pause followed by a single line. Navy/gold Heurex palette throughout.

### Changed
- All human rendering flows through **Spectre.Console in `Hx.Cli.Kernel`** (the single human-output site) — no thin-CLI / channel-independence rule violation. The scaffold runners (`ScaffoldNewRunner`, `FirstSmokeRunner`) emit a `CliEvent` per step through an optional progress sink, so a channel can render progress without the core depending on the renderer.
- The agent-first JSON envelope is **byte-identical** in `--json`/piped mode (the progress callback is a no-op there), so the machine contract is unchanged. `Spectre.Console` is pinned in both `Directory.Packages.props` files so the vendored kernel restores in generated products too.

## [0.2.0] - 2026-06-21

### Added
- **Cross-platform installers** — the release builds a standalone installer for **win-x64** (`.zip`), **linux-x64**, and **osx-arm64** (`.tar.gz`) on native runners, each attached to the GitHub Release. The vendored tool manifests gained linux-x64 + osx-arm64 assets (the Heurex sentrux fork ships every platform), and `ToolFetcher` now extracts `.tar.gz`, not just `.zip`.
- **Per-RID sentrux grammars** vendored in-repo (linux `.so` + macOS `.dylib` alongside the win `.dll`); the store, resolvers, and grammar stager were already RID-keyed.
- **Package-manager templates** — a winget manifest + Homebrew formula + `packaging/PUBLISHING.md` (published post-release, once the archive hashes exist).

### Note
- Archives and `hx` remain unsigned; macOS notarization + Windows Authenticode are deferred until certificates are available.

## [0.1.0] - 2026-06-21

First tagged release — the toolkit is published as a downloadable, standalone win-x64 installer.

### Added
- **Standalone installer** — a self-contained `hx.exe` plus the bundled scaffold payload (template, doti workflow, source projects, and the vendored tool binaries), published as a GitHub Release asset. `hx new` scaffolds a project from any directory (the payload is resolved relative to the executable).
- **Shared tool store** — the vendored binaries (Gitleaks, Sentrux, GitVersion) install once into a per-user data folder with an environment override; generated solutions resolve them store-first (in-repo fallback) instead of carrying a ~127 MB per-solution copy.
- **Thin-CLI architecture enforcement** — a *Channel Independence (Thin Adapter)* constitution principle, plan/arch-review channel-boundary checks, and two new ArchUnit families (`cliSurfaceConfinement`, `cliDelegation`) shipped in the generated template (nine families total) and dog-fooded on the toolkit's own `Hx.*.Cli` projects.
- `tools fetch` — deterministic, hash-verified provisioning of the vendored tool binaries from their pinned manifests (fail-closed on mismatch).
- Agent-first CLI self-description (structured `<PREFIX><NNNN>` error codes, `describe`, the `CliResult` envelope) encoded into the doti workflow.

[Unreleased]: https://github.com/heurexai/speckit-doti/compare/v0.4.0...HEAD
[0.4.0]: https://github.com/heurexai/speckit-doti/compare/v0.3.1...v0.4.0
[0.3.1]: https://github.com/heurexai/speckit-doti/compare/v0.3.0...v0.3.1
[0.3.0]: https://github.com/heurexai/speckit-doti/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/heurexai/speckit-doti/releases/tag/v0.2.0
[0.1.0]: https://github.com/heurexai/speckit-doti/releases/tag/v0.1.0
