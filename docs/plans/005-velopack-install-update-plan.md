# Plan: Velopack Install and Update

> Spec: `docs/specs/005-velopack-install-update.md`.

## Technical Context

This feature changes the release/update contract in four connected areas:

- Doti release artifacts move from source archives plus bespoke `hx update` behavior to Velopack installer/update artifacts.
- `hx release` becomes the sole release-tagging surface. GitVersion calculates the version; `hx release --major|--minor|--patch` validates release intent, creates the canonical annotated tag, verifies the tag/version, and then packages the release.
- Installed Doti assets move from split `doti/` plus `.doti/` surfaces to `.doti/` only.
- Generated scaffold apps inherit Velopack release packaging by default.

Stack and constraints: .NET 10, System.CommandLine, existing `CliResult` envelope, existing `Hx.Scaffold.Core.Release` release service, existing `Hx.Version.Core` GitVersion wrapper, Velopack NuGet package and pinned `vpk` tool invocation, GitVersion 6.7.0, no shell runners, no hand-written version arithmetic, no raw `git tag` by agents, no `hx update`.

Command-backed today:

- `Hx.Scaffold.Cli release --repo <path> ... --json` exists, but currently produces archive/checksum/identity output and does not create tags or Velopack packages.
- `Hx.Runner.Cli version calculate --repo <path> --json` exists and is read-only.
- A runner-side standalone major/minor version-tagging command exists today and must be removed.
- `Hx.Scaffold.Cli update --repo <path> ... --json` exists today and must be removed.
- `doti render-skills --check`, `gate run --profile release`, `architecture test`, Sentrux, Gitleaks, and affected-test proof surfaces already exist.

Planned and absent:

- `hx release --major|--minor|--patch` release-intent validation and tag creation.
- Velopack package staging, `vpk pack`, payload inspection, and update metadata proof.
- `.doti/core` render/source authority and migration from root `doti/`.
- Velopack packaging proof in the release gate.

## Constitution Check (Before Design)

PASS.

- **Deterministic Ownership:** release tags, package metadata, payload checks, and update ownership move into .NET commands.
- **Bootstrap Honesty:** current archive release and existing update/version-tagging surfaces are marked as current behavior, not claimed as final proof.
- **Template Boundary:** template layout changes remain in scaffold/template assets; dynamic release, migration, and verification stay in .NET core services.
- **Public Hygiene:** Velopack packages and vendored binaries are release artifacts or pinned manifests, not committed local binaries.
- **Cross-Platform Rule:** no PowerShell or Bash runners are introduced; platform-specific work is through .NET and external pinned tools.
- **Codified Cycle:** implementation must continue through Doti stamps/gates and `doti cycle commit`.
- **Engineering Discipline:** versioning follows GitVersion semantics; Doti does not manually invent semantic versions.
- **Channel Independence:** CLI remains parse/delegate/render; release and migration logic lives in core libraries.

## Research

- **Decision:** Use Velopack's .NET integration for Doti and generated app installer/update packages.
  **Rationale:** Velopack documents C# generic app support across Windows, macOS, and Linux, with `VelopackApp.Build().Run()` for startup integration and `UpdateManager` APIs for update checks. It packages from a published app directory via `vpk pack --packId ... --packVersion ... --packDir ... --mainExe ...`.
  **Alternatives rejected:** continuing raw zip/tar release archives as the primary install/update path; keeping bespoke `hx update`.

- **Decision:** Stage every vendored executable and Doti asset into the Velopack `packDir` before `vpk pack`, then inspect/prove the staged payload.
  **Rationale:** Velopack packages what is in the published directory; Doti must make vendored payload inclusion deterministic instead of assuming the packer discovers it.
  **Alternatives rejected:** relying on post-package manual inspection only; relying on source archives to carry vendored assets.

