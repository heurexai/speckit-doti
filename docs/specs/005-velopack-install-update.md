# Spec: Velopack Install and Update

> WHAT and WHY only. This feature changes Doti releases from source archives plus bespoke `hx` update logic into installable, updateable Velopack packages, and makes that install/update capability available to apps created from Doti scaffolds.

## Goal

Doti releases must produce an easy-to-use installer that can install Doti into a fresh directory, detect an existing Doti installation, and update it without relying on Doti's current bespoke `hx` download/extract/update path.

The same install/update capability should become the default scaffold behavior so applications built with Doti scaffolds can ship installable, updateable releases instead of only archives. Doti-related scaffold files must also stop being split between root `doti/` and `.doti/`; installed scaffold repositories should keep Doti-owned files under `.doti/` so the repo surface is cleaner, easier to reason about, and easier to migrate.

## Upstream Evidence

- Velopack describes itself as a cross-platform installation and auto-update framework and supports hosting releases from locations such as GitHub Releases.
- Velopack's .NET guidance covers generic C# apps on Windows, macOS, and Linux, with startup integration through `VelopackApp.Build().Run()`.
- Velopack's .NET guidance shows update detection and application through `UpdateManager`, `CheckForUpdatesAsync`, `DownloadUpdatesAsync`, and `ApplyUpdatesAndRestart`.
- Velopack packages a published app directory with `vpk pack --packId <id> --packVersion <version> --packDir <publish-dir> --mainExe <exe>`.
- Velopack's `--packDir` packaging model means vendored executables and assets can be included when Doti stages them into the package directory before `vpk pack`; Doti release proof must verify those files are present rather than assuming the pack step found them.

## Current Directory Split RCA

The current scaffold has both root `doti/` and hidden `.doti/` because they evolved as two different surfaces:

- Root `doti/` is currently the render/source surface. `doti render-skills` reads `doti/core/skills.json`, `doti/profiles/dotnet-cli/profile.json`, and `doti/core/templates/agent-context-template.md`; `doti install` locates Doti by walking upward until it finds `doti/core/skills.json`.
- Hidden `.doti/` is currently the installed/runtime surface. The cycle engine reads `.doti/workflows/doti/workflow.yml` and writes `.doti/cycle-state.json`; the gate proof store writes `.doti/gate-proof.json`; repo metadata and release configuration live under `.doti/*.json`.
- `doti install` currently copies the full root `doti/` source tree into generated/target repos specifically so installed skills can resolve `doti/core/templates/commands/*.md` and so `doti render-skills` can run inside the generated repo.
- `.doti/templates`, `.doti/memory`, and `.doti/workflows` duplicate a subset of root `doti/core` assets to preserve the installed Spec-Kit-style workflow surface. Most paired files are byte-identical today, but `doti/core/templates/plan-template.md` and `.doti/templates/plan-template.md` already differ, proving the split creates real drift risk.
- Managed-asset scanning and update currently treat both sides as managed: `.doti/workflows` and `doti/core/templates` are workflow/template assets; `doti/core`, `doti/profiles`, `.doti/templates`, `.doti/memory`, and `.doti/integrations` are Doti source assets.

Therefore the desired `.doti/`-only layout is not only cosmetic. It must first move the render/source authority from root `doti/` to `.doti/core/`, then update generated skill references, managed-asset hashing, installation, update migration, and tests. Deleting root `doti/` without moving those authority paths would break `doti render-skills`, `doti install`, generated skill references, and managed asset drift detection.

## Scope

Included behavior:

