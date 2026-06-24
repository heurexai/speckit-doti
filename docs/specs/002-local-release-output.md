# Spec: Local Release Output Directory

## Goal

Allow Doti release work to produce a local, reviewable release copy in an operator-controlled directory so release artifacts can be consumed by local tools, sibling repositories, and manual submission flows without depending only on GitHub Release publishing.

By default, when `DOTI_RELEASE_ROOT` is available, a successful release output MUST be copied under that root using the stable layout `<projectName>/<version number>` and also copied to `<projectName>/latest`.

## Scope

Included behavior:

- Users can request a release output copy to a specified local release root.
- Users can optionally name which environment variable should provide the local release root.
- Users can optionally save an explicitly supplied release root into the selected release-root environment variable for future runs.
- When no explicit local release root is supplied, the release process reads the configured release-root environment variable; if none is configured, it defaults to `DOTI_RELEASE_ROOT`.
- Release output is written to `<releaseRoot>/<projectName>/<version number>` and also to `<releaseRoot>/<projectName>/latest`.
- The version folder and `latest` folder contain equivalent release artifacts for the same version.
- The local release copy includes enough identity and checksum information for a human or tool to verify which project/version produced it.
- The release process reports the resolved release root, project name, version, written paths, and whether the copy was explicit, environment-defaulted, or skipped.
- Associated CLI help, machine-readable CLI description, and README/release documentation describe the explicit release-root option, environment-variable-name option, `DOTI_RELEASE_ROOT` default behavior, output layout, and skipped-copy case.
- The copy into the local release root is command-backed release-tool behavior, not an instruction for an agent to copy files manually.
- The local copy step fails hard on unsafe paths, missing prerequisites, partial-copy errors, version mismatch, or checksum mismatch.

Excluded behavior:

- Publishing or uploading to GitHub Releases, package managers, or Microsoft Store.
- Changing the GitVersion version authority, tag format, or release gate policy.
- Replacing the existing GitHub Actions release workflow.
- Committing local release artifacts back into the source repository.
- Inferring a machine-specific fallback release root when no explicit root and no selected release-root environment variable value exist.
- Treating an agent-performed manual file copy as release proof.

## Functional Requirements