- **Decision:** Keep GitVersion as the version authority and move release intent/tagging into `hx release`.
  **Rationale:** GitVersion documentation says GitVersion works out the next SemVer from history/config and supports commit-message increment signals such as `+semver: major`, `+semver: minor`, and `+semver: patch`; tags then become version sources. Doti's current `NextVersion` arithmetic duplicates and can contradict that model.
  **Alternatives rejected:** runner-side standalone major/minor version-tagging; Doti-owned parsing plus manual major/minor arithmetic; manual raw `git tag`.

- **Decision:** Codify GitVersion increment signals in `GitVersion.yml`.
  **Rationale:** Defaults may work, but Doti needs a repo-visible contract that `hx release --major|--minor|--patch` can validate against.
  **Alternatives rejected:** depending on undocumented defaults; letting the release command silently accept intent that GitVersion cannot see.

- **Decision:** Remove `hx update` rather than preserve a compatibility shim.
  **Rationale:** Velopack becomes the single update mechanism. A shim would keep two update concepts alive and confuse agents/non-coders.
  **Alternatives rejected:** hidden `hx update` alias; warning-only deprecation.

- **Decision:** Make `doti install --repo <path> --agents <agents> [--force] --json` the retained repo-asset install/repair/migration surface after `hx update` is removed.
  **Rationale:** Velopack updates installed executables and packaged products; it does not rewrite managed workflow assets inside arbitrary Git checkouts. The existing Doti install command already owns repo asset installation, rendered skills, entrypoints, and hook arming, so legacy root `doti/` migration belongs in the same Doti install core service rather than in release packaging or a second update command.
  **Alternatives rejected:** keeping `hx update`; hiding migration inside `hx release`; relying on Velopack to mutate repository checkouts; adding a second repo-migration command with a separate classifier.

- **Decision:** Move Doti source authority to `.doti/core` before deleting root `doti/`.
  **Rationale:** current renderer and installer resolve `doti/core/skills.json`, `doti/profiles/dotnet-cli/profile.json`, and `doti/core/templates/agent-context-template.md`; deleting root `doti/` first would break rendering and installed skill references.
  **Alternatives rejected:** deleting root `doti/` without relocation; continuing dual source surfaces.

## Design

### Phase 1: Release Intent And Tagging

Files likely to change:

- `tools/Hx.Scaffold.Cli/ScaffoldCommandFactory.cs`
- `tools/Hx.Scaffold.Cli/ScaffoldCommands.Release.cs`
- `src/Hx.Scaffold.Core/Release/LocalReleaseService.cs`
- new `src/Hx.Scaffold.Core/Release/ReleaseTagService.cs`
- `tools/Hx.Version.Core/GitVersionTool.Calculate.cs`
- `tools/Hx.Tooling.Contracts/LocalReleaseResult.cs`
- `tools/Hx.Tooling.Contracts/VersionResult.cs` or new release/tag result contracts
- `GitVersion.yml`
- `errorcodes/registry.json`
- tests under `test/Hx.Scaffold.Tests` and `test/Hx.Runner.Tests`

Approach:

- Add mutually exclusive `--major`, `--minor`, and optional explicit `--patch` to `hx release`; no flag means patch.
- Add `ReleaseIntent` to `LocalReleaseRequest` and the release result.
- Extend `GitVersionTool.Calculate` result parsing to expose enough data for release validation: `MajorMinorPatch`, tool version/source, and the calculated version before and after tag verification.
- Add GitVersion config for supported release intent signals:
  - `+semver: major` / `+semver: breaking`
  - `+semver: minor` / `+semver: feature`
  - `+semver: patch` / `+semver: fix`
- Validate release intent against the latest reachable SemVer tag and GitVersion-calculated `MajorMinorPatch`. Fail before mutation if intent and version do not agree.
- Create the annotated tag `v<MajorMinorPatch>` from `hx release`, not the runner. The tag message is deterministic:

  ```text
  Release v<version>

  Release-Intent: <major|minor|patch>
  Release-Command: hx release
  Release-Target: <packageName>
  Source-Commit: <full SHA>
  GitVersion-Version: <tool version>
  ```