- Treat this feature as a minor version upgrade when released.
- Use `velopack/velopack` as the install and update mechanism for Doti itself.
- Produce Velopack installer/update artifacts for Doti releases.
- Replace Doti's current bespoke release-asset download/extract/update behavior in `hx.exe` with Velopack-based install/update behavior.
- Remove the `hx update` command surface; Doti and scaffold-generated applications must update only through Velopack installer/update artifacts.
- Keep Doti release artifacts versioned and compatible with GitVersion-backed Doti versions.
- Make `hx release` the command-backed owner of release tag creation and the canonical annotated tag message; release tagging must not remain a separate runner workflow step.
- Allow an agent/operator to declare major or minor release intent through `hx release` arguments, while keeping GitVersion as the version calculation authority.
- Include `hx`, Doti workflow assets, generated skill sources, templates, prerequisite policy, release metadata, and vendored tool assets in the Velopack payload.
- Make Velopack the default release/install/update capability in new Doti scaffolds.
- Ensure generated applications can package their declared product executable through Velopack, not through Doti-specific archive code.
- Move installed Doti-owned scaffold assets into `.doti/`.
- Remove the installed root `doti/` directory from generated and updated scaffold repositories.
- Update generated Codex/Claude skills and root entrypoints so they reference `.doti/core/...` paths instead of `doti/core/...`.
- Provide a migration path for existing Doti-scaffolded repositories that currently contain root `doti/` through the repo-asset install/repair command, not through `hx update`.
- Preserve live repo-owned configuration and baselines during migration.

Excluded behavior:

- Replacing the Doti cycle gate, affected-test planner, Sentrux, Gitleaks, GitVersion, or prerequisite preflight with Velopack.
- Requiring applications built from Doti scaffolds to publish to the Microsoft Store.
- Requiring generated applications to use a GUI update prompt.
- Requiring package-manager publication such as winget, Homebrew, Scoop, deb, or rpm in this feature.
- Retaining `Hx.Scaffold.Cli update --repo <path>` or any equivalent `hx update` repository/self-update path.
- Using Velopack to rewrite repository-managed Doti workflow files inside a Git checkout; Velopack updates installed executables and packaged payloads, while `doti install --repo <path> --agents <agents> [--force] --json` owns repo-asset install, repair, and legacy `doti/` migration from the already-installed Doti package.
- Retaining any runner-side standalone major/minor version-tagging command as the release-lane version/tagging surface.
- Letting Doti calculate the next semantic version by manually incrementing parsed major/minor/patch values outside GitVersion.
- Letting agents create release tags manually with raw `git tag`.
- Moving non-Doti product source, tests, docs, `.sentrux` baselines, or app-owned release configuration into `.doti/`.

## Functional Requirements