- `FR-001`: Users MUST be able to direct release output to an explicit local release root.
- `FR-002`: Users MUST be able to provide an optional environment-variable-name input that tells the release process which environment variable should be read as the local release root.
- `FR-003`: If no explicit local release root is supplied and the environment-variable-name input is supplied, the system MUST read that named environment variable as the candidate local release root.
- `FR-004`: If no explicit local release root is supplied and no environment-variable-name input is supplied, the system MUST read `DOTI_RELEASE_ROOT` as the default candidate local release root.
- `FR-005`: If the selected environment variable is set to a usable directory, the system MUST copy the release output to that environment-defined root by default.
- `FR-006`: If neither an explicit local release root nor the selected environment variable is available, the release process MUST continue without a local copy only when the release command is otherwise valid, and MUST report that the local release copy was skipped because no root was configured.
- `FR-007`: The local release layout MUST be `<releaseRoot>/<projectName>/<version number>` for the immutable version copy and `<releaseRoot>/<projectName>/latest` for the current copy.
- `FR-008`: The `<version number>` segment MUST use the calculated release version without a tag prefix so local directories sort and compare by semantic version.
- `FR-009`: The `<projectName>` segment MUST be deterministic for the releasing project and safe for local filesystem paths.
- `FR-010`: The `latest` folder MUST contain a physical copy of the same release artifact set as the matching version folder, not a pointer whose behavior depends on platform-specific link support.
- `FR-011`: A successful local release copy MUST leave both the version folder and `latest` folder complete and internally consistent for one version.
- `FR-012`: The local release copy MUST fail hard rather than leave `latest` pointing at, or containing, a partial or mixed-version artifact set.
- `FR-013`: The local release copy MUST include release identity metadata that records project name, version, source commit, release command identity, artifact list, and checksum information.
- `FR-014`: The local release copy MUST verify that copied files match the release artifacts that were produced for the same version.
- `FR-015`: The release report MUST identify the local release root source as explicit, named environment variable, default `DOTI_RELEASE_ROOT`, or unavailable.
- `FR-016`: The release report MUST include the selected environment variable name when environment-variable resolution is used or attempted.
- `FR-017`: The release report MUST list the version directory and latest directory paths when a local copy is produced.
- `FR-018`: The release process MUST reject release roots that resolve inside the source repository unless explicitly allowed by a future design.
- `FR-019`: The release process MUST reject path traversal, empty project names, empty versions, invalid version identity, invalid environment-variable names, and any resolved path that escapes the chosen release root.
- `FR-020`: Re-running the same release for the same project/version MUST be deterministic: existing matching version output may be reused or replaced only after checksum verification proves it belongs to the same release identity.
- `FR-021`: Re-running the same release MUST not silently overwrite a different project/version in `latest`; any existing `latest` contents must be replaced only after the new version output is complete and verified.
- `FR-022`: Release artifacts copied to the local release root MUST remain outside source control by default and MUST NOT require adding local absolute paths to tracked files.
- `FR-023`: The release CLI's human help MUST document the explicit local release-root input, the environment-variable-name input, the `DOTI_RELEASE_ROOT` default, the `<projectName>/<version number>` and `<projectName>/latest` layout, and the behavior when no local release root is configured.
- `FR-024`: The release CLI's machine-readable description MUST expose the local release-root input, the environment-variable-name input, the environment-default behavior, the local-copy effects that can be reported, and the skipped-copy reason.
- `FR-025`: README and release documentation MUST describe the local release directory behavior only once it is implemented, including explicit-root precedence, configurable environment-variable lookup, `DOTI_RELEASE_ROOT` defaulting, the version/latest layout, checksum expectations, and the fact that local release artifacts stay out of source control.
- `FR-026`: The local release copy MUST be performed by command-backed release tooling; agents MUST NOT satisfy this behavior by manually copying files or by following prose-only release instructions.
- `FR-027`: The command-backed copy MUST produce a machine-readable release-copy result or proof that records skipped, succeeded, and failed outcomes.
- `FR-028`: The command-backed copy MUST stage or otherwise verify the version output before replacing `latest` so `latest` is never the sole proof of a release and never represents a partially copied release.
- `FR-029`: The explicit local release root input MUST be exposed as `--release-root <path>` unless a later implementation design proves the CLI already has a more consistent option name.
- `FR-030`: The environment-variable-name override MUST be exposed as `--release-root-env <name>` unless a later implementation design proves the CLI already has a more consistent option name.
- `FR-031`: When `--release-root <path>` and `--release-root-env <name>` are both supplied, `--release-root` MUST win, the environment variable MUST NOT be read to choose the root, and the result/proof MUST report that the environment-variable override was ignored because an explicit root was supplied.
- `FR-032`: The release-copy result/proof MUST capture the requested environment-variable-name input, the effective environment variable name, whether that variable was read, and the final root-source decision.
- `FR-033`: Users MUST be able to request that an explicit `--release-root <path>` value be saved into the selected release-root environment variable for future runs.
- `FR-034`: The save-release-root behavior MUST be opt-in; supplying `--release-root <path>` alone MUST NOT persist or modify any environment variable.
- `FR-035`: The save-release-root behavior MUST save to `DOTI_RELEASE_ROOT` by default, or to the variable named by `--release-root-env <name>` when that option is supplied.
- `FR-036`: The save-release-root behavior MUST fail closed when no explicit `--release-root <path>` is supplied, because the command must not persist a root discovered from the environment back into the environment.
- `FR-037`: The release-copy result/proof MUST capture whether environment persistence was requested, which variable name was targeted, whether the value was written, and any platform/scope limitation that prevented persistence.
- `FR-038`: The save-release-root input MUST be exposed as `--save-release-root` unless a later implementation design proves the CLI already has a more consistent option name.

## Success Criteria

