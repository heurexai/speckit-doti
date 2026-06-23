# Spec: Release v0.4.0

## Goal

Publish `speckit-doti` v0.4.0 as a minor release containing the trusted prerequisite preflight/update work, release-ready documentation, GitHub release archives, and a Windows MSIX package path suitable for Microsoft Store submission.

## Scope

**Included**
- Update release-facing documentation for v0.4.0 before tagging.
- Preserve the committed Sentrux baseline by fixing any release-prep structural regression in source shape rather than refreshing the baseline.
- Use the command-backed release lane as local proof before pushing the release tag.
- Create the release tag only through `version bump --minor`.
- Push the release tag so `.github/workflows/release.yml` publishes GitHub Release archives.
- Produce or trigger the MSIX packaging path for v0.4.0 when the matching tag/ref is available.
- Update post-release package-manager templates only after the new archive hashes exist.

**Excluded**
- Manual Partner Center listing work.
- Publishing external winget/Homebrew pull requests before GitHub release assets exist.
- Bypassing GitVersion, release workflow version checks, or Doti commit gates.

## Functional Requirements

- `FR-001`: v0.4.0 documentation MUST name the new prerequisite preflight/install behavior and current release version.
- `FR-002`: The changelog MUST include a dated v0.4.0 entry before tagging.
- `FR-003`: The release-prep commit MUST keep Sentrux regression passing without creating or refreshing `.sentrux/baseline.json`.
- `FR-004`: The local release lane MUST pass before the release tag is pushed.
- `FR-005`: The release tag MUST be created through `version bump --minor`.
- `FR-006`: The GitHub Release workflow MUST publish win-x64, linux-x64, and osx-arm64 archives plus `.sha256` sidecars.
- `FR-007`: The published archive `hx --version` checks MUST match the tag version exactly.
- `FR-008`: The MSIX path MUST build from the matching v0.4.0 tag/ref so `hx.exe --version` matches the MSIX version prefix.

## Success Criteria

- `SC-001`: `CHANGELOG.md`, `README.md`, and publishing/store notes no longer present v0.3.1 as the current release.
- `SC-002`: `gate run --profile release --repo . --json` passes on the v0.4.0 release-doc/tag state.
- `SC-003`: GitHub Actions release workflow for tag `v0.4.0` completes successfully.
- `SC-004`: GitHub Release `v0.4.0` exists with all expected archives and checksum sidecars.
- `SC-005`: A v0.4.0 MSIX package is produced locally or by the store workflow for Store submission.

## Deterministic Surfaces

- `version calculate` and `version bump --minor` are the version authority.
- `gate run --profile release` is the local release proof.
- `.github/workflows/release.yml` is the GitHub Release archive producer.
- `.github/workflows/store-release.yml` and `packaging/STORE.md` define the MSIX path.

## Clarifications

### 2026-06-23
- The operator classified this as a minor release.
- The GitHub Release assets must be produced from a tag, not from ordinary CI on `main`.
- A clean `HEAD` replay showed Sentrux failed on `God files: 4 -> 5`; release prep must resolve that by behavior-preserving source/CLI command splitting, not by updating the baseline.
