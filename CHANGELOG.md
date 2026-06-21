# Changelog

All notable changes to speckit-doti are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); the project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html) (pre-1.0: minor versions may include breaking changes).

## [Unreleased]

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
- **Shared tool store** — the vendored binaries (Gitleaks, Sentrux, GitVersion) install once into a versioned, RID-keyed, SHA-256-verified per-user store (`%LOCALAPPDATA%\Heurex\speckit-doti\tools`, overridable with `HX_TOOL_STORE`); generated solutions resolve them store-first (in-repo fallback) instead of carrying a ~127 MB per-solution copy.
- **Thin-CLI architecture enforcement** — a *Channel Independence (Thin Adapter)* constitution principle, plan/arch-review channel-boundary checks, and two new ArchUnit families (`cliSurfaceConfinement`, `cliDelegation`) shipped in the generated template (nine families total) and dog-fooded on the toolkit's own `Hx.*.Cli` projects.
- `tools fetch` — deterministic, hash-verified provisioning of the vendored tool binaries from their pinned manifests (fail-closed on mismatch).
- Agent-first CLI self-description (structured `<PREFIX><NNNN>` error codes, `describe`, the `CliResult` envelope) encoded into the doti workflow.

[Unreleased]: https://github.com/heurexai/speckit-doti/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/heurexai/speckit-doti/releases/tag/v0.2.0
[0.1.0]: https://github.com/heurexai/speckit-doti/releases/tag/v0.1.0
