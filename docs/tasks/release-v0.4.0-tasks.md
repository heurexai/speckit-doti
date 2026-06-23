# Tasks: Release v0.4.0

- [ ] `T001` Update release-facing docs (`CHANGELOG.md`, `README.md`, `packaging/PUBLISHING.md`, `packaging/STORE.md`) for v0.4.0 without guessing archive hashes.
- [ ] `T002` Root-cause and fix any Sentrux structural regression without refreshing `.sentrux/baseline.json`.
- [ ] `T003` Run release-prep verification: render-skill drift check, staged hygiene, and a normal gate proof.
- [ ] `T004` Commit the release prep through `doti cycle commit`.
- [ ] `T005` Create the minor release tag with `version bump --minor`; verify `version calculate` reports `0.4.0`.
- [ ] `T006` Run the release lane after the tag exists.
- [ ] `T007` Push `v0.4.0` and verify the GitHub Release workflow completes.
- [ ] `T008` Verify GitHub Release assets: win-x64 zip, linux-x64 tarball, osx-arm64 tarball, and all `.sha256` sidecars.
- [ ] `T009` Produce or dispatch MSIX packaging for v0.4.0 from the matching tag/ref.
- [ ] `T010` Update package-manager templates with actual v0.4.0 hashes after assets exist, if this release pass includes post-release manifest updates.

## Current Status Notes

- The prerequisite feature commit is already on `main` and CI passed.
- The release is minor by operator instruction.
- GitHub Release publication is tag-driven, not ordinary CI-driven.
- Sentrux RCA showed the prereq feature raised the god-file regression only when source and CLI files were combined; the release prep splits command/preflight partials and keeps the committed baseline unchanged.
