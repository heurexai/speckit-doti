# Spec: Vendored release targets

> WHAT and WHY only. This feature fixes the 0.7.0 release regression where `hx release` works only in the speckit-doti toolkit repo because it assumes the target repo contains `tools/Hx.Scaffold.Cli`.

## Goal

`hx release` must work when the `hx` executable is run from a verified speckit-doti release payload against a doti-enabled target repository. The command must release the target repository's declared product executable, not the scaffold tool itself, and must fail with a clear target-configuration diagnostic when a repository has no release target.

This matters because installed repositories such as Ergon correctly carry Doti's vendored runner/tool source but do not carry `tools/Hx.Scaffold.Cli`. A release command that hard-codes the scaffold project breaks real generated/upgraded repositories and makes local release output unreliable for non-coding operators and agents.

## Scope

Included behavior:

- `hx release --repo <target>` runs from the vendored `hx` executable or from the speckit-doti source checkout and releases the target repo.
- The target repo declares its release product identity, publish project, executable name, package/archive name, and release-root environment default through a repo-owned release manifest or equivalent tracked configuration.
- Repositories without a release target fail before packaging with a specific diagnostic that identifies the missing release-target configuration.
- Repositories with a release target publish that target executable for the selected RID and place that executable into the release payload.
- The release archive name, local release-store project segment, identity metadata, and checksum proof use the target product identity, not `speckit-doti`, unless the target is the speckit-doti repo itself.
- speckit-doti remains releasable through the same generic mechanism by declaring itself as a release target for `hx`.
- Generated or updated repos receive a safe default release-target declaration when the template has a single CLI project.
- Upgraded repos such as Ergon can add or keep a repo-owned release-target declaration that names `src/Ergon.Cli/Ergon.Cli.csproj` and `ergon`.
- Release root behavior from `002-local-release-output` remains intact: explicit `--release-root` wins, `--release-root-env` selects the variable, `DOTI_RELEASE_ROOT` remains the default unless the target declares a different default, and `--save-release-root` is opt-in.
- `LocalReleaseResult` records the resolved release target identity and publish project so agents can see what was actually packaged.
- `hx update` preserves an existing `.doti/release.json`; when older repos have no manifest, update creates one only if exactly one non-tool, non-test executable project can be inferred.
- CLI help, `describe --json`, README, agent context, and generated doti release skill text make clear that `hx release` is target-repo driven.

Excluded behavior:

- Publishing to GitHub Releases, package managers, or Microsoft Store.
- Inferring a publish target by scanning every executable project when multiple candidates exist.
- Requiring target repos to vendor or contain `tools/Hx.Scaffold.Cli`.
- Treating manual agent file copying as release proof.
- Replacing product-specific release validation such as Ergon schema compatibility, signing, or store packaging.

## Functional Requirements

- `FR-001`: `hx release` MUST NOT require the target repo to contain `tools/Hx.Scaffold.Cli`.
- `FR-002`: `hx release` MUST release the target repo's declared product executable rather than always releasing `hx`.
- `FR-003`: Target repos MUST have a tracked release-target declaration before `hx release` can package product artifacts.
- `FR-004`: If no release-target declaration exists, `hx release` MUST fail before packaging with a specific diagnostic that names the missing release-target configuration and the target repo.
- `FR-005`: If a release-target declaration exists, `hx release` MUST publish the declared project for the requested RID.
- `FR-006`: The published executable copied into the payload MUST use the executable name declared by the target release configuration.
- `FR-007`: The release archive name MUST use the declared product/package name and calculated version.
- `FR-008`: The local release-store project directory MUST use the declared product/package name.
- `FR-009`: `release.identity.json` MUST record product name, package name, publish project, executable name, RID, version, source commit, command identity, and artifact checksums.
- `FR-010`: The local release proof MUST still create matching immutable version and `latest` directories when a release root is configured.
- `FR-011`: The existing explicit-root, environment-root, environment override, and opt-in save-root semantics MUST continue to work.
- `FR-012`: A target's release declaration MAY select a different default release-root environment variable, but command-line `--release-root-env` MUST still override it.
- `FR-013`: `hx release` MUST reject release-target declarations whose paths escape the target repository.
- `FR-014`: `hx release` MUST reject empty or unsafe product names, package names, publish-project paths, and executable names.
- `FR-015`: Generated single-CLI repos SHOULD receive a release-target declaration from the template or install/update process.
- `FR-016`: speckit-doti MUST declare its own release target so its release behavior remains command-backed and no longer depends on a hard-coded special case.
- `FR-017`: `hx release --json` MUST expose the release target details in `LocalReleaseResult`.
- `FR-018`: CLI help, `describe --json`, README, `.doti/agent-context.md`, and generated `doti-release` skill text MUST describe target-repo release behavior accurately.
- `FR-019`: `hx update` MUST NOT replace an existing `.doti/release.json`.
- `FR-020`: `hx update` SHOULD create `.doti/release.json` for older repos only when a single executable product project can be inferred without ambiguity.
- `FR-021`: If `hx update` cannot infer a release target, it MUST report a specific follow-up instead of guessing.