- Verify an existing tag is idempotent only when it points to the same commit and carries the same Doti release identity. Any other existing tag state is a hard validation/integrity failure.
- Verify GitVersion on the tagged commit returns the same public version before package output is accepted.
- Include tag name, source commit, tag object/commit identity, release intent, GitVersion identity, and package version in `LocalReleaseResult` and `release.identity.json`.
- Do not push the tag from `hx release`. Return the exact tag-push action needed for remote publication; `/doti-release` pushes the verified tag, watches GitHub CI/release, and verifies uploaded Velopack artifacts.

### Phase 2: Remove Old Version And Update Surfaces

Files likely to change:

- `tools/Hx.Runner.Cli/RunnerCommandFactory.Gates.cs`
- `tools/Hx.Runner.Cli/RunnerCommands.Gates.cs`
- `tools/Hx.Version.Core/GitVersionTool.Calculate.cs`
- `test/Hx.Runner.Tests/RunnerCommandsTests.cs`
- `test/Hx.Runner.Tests/VersionTests.cs`
- `tools/Hx.Scaffold.Cli/ScaffoldCommandFactory.cs`
- `tools/Hx.Scaffold.Cli/ScaffoldCommands.Update.cs`
- `src/Hx.Scaffold.Core/Update/*`
- `test/Hx.Scaffold.Tests/ScaffoldUpdateCommandTests.cs`
- `doti/profiles/dotnet-cli/profile.json`
- `doti/core/templates/agent-context-template.md`
- `doti/core/templates/commands/doti-release.md`
- `README.md`, `CHANGELOG.md`, and release docs

Approach:

- Delete the runner-side major/minor version-tagging command registration, command implementation, `GitVersionTool.Bump`, `NextVersion`, and tests that validate Doti-owned arithmetic.
- Keep only read-only `version calculate`.
- Delete `hx update` from command registration, help, `describe --json`, tests, generated skills, profile command list, and agent context.
- Move legacy root `doti/` repo-asset migration into the Doti install core service used by `doti install --repo <path> --agents <agents> [--force] --json` and scaffold finishing; keep it out of `hx release` and Velopack executable update flow.
- Remove or quarantine update implementation only after every non-update behavior it owns today has either moved into Doti install/repair migration or is explicitly deleted as obsolete.
- Add a proof check for removed command identifiers and flags: `rg "VersionBump|AddVersionBump|NextVersion|bump --major|bump --minor"` must have no live source, live docs, generated skills, or agent-context hits. Historical docs should either be updated to the new release path or explicitly marked superseded; no current guidance may mention the removed command.
- Add a proof check: `hx describe --json`, `hx --help`, and nested help do not expose `update`; runner `describe --json` does not expose a version-tagging subcommand.

### Phase 3: Velopack Release Packaging

Files likely to change:

- `Directory.Packages.props`
- Doti CLI project entry/startup files
- generated template CLI project files under `scaffold/templates/dotnet-cli`
- `src/Hx.Scaffold.Core/Release/*`
- new `src/Hx.Scaffold.Core/Release/Velopack*` types
- `tools/Hx.Tooling.Contracts/LocalReleaseResult.cs`
- `.github/workflows/release.yml`
- `.github/workflows/store-release.yml` if retained for Store-only path
- `rules/hygiene.json`
- release tests under `test/Hx.Scaffold.Tests`

Approach:

- Add pinned `Velopack` package and pinned `vpk` tool/version metadata.
- Add Velopack bootstrap call to Doti's `hx` entrypoint and generated scaffold product entrypoints where appropriate.
- Publish the manifest-declared target project to a deterministic pack directory.
- Stage `.doti/`, `hx`, templates/skills source, prerequisite policy, release metadata, and vendored tool assets into the pack directory.
- Run pinned `vpk pack` with deterministic `packId`, `packVersion`, `packDir`, and `mainExe` from `.doti/release.json`.
- Inspect the pack directory and resulting Velopack output before reporting success.
- Keep raw archives only as optional diagnostics, not as primary install/update proof.
- Update GitHub release workflow to upload Velopack artifacts/update metadata instead of only source archives.

