# Plan: Release v0.4.0

## Approach

Prepare a release-prep commit, including docs plus any behavior-preserving Sentrux shape fix needed for the gate, pass the Doti commit gate, create `v0.4.0` through `version bump --minor`, run a release gate with the tag in place, push the tag, then verify the GitHub Release assets and MSIX package path.

## Steps

1. Update `CHANGELOG.md`, `README.md`, `packaging/PUBLISHING.md`, and `packaging/STORE.md` for v0.4.0.
2. If Sentrux reports structural regression, root-cause it and split source/CLI command files instead of refreshing `.sentrux/baseline.json`.
3. Commit the release-prep change set through `doti cycle commit`.
4. Run `version calculate` and `version bump --minor` to create the annotated `v0.4.0` tag.
5. Run `gate run --profile release` after the tag exists.
6. Push `v0.4.0` and watch `.github/workflows/release.yml`.
7. Verify release assets and `.sha256` sidecars.
8. Build or dispatch MSIX packaging for v0.4.0 from the matching tag/ref.
9. After release assets exist, update package-manager templates with actual v0.4.0 hashes in a follow-up commit if needed.

## Gates

- `doti render-skills --check`
- `hygiene scan --scope changed --source staged`
- `gate run --profile normal` for the release-doc commit
- `version bump --minor`
- `gate run --profile release` for the tagged state
- GitHub release workflow success for `v0.4.0`

## Risks

- Running the release workflow from `main` will not update GitHub Releases; it must be a `v*` tag push.
- A stale-looking Sentrux failure may be a real source-shape regression; do not refresh the baseline unless the operator explicitly chooses that path after RCA.
- Package-manager manifests need actual release hashes, so updating them before assets exist would be guesswork.
- MSIX packaging must run from the matching tag/ref or it will fail the `hx.exe --version` guard.