- `FR-001`: Doti releases MUST produce Velopack installer/update artifacts for every supported release RID.
- `FR-002`: A Doti Velopack installer MUST install into a fresh machine/user installation target without requiring a Git checkout.
- `FR-003`: A Doti Velopack installer or updater MUST detect an existing Doti installation and update it to the release version.
- `FR-004`: An installed Doti `hx --version` MUST report the Velopack release version after fresh install and after update.
- `FR-005`: Doti release proof MUST verify that the Velopack payload contains the `hx` executable, Doti workflow assets, Doti core templates/skills source, prerequisite policy, release metadata, and supported vendored tool assets.
- `FR-006`: Doti release proof MUST fail if the Velopack payload only contains a source archive without install/update metadata.
- `FR-007`: `hx.exe` MUST NOT contain bespoke Doti self-install or self-update logic that downloads, extracts, prunes, or delegates to GitHub release archives outside Velopack.
- `FR-008`: Doti's installed update check MUST use Velopack update metadata and MUST NOT use the existing `hx update` release-cache protocol for updating the installed Doti tool.
- `FR-009`: New Doti scaffolds MUST include Velopack as the default release packaging and update mechanism for the generated product executable.
- `FR-010`: A generated scaffold's release manifest MUST declare the Velopack package identity, release channel/update source, main executable, version, RID targets, and any local release-root behavior needed for deterministic release proof.
- `FR-011`: Generated app release proof MUST produce Velopack installer/update artifacts for the app's declared product executable.
- `FR-012`: Generated app release proof MUST verify install/update behavior at least for the active supported RID before reporting release readiness.
- `FR-013`: New generated scaffold repositories MUST NOT contain a root `doti/` directory.
- `FR-014`: New generated scaffold repositories MUST place Doti-owned source/configuration/assets under `.doti/`.
- `FR-015`: Generated Codex/Claude skills and root entrypoints MUST reference `.doti/core/...` paths for command templates and Doti source assets.
- `FR-016`: `doti render-skills --check` MUST fail if generated skills or entrypoints still reference root `doti/core/...` in installed scaffold repositories.
- `FR-017`: Existing scaffold repositories updated from older Doti versions MUST migrate Doti-owned root `doti/` content into `.doti/` or remove it after equivalent `.doti/` content is installed.
- `FR-018`: Existing scaffold repository migration MUST preserve live repo-owned configuration, including `.doti/release.json`, prerequisite policy overrides when explicitly repo-owned, `.sentrux` baselines, cycle state, gate proof, and product docs/source.
- `FR-019`: Existing scaffold repository migration MUST fail hard if root `doti/` contains user modifications that cannot be proven equivalent to generated Doti-owned assets, unless the operator uses an explicit force path.
- `FR-020`: Existing scaffold repository migration MUST report every removed, moved, replaced, preserved, and blocked Doti path in machine-readable JSON.
- `FR-039`: `doti install --repo <path> --agents <agents> [--force] --json` MUST be the deterministic repo-asset install/repair/migration command for managed Doti assets after `hx update` is removed; it MUST NOT download release assets, prune release caches, replace installed executables, or update Doti itself.
- `FR-040`: `Hx.Scaffold.Cli new` and any generated-repo finishing flow MUST use the same Doti install/repair/migration core service as `doti install`, so new installs and legacy migrations share one managed-asset classifier and JSON report contract.
- `FR-021`: Doti's version command MUST report Velopack install/update identity when run from an installed Doti tool.
- `FR-022`: Doti release documentation and CLI help MUST describe Velopack installer artifacts as the primary user-facing release output.
- `FR-023`: Raw archives MAY remain as fallback/debug artifacts, but MUST NOT be described as the primary Doti install/update path.
- `FR-024`: The Doti gate MUST include a deterministic check that Velopack packaging metadata and generated scaffold release metadata are current.
- `FR-025`: The release of this feature MUST be treated as a minor version upgrade and MUST use `hx release --minor` as the command-backed release intent and tagging path.
- `FR-026`: The `hx update` command MUST be removed from the CLI command tree, help, machine-readable `describe --json` output, docs, tests, generated skills, and agent context.
- `FR-027`: Doti release packaging MUST stage all required vendored executables and assets into the Velopack package directory before `vpk pack`.
- `FR-028`: Doti release proof MUST verify the packaged Velopack payload includes every required vendored executable and asset with the expected hash or declared identity.
- `FR-029`: Generated scaffold release proof MUST verify app-specific vendored executables and assets are staged into and present in the generated app's Velopack payload.
- `FR-030`: `hx release` MUST accept a release intent option that lets an agent choose a minor or major release; patch release intent MUST remain available as the default or an explicit option.
- `FR-031`: `hx release` MUST use GitVersion as the sole version calculation authority for the release version and MUST NOT calculate the next version by applying Doti-owned major/minor/patch arithmetic to the previous version.
- `FR-032`: `hx release` MUST reconcile the requested release intent with the GitVersion-calculated version before creating any tag or installer artifacts; if the calculated version does not match the requested major/minor/patch intent, the command MUST fail with a diagnostic that names the requested intent, the GitVersion-calculated version, the previous release tag/version, and the expected GitVersion increment signal or configuration change.
- `FR-033`: `hx release` MUST create the release tag itself after the release gate passes, using the canonical tag name `v<GitVersion MajorMinorPatch>` on the exact committed `HEAD` being released.
- `FR-034`: `hx release` MUST create an annotated tag with a deterministic Doti-generated tag message/comment that records at least the release version, release intent, source commit, package/product name, command identity, and GitVersion identity.
- `FR-035`: `hx release` MUST be idempotent when the expected tag already exists on the same commit with matching Doti release identity, and MUST fail hard when the tag exists on a different commit or with a mismatched release identity.
- `FR-036`: `hx release` MUST rerun or verify GitVersion after tag creation and MUST fail if the tagged commit does not report the exact public release version used for Velopack packaging.
- `FR-037`: Release proof and `release.identity.json` MUST include the release intent, tag name, tag object/commit identity, GitVersion-calculated version, and Velopack package version.
- `FR-038`: `version calculate` MAY remain as a read-only diagnostic surface, but the existing runner-side standalone major/minor version-tagging command MUST be deleted from CLI source, docs, generated skills, agent context, and machine-readable capability output.