### Phase 4: `.doti`-Only Source Authority

Files likely to change:

- `tools/Hx.Doti.Core/DotiRenderer.cs`
- `tools/Hx.Doti.Core/DotiInstaller.cs`
- `tools/Hx.Doti.Core/ManagedAssets/*`
- `src/Hx.Scaffold.Core/ScaffoldDotiInstaller.cs`
- `src/Hx.Scaffold.Core/Versioning/*`
- `doti/core/*` moved to `.doti/core/*`
- `doti/profiles/*` moved to `.doti/profiles/*`
- generated skill templates and root entrypoints
- `.gitignore`
- template files under `scaffold/templates/dotnet-cli`
- tests under `test/Hx.Doti.Tests`, `test/Hx.Scaffold.Tests`, and `test/Hx.Templates.Tests`

Approach:

- Move render/source authority from `doti/core` and `doti/profiles` to `.doti/core` and `.doti/profiles`.
- Update `DotiRenderer` constants and all generated references.
- Update `DotiInstaller` to copy `.doti/core`, `.doti/profiles`, workflows, memory, integrations, templates, prerequisite policy, and rendered files without creating root `doti/`.
- Update managed-asset scanner/baseline categories to treat `.doti/core`, `.doti/profiles`, `.doti/templates`, `.doti/memory`, `.doti/integrations`, and `.doti/workflows` as managed Doti assets.
- Add migration behavior to the Doti install/repair service that removes root `doti/` only when equivalent generated content is proven unmodified. Modified content blocks unless force is explicit.
- Preserve `.doti/release.json`, prerequisite policy overrides that are repo-owned, cycle/gate state, `.sentrux` baselines, product source/docs/config, and any other live repo-owned state.
- Emit a machine-readable install/repair/migration report from `doti install` with removed, moved, replaced, preserved, and blocked path lists. The same report contract is used when scaffold finishing invokes the service.
- Add `doti render-skills --check` failure tests for any installed generated file that references root `doti/core`.

### Phase 5: Generated Scaffold Default

Files likely to change:

- `scaffold/templates/dotnet-cli/**`
- `scaffold/Hx.Scaffold.Templates.csproj`
- template golden and round-trip tests
- generated `.doti/release.json` defaults
- generated README/agent context

Approach:

- Generate `.doti`-only Doti assets in new repos.
- Generate release metadata that includes Velopack package id, channel/update source, main executable, RID targets, and local release-root behavior.
- Ensure generated product release uses the same `hx release` path and produces Velopack artifacts for the product executable.
- Validate generated app package payload includes product exe and app-declared vendored assets.

### Phase 6: Gates, Docs, And Proof

Files likely to change:

- `tools/Hx.Gate.Core/GateRunner.cs`
- `tools/Hx.Tooling.Contracts/GateProof.cs` or related release proof contracts
- `doti/core/templates/commands/doti-release.md`
- `doti/profiles/dotnet-cli/profile.json`
- `.agents/skills/*` and `.doti/agent-context.md` through `doti render-skills`
- `README.md`, `CHANGELOG.md`, `packaging/*`, GitHub workflow docs

Approach:

- Add release-gate proof for Velopack metadata freshness, package payload inspection, vendored asset presence/hash, tag identity, GitVersion identity, and generated scaffold release metadata.
- Update `doti-release` guidance to run `hx release --major|--minor|--patch` as the release path and never instruct a separate version-tagging command.
- Re-render generated skills and agent context from the source manifest.
- Update README/help so non-coder operators see installer/update flow as the primary output.

## Architecture Delta

No new ArchUnitNET family is required if implementation keeps this shape:

- Release/tagging/Velopack orchestration lives in `src/Hx.Scaffold.Core.Release`.
- GitVersion calculation remains in `tools/Hx.Version.Core`.
- Doti render/install/migration logic remains in `tools/Hx.Doti.Core`.
- CLI files in `tools/Hx.Scaffold.Cli` and `tools/Hx.Runner.Cli` only parse options, delegate to core services, and render `CliResult`.

