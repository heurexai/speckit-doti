# Changelog

All notable changes to speckit-doti are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); the project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html) (pre-1.0: minor versions may include breaking changes).

## [Unreleased]

- **Changed** `hx release` now owns release intent and local tag creation (`--major|--minor|--patch`) and records tag/GitVersion/Velopack payload identity in the local release result.
- **Removed** the bespoke repository updater command and the runner-side standalone tag command; Velopack owns installed executable updates, while `doti install` remains the repo workflow asset install/repair path.

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