- `SC-001`: With `DOTI_RELEASE_ROOT` set and no explicit local release root supplied, a release produces both `<DOTI_RELEASE_ROOT>/<projectName>/<version number>` and `<DOTI_RELEASE_ROOT>/<projectName>/latest`.
- `SC-002`: With an explicit local release root supplied, a release produces both `<explicitRoot>/<projectName>/<version number>` and `<explicitRoot>/<projectName>/latest` and reports that the explicit root was used.
- `SC-003`: With an alternate environment variable name supplied and that environment variable set, a release uses that variable's value instead of `DOTI_RELEASE_ROOT` and reports the selected variable name.
- `SC-004`: With no explicit root and no selected environment variable value, a release report clearly states that the local release copy was skipped because no local release root was configured.
- `SC-005`: The version folder and `latest` folder contain the same artifact names and matching checksums for the released version.
- `SC-006`: A simulated partial-copy or checksum mismatch causes a hard failure and does not leave `latest` as a mixed or partial release.
- `SC-007`: Re-running an identical release does not create duplicate project/version layouts or change the release identity metadata.
- `SC-008`: The local release copy behavior works without committing any local release path or generated release artifact to the repository.
- `SC-009`: Human CLI help mentions `DOTI_RELEASE_ROOT`, the optional environment-variable-name input, explicit local release-root override, `<projectName>/<version number>`, `<projectName>/latest`, and the no-root skipped-copy outcome.
- `SC-010`: The CLI `describe --json` output includes the local release-root option/default metadata, environment-variable-name option metadata, and enough effect/result fields for an agent to know where the version and latest directories were written.
- `SC-011`: README or release documentation includes a local release directory section that matches the implemented CLI behavior and does not present planned behavior as already available before implementation.
- `SC-012`: A release run can be verified from the CLI result/proof alone; no acceptance check depends on an agent statement that files were copied manually.
- `SC-013`: Human CLI help and `describe --json` show `--release-root <path>` and `--release-root-env <name>` with the precedence rule that an explicit root overrides environment-variable lookup.
- `SC-014`: A run with both `--release-root <path>` and `--release-root-env <name>` copies to the explicit path and reports that the environment-variable override was ignored.
- `SC-015`: A run with only `--release-root-env <name>` records the requested name, reads that variable, and reports the variable as the local release-root source when it is usable.
- `SC-016`: A run with `--release-root <path> --save-release-root` records that `<path>` was saved to `DOTI_RELEASE_ROOT` when no environment-variable-name override is supplied.
- `SC-017`: A run with `--release-root <path> --release-root-env <name> --save-release-root` records that `<path>` was saved to `<name>`.
- `SC-018`: A run with `--save-release-root` but without `--release-root <path>` fails with a targeted diagnostic and does not write any environment variable.

## Key Entities

- **Local release root** - The operator-provided directory, or the selected environment variable value when no explicit root is supplied, under which project release copies are organized.
- **Release-root environment variable name** - The optional release command input that chooses which environment variable supplies the local release root; defaults to `DOTI_RELEASE_ROOT`.
- **Release-root persistence request** - The opt-in command request to save an explicit release root path into the selected release-root environment variable for future runs.
- **Root-source decision** - The command-backed record of how the release root was selected: explicit `--release-root`, selected environment variable, default `DOTI_RELEASE_ROOT`, or unavailable.
- **Project name** - The deterministic release-store name for the project, used as the first directory below the release root.
- **Version number** - The calculated release version without a leading tag prefix, used as the immutable release directory name.
- **Version release directory** - The complete local copy for one project/version.
- **Latest release directory** - A physical copy of the most recent successfully produced local release for the project.
- **Release identity metadata** - Machine-readable metadata proving which source, version, command identity, and artifact checksums produced the local copy.

## Deterministic Surfaces

- `version calculate` remains the version authority for the release version.
- `version bump --major|--minor` remains the sole command-backed version bump surface for major/minor releases.
- `gate run --profile release` remains the command-backed release proof before a release is accepted.
- `hx release --repo <path> [--rid <rid>] [--release-root <path>] [--release-root-env <name>] [--save-release-root] --json` is the command-backed local release-copy surface.
- `--release-root <path>` is the explicit local release-root option.
- `--release-root-env <name>` is the environment-variable-name override option.
- `--save-release-root` is the opt-in environment persistence option.
- `DOTI_RELEASE_ROOT` is the default environment variable name when no explicit release root and no alternate environment-variable-name input are supplied.
- `release.identity.json` in the local release directory records release identity and artifact checksums.
- Shared CLI help rendering and `describe --json` capability metadata document the local release-root option, the optional environment-variable-name input, and the environment default.
- README and release documentation describe the command-backed local release-store guidance.