The existing `rules/architecture.json` families (`cliSurfaceConfinement`, `cliDelegation`) cover the thin-CLI requirement. The existing `.sentrux/rules.toml` layers already include `src/Hx.Scaffold.Core/*`, `tools/Hx.Doti.Core/*`, `tools/Hx.Version.Core/*`, `tools/Hx.Cli.Kernel/*`, and CLI projects. No layer rule change is needed unless implementation introduces a new project. If a new project is introduced, add it to the appropriate Sentrux layer and update any matching architecture/template tests in the same change.

Generated scaffold architecture rules should not need a new family if generated app code only bootstraps Velopack in the product entrypoint and Doti tooling owns packaging. If generated apps include update-check services, keep them out of the domain library and update the template architecture/Sentrux fixtures consistently.

## CLI Surface And Error Contract

### `hx release`

Command:

```text
hx release --repo <path> [--rid <rid>] [--release-root <path>] [--release-root-env <name>] [--save-release-root] [--major|--minor|--patch] [--json]
```

Exit classes:

- `Success`: release intent validated, tag identity verified/created idempotently, Velopack artifacts produced or local copy explicitly skipped for missing release root.
- `Usage`: mutually exclusive release intent flags, invalid environment variable, invalid option combination.
- `Validation`: release gate missing/stale/failing, GitVersion intent mismatch, dirty/uncommitted release state, unsafe paths, missing Velopack metadata, tag conflict, publish/package failure.
- `Integrity`: vendored asset hash mismatch, Velopack payload hash/identity mismatch, existing tag identity mismatch where the artifact claims a different source/version.
- `Internal`: unexpected process or serialization failure.

New or revised error-code suffixes to add to `errorcodes/registry.json`:

- `release-intent-mismatch` (`validation`)
- `release-gate-proof-missing` (`validation`)
- `release-gate-proof-stale` (`validation`)
- `release-tag-conflict` (`validation` or `integrity`, choose integrity if an existing tag identity is actively inconsistent)
- `release-tag-verification-failed` (`integrity`)
- `velopack-package-failed` (`validation`)
- `velopack-payload-missing` (`integrity`)
- `velopack-payload-hash-mismatch` (`integrity`)

Envelope:

- Extend `LocalReleaseResult` additively with `ReleaseIntent`, `TagName`, `TagCommit`, `TagObject`, `GitVersion`, `VelopackArtifacts`, `VelopackUpdateMetadata`, and staged payload checks.
- Include a remote-publication action in `LocalReleaseResult`, such as the verified tag name and expected remote push action, but do not push remotes from `hx release`.
- Continue returning the standard `CliResult` envelope.
- `describe --json` must expose the new `--major|--minor|--patch` release options.

### `doti install`

Command:

```text
doti install --repo <path> --agents <agents> [--force] [--json]
```

Contract:

- Installs, repairs, and migrates managed Doti workflow assets in a Git checkout from the already-installed Doti package.
- Migrates legacy root `doti/` to `.doti/` only through the managed-asset classifier and canonical hash baseline.
- Preserves live repo-owned configuration and baselines.
- Reports removed, moved, replaced, preserved, and blocked paths in JSON.
- Does not download release assets, prune release caches, update installed executables, or replace Velopack's install/update responsibility.

### Removed Surfaces

- Remove runner-side standalone major/minor version-tagging command from CLI registration, command implementation, help, `describe --json`, tests, docs, generated skills, and agent context.
- Remove `hx update` from CLI registration, command implementation, help, `describe --json`, tests, docs, generated skills, and agent context.

## Deterministic Surfaces And Command Availability