## Success Criteria

- `SC-001`: A Doti release produces a Velopack installer artifact and update metadata for `win-x64`.
- `SC-002`: A Doti release produces Velopack-compatible artifacts for Linux and macOS RIDs where Velopack support is validated; unsupported RIDs fail closed or are explicitly marked advisory.
- `SC-003`: Installing Doti into a clean target through the produced installer makes `hx --version` return the release version.
- `SC-004`: Updating an older Doti install through Velopack makes `hx --version` return the newer release version without requiring a Git checkout.
- `SC-005`: Release proof fails if the produced artifact is only a source zip/tarball.
- `SC-006`: A new scaffolded app can produce a Velopack installer/update artifact for its product executable through the Doti release command.
- `SC-007`: A newly scaffolded repository contains `.doti/` Doti assets and no root `doti/` directory.
- `SC-008`: `doti render-skills --check` passes in a newly scaffolded repository whose generated skills reference `.doti/core/...`.
- `SC-009`: Updating an older scaffolded repository removes or migrates root `doti/` when the directory is unmodified generated Doti content.
- `SC-010`: Updating an older scaffolded repository fails with specific path diagnostics when root `doti/` contains modified generated content and `--force` is not supplied.
- `SC-011`: Live repo configuration and baselines survive scaffold migration byte-for-byte unless they are explicitly regenerated by a documented command-backed step.
- `SC-021`: Running `doti install --repo <legacy-repo> --agents codex,claude --json` against an older scaffolded repository migrates unmodified managed `doti/` content to `.doti/`, removes the obsolete root `doti/` tree, and reports every action without changing the installed Doti executable.
- `SC-012`: Release docs and help show a non-coder operator how to install fresh, update existing, and verify the installed version.
- `SC-013`: Release readiness proof for this feature shows a minor-version release identity rather than a patch-only release identity.
- `SC-014`: `hx --help`, subcommand help, and `describe --json` no longer expose an `update` command.
- `SC-015`: A Doti Velopack package inspection shows `hx`, `.doti/` assets, and vendored tool executables/assets are present in the package payload.
- `SC-016`: A generated app Velopack package inspection shows the app executable and app-declared vendored assets are present in the package payload.
- `SC-017`: Running `hx release --minor --repo . --json` for this feature creates a Doti-owned annotated `v<version>` tag on the release commit and the tag message records `Release-Intent: minor`.
- `SC-018`: A release attempt whose requested `--major` or `--minor` intent does not match the GitVersion-calculated version fails before tag creation and before Velopack packaging.
- `SC-019`: Re-running `hx release` for an already-tagged commit is accepted only when the existing tag points to the same commit and carries the same Doti release identity.
- `SC-020`: `doti-release` guidance, README release docs, and generated agent context describe `hx release --major|--minor` as the release tagging path and no longer instruct agents to run a separate version-tagging command.

## Key Entities