## Success Criteria

- `SC-001`: Running `hx release --repo <ergon> --release-root <temp> --json` from a speckit-doti `hx` build no longer attempts to publish `tools/Hx.Scaffold.Cli` inside Ergon.
- `SC-002`: The Ergon release payload contains `ergon.exe` on win-x64 and not `hx.exe` as the product executable.
- `SC-003`: The Ergon local release directories use `<releaseRoot>/ergon/<version>` and `<releaseRoot>/ergon/latest`.
- `SC-004`: The Ergon `release.identity.json` records `src/Ergon.Cli/Ergon.Cli.csproj` and `ergon.exe`.
- `SC-005`: A repo with no release-target declaration fails with a targeted validation diagnostic before running `dotnet publish`.
- `SC-006`: speckit-doti can still run `hx release --repo . --release-root <temp> --json` and produce the `speckit-doti` / `hx.exe` release payload.
- `SC-007`: `doti render-skills --check` reports no drift after guidance updates.
- `SC-008`: The normal and release gates pass after the release-target changes.
- `SC-009`: Updating an older repo with one executable project creates `.doti/release.json` with that project path and executable identity.
- `SC-010`: Updating a repo with an existing `.doti/release.json` preserves it as live repo configuration.

## Key Entities

- **Release target declaration** - Repo-owned tracked configuration that names the product identity and publishable executable to package.
- **Product name** - Human/product identity for release proof and metadata.
- **Package name** - Filesystem-safe identity used in archive names and local release-store directories.
- **Publish project** - Repo-relative project path that `hx release` publishes for a RID.
- **Executable name** - Product executable name placed at the release payload root for the selected RID.
- **Release target proof** - The `LocalReleaseResult`/identity metadata fields showing which repo target was packaged.

## Deterministic Surfaces

- `dotnet run --project tools/Hx.Scaffold.Cli -- release --repo <path> [--rid <rid>] [--release-root <path>] [--release-root-env <name>] [--save-release-root] --json` remains the command-backed release surface.
- A target release declaration is a new command-backed config surface and is planned until implemented.
- `LocalReleaseResult` and `release.identity.json` are the machine-readable proof surfaces for release target identity.
- The doti template/install/update surfaces are responsible for seeding managed default release-target declarations where appropriate.
- README, `.doti/agent-context.md`, and generated skills are documentation surfaces that must match implemented behavior.

## Architecture Impact

- The release implementation must separate the running `hx` command source from the target repo's product release target.
- The release-target declaration belongs to target repo configuration, not the speckit-doti release cache.
- CLI command bodies should remain thin and delegate release-target resolution and packaging to core code.
- Existing path-safety, checksum, staging, identity metadata, and latest-copy protections from `002-local-release-output` remain required.

## Sentrux And Hygiene Impact

- Release target declarations must not contain user-local absolute paths, secrets, or machine-specific cache locations.
- Local release artifacts remain outside source control.
- No Sentrux baseline is created or updated.
- Any generated/template release-target declaration must be deterministic and safe for public repositories.

## Assumptions

- `doti/release.json` or an equivalently scoped tracked JSON file is an acceptable target-owned declaration surface if the implementation plan chooses it.
- Single-CLI generated repos can safely default the release target to their primary CLI project.
- Existing repos upgraded from 0.7.0 may need one tracked release-target declaration added by an agent/operator when update cannot infer exactly one executable project; `hx release` should fail clearly until that exists.
- The default release-root environment remains `DOTI_RELEASE_ROOT` unless the target declaration selects another default.

## Acceptance

Command-backed today:

- `hx release` exists but is known to be incorrectly scaffold-specific.
- `version calculate`, `gate run --profile normal`, `gate run --profile release`, and `doti render-skills --check` are available.

Advisory until implemented:

- Target release declaration loading and validation.
- Generic target executable publishing from a vendored `hx`.
- Template/install/update seeding of release target declarations.
- Ergon proof using its `src/Ergon.Cli/Ergon.Cli.csproj` target.

## Clarifications

### 2026-06-24

- No operator question was needed. The failure was reproduced by inspection: Ergon has `src/Ergon.Cli/Ergon.Cli.csproj` and no `tools/Hx.Scaffold.Cli`, while `LocalReleaseService` hard-codes `dotnet publish tools/Hx.Scaffold.Cli`.