## Architecture Impact

- Release packaging and local-copy behavior MUST be owned by command-backed release tooling rather than by advisory skill text or agent instructions.
- The local release-copy result must use the existing agent-first CLI envelope style so agents can read the resolved root, produced paths, checksums, skipped-copy reason, and failure diagnostics.
- The release-copy result must capture requested option values separately from effective decisions so a reviewer can see whether `--release-root-env` was used, ignored, invalid, unset, or defaulted.
- Environment-variable persistence must be represented as a separate command effect from release copying so a reviewer can see whether the command wrote an environment variable as well as where it copied artifacts.
- Help text and command description metadata must be generated or maintained from the same command model used by the release CLI so human and agent-facing documentation cannot diverge.
- Version calculation and release proof remain separate from local copying; a local copy cannot make an otherwise invalid release valid.
- The design must preserve cross-platform path handling while treating `<projectName>/<version number>` and `<projectName>/latest` as the logical layout.

## Sentrux And Hygiene Impact

- Local release artifacts must not be committed to the repository and should remain outside normal source scans unless a command explicitly scans the release output.
- The release root must not be recorded in tracked files because it can contain machine-specific or user-specific paths.
- Release identity metadata must not include secrets, private machine identifiers, or unnecessary absolute source paths.
- Public release hygiene remains required for the artifacts that are copied into the local release store.

## Assumptions

- The project directory segment should be the release project name, such as `speckit-doti`, unless a later plan identifies a stricter source of truth.
- The version directory should use the semantic version value, such as `0.6.0`, rather than the tag name `v0.6.0`.
- The explicit local release root, when supplied, takes precedence over any environment-variable lookup.
- The optional environment-variable-name input is spelled `--release-root-env <name>` unless implementation design proves an existing release-command naming convention is better.
- The optional environment-variable-name input changes which variable is read, and also selects the target variable for save-release-root when that opt-in persistence request is supplied.
- A provided release root is saved only when the operator supplies the save-release-root option; this avoids surprising environment mutation during ordinary release runs.
- A physical `latest` copy is required because the operator asked for a copy into the latest folder, and physical copies are easier for non-coding consumers and cross-platform tools to consume than symlinks or junctions.

## Acceptance

Command-backed today:

- `version calculate` can identify the release version.
- `version bump --major|--minor` can record major/minor release intent.
- `gate run --profile release` can prove release readiness.
- `hx release` can produce the artifact/checksum/identity set and copy it to the resolved local release root.
- `hx release --release-root-env <name>` selects a non-default release-root variable for discovery and opt-in persistence.
- `hx release --save-release-root` persists only an explicit `--release-root` value.
- `hx release` defaults release-root discovery to `DOTI_RELEASE_ROOT`.
- `hx release` creates `<projectName>/<version number>` and `<projectName>/latest` local release directories.
- `hx release` writes and verifies local release identity metadata and copied-artifact checksums.
- `hx release --json` reports local release-copy effects through the CLI result envelope.
- CLI help, CLI `describe --json`, README, and release documentation describe the implemented default behavior.
- Prose-only or agent-manual file copying is not release proof.

## Clarifications

### 2026-06-24

- Q: Is the local release copy an agent instruction or codified behavior? -> A: It must be codified command-backed release-tool behavior. Agent/manual file copying is not release proof.
- Q: What is the default release-root environment variable? -> A: Use `DOTI_RELEASE_ROOT` by default.
- Q: How is the environment-variable override expressed? -> A: Use `--release-root-env <name>` to select a different variable name for discovery and for opt-in persistence.
- Q: How is an explicit release root expressed and persisted? -> A: Use `--release-root <path>` for the current release, and `--save-release-root` to opt into saving that explicit path to `DOTI_RELEASE_ROOT` or to the variable named by `--release-root-env <name>`.
- Q: What happens if `--save-release-root` is supplied without `--release-root <path>`? -> A: The command must fail hard with a targeted diagnostic and must not write any environment variable.