- **Doti Velopack Package**: The installable Doti release containing `hx`, `.doti/` workflow/core assets, release metadata, prerequisite policy, and supported vendored tool assets.
- **Generated App Velopack Package**: The installable release package for an application created from a Doti scaffold.
- **Doti Install Identity**: Version, channel/source, package id, RID, installed executable path, update source, and payload hash metadata used to prove what is installed.
- **Doti-Owned Scaffold Asset**: Any generated or managed Doti asset currently installed under `.doti/`, `.agents/`, `.claude/`, root entrypoints, or legacy root `doti/`.
- **Doti Repo-Asset Install/Repair**: The command-backed `doti install --repo <path> --agents <agents> [--force] --json` operation that writes or repairs managed Doti workflow assets in a Git checkout from the already-installed Doti package. It is intentionally not an updater for the installed `hx`/Doti executable.
- **Release Intent**: The operator/agent-selected semantic release class (`major`, `minor`, or `patch`) passed to `hx release`, reconciled with GitVersion before tag creation.
- **Doti Release Tag**: The canonical annotated git tag created by `hx release` using `v<GitVersion MajorMinorPatch>` and a deterministic Doti-generated tag message/comment.
- **Live Repo Configuration**: Repo-owned state that must be preserved through migration, including `.doti/release.json`, cycle/gate state, baselines, docs, source, and product configuration.

## Release Tagging Process

The release process must be command-backed and GitVersion-led:

1. The agent/operator runs `hx release --repo <path>` for a patch release, `hx release --minor --repo <path>` for a minor release, or `hx release --major --repo <path>` for a major release. Mutually exclusive release-intent options are required for major/minor and patch remains the default or explicit safe option.
2. `hx release` validates that the release commit is the exact committed `HEAD`, the repository is in a releasable state, and the release gate has passed for that change set.
3. `hx release` invokes GitVersion to calculate the candidate release version. GitVersion configuration, tags, branch rules, and supported commit/merge-message increment signals are the version authority; Doti must not manually increment the parsed version.
4. `hx release` compares the requested release intent with the GitVersion-calculated candidate relative to the latest reachable release tag. A mismatch fails before tag creation, with a specific diagnostic that tells the agent whether the GitVersion increment signal/configuration is missing or inconsistent.
5. `hx release` creates the annotated release tag on the exact release commit using `v<GitVersion MajorMinorPatch>`. The tag message/comment is generated by Doti and must be stable, for example:

   ```text
   Release v<version>

   Release-Intent: <major|minor|patch>
   Release-Command: hx release
   Release-Target: <packageName>
   Source-Commit: <full SHA>
   GitVersion-Version: <GitVersion tool version>
   ```

6. `hx release` verifies the tag after creation, reruns or verifies GitVersion on the tagged commit, then builds Velopack release artifacts using the same version and source commit.
7. The release result reports the local tag, release intent, version, source commit, Velopack artifacts, and local release output. `hx release` MUST NOT silently push tags or mutate remotes; it MUST report the exact tag push command/action needed for GitHub CI when remote publication is required.
8. `/doti-release` owns the remote-publication step after local release proof: it pushes the verified Doti release tag to the configured remote, verifies the GitHub workflow run and release artifacts, and reports any CI/release failure separately from local packaging success.

This replaces the current separate runner-side major/minor release-tagging step. The only remaining version command required for release planning is read-only version calculation/diagnostics.

## Deterministic Surfaces

Command-backed or planned deterministic surfaces:

- `Hx.Scaffold.Cli release --repo <path> --json`: MUST produce or report Velopack installer/update artifacts instead of only archive/checksum output.
- `Hx.Scaffold.Cli release --repo <path> [--major|--minor|--patch] --json`: MUST own release intent, GitVersion reconciliation, canonical annotated tag creation, tag verification, and Velopack artifact production.
- `Hx.Runner.Cli gate run --profile release --repo <path> --json`: MUST include Velopack packaging and metadata checks in release proof.
- `Hx.Scaffold.Cli version --repo <path> --json`: MUST report Velopack install/update identity when available.
- `Hx.Scaffold.Cli new --name <name> --output <path> --json`: MUST generate the `.doti/`-only Doti layout and default Velopack release capability.
- `Hx.Runner.Cli doti install --repo <path> --agents <agents> [--force] --json`: MUST install, repair, and migrate repo-managed Doti assets from the already-installed Doti package, including legacy root `doti/` migration. It MUST NOT update installed executables or replace Velopack's product update role.
- `doti render-skills --check`: MUST validate `.doti/core/...` generated references and fail closed on root `doti/core/...` drift.
- Existing `Hx.Scaffold.Cli update --repo <path> --json`: MUST be removed; this feature resolves update ownership to Velopack only.
- Existing runner-side standalone major/minor version-tagging command: MUST be removed from source, CLI command registration, help, `describe --json`, docs, generated skills, and agent context; `hx release` is the only tagging surface.

## Architecture, Sentrux, And Hygiene Impact

- The release architecture changes from archive assembly to installer/update artifact production.
- Release versioning changes from a separate Doti-owned version/tag command to a GitVersion-led `hx release` flow that performs intent validation and tag creation in one command.
- Velopack must be isolated behind Doti release/install/update abstractions so generated app packaging can reuse it without leaking Doti-specific behavior into product code.
- The root `doti/` removal changes generated-file boundaries, managed-asset hashing, skill rendering, and update migration.
- Removing `hx update` simplifies `hx` to scaffold/release/version/prerequisite/workflow duties while Velopack owns product installation and updates.
- Repo-asset migration belongs in the Doti install/repair core service shared by `doti install` and scaffold finishing, not in release packaging, not in `hx update`, and not in Velopack's executable update flow.
- Sentrux should treat Velopack packaging and `.doti/` migration code as product code subject to the normal structural gate.
- Hygiene checks must scan Velopack package metadata and generated installer/update configuration for accidental local paths, secrets, and unstable machine-specific values.

## Assumptions

- Velopack can support the required Doti CLI installation and update UX for at least Windows, with Linux/macOS support validated before those RIDs become enforced release targets.
- Doti can continue publishing release artifacts to GitHub Releases while using Velopack metadata for install/update detection.
- GitVersion can be configured so major/minor intent expressed for a release is observable to `hx release` before tag creation; if the repo lacks that signal, `hx release --major|--minor` fails rather than inventing a version.
- Generated apps should receive Velopack release capability by default, while future work may add an opt-out if a scaffold owner needs a different installer strategy.
- Raw archive artifacts may remain useful for diagnostics or CI, but they are not the primary install/update experience.

## Dependencies

- Velopack package/tool version must be pinned and verified in the Doti dependency model before release proof treats it as deterministic.
- GitVersion configuration and Doti release docs must define the supported major/minor/patch release-intent signals that `hx release` validates.
- GitHub release workflow must be updated to upload Velopack installer/update artifacts and metadata.
- `/doti-release` guidance must define the post-`hx release` remote-publication step: push the verified release tag, watch the GitHub CI/release workflow, and verify uploaded Velopack artifacts.
- Existing generated skills and templates must be migrated to `.doti/core/...` references before root `doti/` can be removed safely.

## Clarifications

### 2026-06-25

- The operator explicitly selected `velopack/velopack` as the install/update mechanism for Doti itself and, if successful, as the default scaffold install/update mechanism.
- The operator explicitly requested removing Doti's existing install/update code from `hx.exe`.
- The operator explicitly requested removing the installed root `doti/` directory and placing Doti-related installed scaffold files under `.doti/`.
- The operator explicitly stated this feature is a minor version upgrade when released.
- The operator explicitly resolved the update ownership decision: `hx update` should disappear, and all Doti/scaffold-generated product updates should occur only through Velopack installer/update artifacts.
- Velopack is considered suitable for vendored executables and assets only when Doti stages those files into the Velopack package directory and proves their presence/hash in release proof.
