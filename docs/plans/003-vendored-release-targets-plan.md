# Plan: Vendored Release Targets

## Goal

`hx release` must work when `hx` is the vendored scaffold executable inside a target repository. The command must publish the target product declared by that repository, not assume the target contains the speckit-doti scaffold CLI source project.

## Design

- Add a tracked repo-owned release manifest at `.doti/release.json`.
- Load the manifest before calculating release-root decisions or running `dotnet publish`.
- Validate the manifest fail-closed: schema version, required fields, safe names, environment variable name, relative in-repo `.csproj`, and existing project file.
- Use the manifest as the release target source for package/archive name, publish project, published executable name, output executable name, and default release-root environment variable.
- Include the resolved target in `LocalReleaseResult` and `release.identity.json`.
- Write a default release manifest during `hx new` so generated repos can release without requiring the scaffold source project.
- During `hx update`, preserve an existing release manifest as live repo configuration. For older repos that lack one, create a default only when one non-tool, non-test executable `.csproj` is unambiguous; otherwise report a follow-up for the agent/operator to add `.doti/release.json`.
- Preserve `002-local-release-output` behavior for `--release-root`, `--release-root-env`, `--save-release-root`, checksum output, version/latest directories, and machine-readable proof.

## Proof

- Focused unit tests cover manifest load, missing manifest validation, unsafe publish project rejection, and target default release-root environment selection.
- Update tests cover older-repo manifest creation from a single executable project.
- Build proof compiles `Hx.Scaffold.Core`, `Hx.Scaffold.Cli`, and release contracts after the result shape change.
- Command proof runs `hx release --repo . --release-root <temp> --json` for speckit-doti.
- Optional live proof can run the rebuilt `hx release --repo D:\github\heurex\ergon --release-root <temp> --json` after Ergon has a repo-owned `.doti/release.json`.