| Area | Command | Status |
| --- | --- | --- |
| Restore | `dotnet restore .\scaffold-dotnet.slnx` | implemented |
| Build | `dotnet build .\scaffold-dotnet.slnx -c Release --no-restore /m:1` | implemented |
| Test | `dotnet test .\scaffold-dotnet.slnx -c Release --no-build /m:1` | implemented |
| Release gate | `dotnet run --project tools/Hx.Runner.Cli -- gate run --repo . --profile release --json` | implemented; must gain Velopack/tag checks |
| Release | `dotnet run --project tools/Hx.Scaffold.Cli -- release --repo . [--major|--minor|--patch] --json` | command exists; intent/tag/Velopack behavior planned |
| Version calculate | `dotnet run --project tools/Hx.Runner.Cli -- version calculate --repo . --json` | implemented; remains read-only |
| Version tagging command | runner-side standalone major/minor version-tagging command | implemented today; must be removed |
| Update | `dotnet run --project tools/Hx.Scaffold.Cli -- update --repo . --json` | implemented today; must be removed |
| Repo asset install/repair/migration | `dotnet run --project tools/Hx.Runner.Cli -- doti install --repo . --agents codex,claude --json` | implemented today; must gain `.doti`-only migration/report behavior |
| Skill render | `dotnet run --project tools/Hx.Runner.Cli -- doti render-skills --repo . --agents codex,claude --check --json` | implemented; must move to `.doti/core` authority |
| Velopack packaging | pinned `vpk pack` through .NET release service | planned |

## Constitution Re-Check (After Design)

PASS.

The design keeps mutable release identity inside `hx release`, keeps GitVersion authoritative, removes duplicate update/tagging surfaces, keeps all new behavior in core libraries, preserves template boundaries, and names proof commands for every planned deterministic surface. No justified constitution violation is needed.

## Complexity Tracking

None.

## Verification Plan

- Unit tests:
  - release intent option validation;
  - GitVersion version-vs-intent reconciliation;
  - tag creation message content;
  - tag idempotency and conflict behavior;
  - Velopack staged payload checks;
  - removal of version-tagging command from runner command model;
  - removal of `hx update` from scaffold command model;
  - `.doti/core` render/install paths;
  - `doti install` migration preservation/blocking of live config and modified legacy `doti/`.
- Golden/template tests:
  - generated repo has `.doti` assets and no root `doti/`;
  - generated skills point at `.doti/core`;
  - generated release manifest includes Velopack fields;
  - template architecture/Sentrux rules stay aligned.
- Command proof:
  - `dotnet run --project tools/Hx.Scaffold.Cli -c Release --no-build -- describe --json`;
  - `dotnet run --project tools/Hx.Runner.Cli -c Release --no-build -- describe --json`;
  - `dotnet run --project tools/Hx.Runner.Cli -c Release --no-build -- doti render-skills --repo . --agents codex,claude --check --json`;
  - `dotnet run --project tools/Hx.Runner.Cli -c Release --no-build -- gate run --repo . --profile release --json`;
  - `dotnet run --project tools/Hx.Runner.Cli -c Release --no-build -- doti install --repo <legacy-fixture> --agents codex,claude --json`;
  - local Velopack package inspection for the active RID.
- Search proof:
  - `rg "VersionBump|AddVersionBump|NextVersion|bump --major|bump --minor"`;
  - `rg "hx update|ScaffoldCommands.Update|ScaffoldUpdateService|AddUpdate"`;
  - any remaining hits must be historical/superseded notes only, not live command surfaces or guidance.

## Risks

- Velopack's cross-platform support must be validated per RID before Linux/macOS become enforced release targets.
- `hx release --major|--minor` will fail until GitVersion config and commit/merge-message practices make the requested intent observable.
- Moving Doti authority to `.doti/core` is high-blast-radius because renderer, installer, migration, managed hashes, generated skills, and tests all depend on those paths.
- Removing `hx update` eliminates the current repo-update command; migration must happen during this feature before the command disappears from released guidance.
- `doti install` becomes more important and must be carefully named in help/docs as repo-asset install/repair, not installed-tool update, so non-coder operators do not confuse it with Velopack updates.
- Release tag creation mutates git refs. Tests must use isolated repos and must never retag real release refs during ordinary unit tests.
