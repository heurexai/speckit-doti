# Standalone, Source-Free Hx CLI and Distribution

## Goal

After installation, an operator can run `hx` from the installed application directory — on Windows, Linux, or macOS — and have every normal command work: scaffold a new repo (`hx new`), check prerequisites, report version, and install/update Doti assets in a target repo (`hx doti install --repo`). The installed product is a normal compiled CLI: it contains no source code, depends on no `speckit-doti` source checkout, returns real stdout/stderr/exit codes synchronously, and renders all output through one consistent renderer.

This replaces the broken `v0.9.0` shape — a Velopack desktop installer whose root-level `hx.exe` stub launched `current\hx.exe` asynchronously and dropped command output, while `current\hx.exe` failed operational commands because they searched for the source solution `scaffold-dotnet.slnx` — and the Store build that shipped the entire source tree. It also commits the distribution model: `hx` ships as a **.NET global tool** (primary, all platforms) and a **Microsoft Store MSIX** package (Windows, Store-delivered updates). Velopack, self-contained GitHub app archives, and the self-updating desktop-installer model are removed from this feature scope. **Beyond the distribution core, 007 also delivers the enforcement substrate (Living-Spec evolution, ordered-task enforcement), profile-driven tiered/structure-agnostic gates, scaffold/doti separation, and the Spec Kit workflow absorptions (FR-026–FR-042) — built in 8 sequential phases per `docs/tasks/007-…-tasks.md`.**

Why this shape: the operator has no Authenticode or Apple signing budget, so an unsigned self-updating installer (the Velopack `Setup.exe`) is the friction to avoid; the Microsoft Store re-signs MSIX server-side and delivers updates without an operator-owned certificate; and because `hx`'s marquee commands shell out to the .NET SDK (`hx new` runs `dotnet new`/`dotnet build`; `hx release` runs `dotnet publish`), the audience already has the SDK — so the global-tool channel is the frictionless, signing-free, cross-platform path and a self-contained runtime bundle is unnecessary.

## Scope

Included:

- Remove the Velopack dependency, the `VelopackApp` runtime hook, the root-level launcher stub, and the `vpk`-backed release path from the product and its runtime.
- Make every installed operational command source-free: resolve templates, prerequisite policy, `.doti` assets, and vendored tools from payload shipped beside the installed executable, never from a source checkout or the current working directory.
- Keep source/developer-only commands (self-host scaffolding) allowed to reference solution/project files, but classify and gate them as source-only and exclude them from the normal installed surface.
- Distribute `hx` as: (1) a public NuGet.org .NET global tool (`dotnet tool install`/`update`) exposing `hx` on PATH on Windows/Linux/macOS; (2) a Microsoft Store MSIX exposing `hx` on PATH and updated by the Store.
- Guarantee no release artifact of any channel contains source code, enforced by a fail-closed packaging check.
- Make repo Doti install/repair/migrate/update flow through `hx doti install --repo <path>`, reconciling the bundled payload version with the target repo's recorded payload version.
- Route all `hx` help, results, diagnostics, and failures through the shared kernel renderer (rich/plain), with no command surface falling through to System.CommandLine's built-in help or error rendering.
- Update release proof so the exact documented per-channel command path is the path smoke-tested in a source-free location.
- **Expanded scope (FR-026–FR-042):** the enforcement substrate (Living-Spec spec evolution, ordered-task enforcement); profile-driven gate layering where the declared profile owns the gate ladder (curated tiers `workflow-only` / `dotnet-lib` / `dotnet-cli-heurex`) and doti installs structure-agnostically into existing repos without bypass; scaffold/doti separation (template = standalone `dotnet new` pack; binaries fetched, not bundled); and the Spec Kit workflow absorptions (bug mini-cycle, URL trust policy, context-budget implement, template methodology, transpilation, `converge`, checklist depth).
- **Glossary:** a *profile* (recorded in `.doti/integration.json`, defined in `.doti/profiles/<name>/profile.json`) is the single knob that selects a *tier* — its gate ladder; `workflow-only` = Tier 1, `dotnet-lib` = Tier 2, `dotnet-cli-heurex` = Tier 3.

Excluded:

- Velopack, `vpk`, and any in-app self-updating desktop-installer for the CLI.
- Self-contained per-OS app archives attached to the GitHub release; this direct-download channel is deferred until after the global-tool and Store channels are source-free and stable.
- A top-level `hx update` repository updater (repo updates remain `hx doti install --repo`).
- The installer/package mutating arbitrary target repositories.
- Requiring non-coder users to clone, compile, or run from the `speckit-doti` source repository.
- Treating `dotnet run` examples as released-package instructions.
- Authenticode / Apple Developer ID code-signing and notarization, and the signed direct-download or notarized-macOS auto-update channels they would unlock (deferred until a signing budget exists).
- winget and Homebrew submission/automation; existing packaging manifests that reference non-produced archives are removed or corrected, but new package-manager channels are deferred.

## Functional Requirements

Source independence:

- `FR-001`: Released `hx` MUST run as a compiled executable without a `.slnx`, `.csproj`, `tools/`, `src/`, or `scaffold/` source tree beside it.
- `FR-002`: Installed `hx` operational commands (`new`, `version`, `prereq check`, `doti install`) MUST NOT perform source-root discovery based on `scaffold-dotnet.slnx` or any other solution/project marker.
- `FR-003`: Installed `hx` operational commands MUST resolve runtime payload (templates, prerequisite policy, `.doti` assets, vendored tools) relative to the installed executable, identified by a non-source payload descriptor shipped beside it, with an explicit operator override taking precedence; resolution MUST NOT use the current working directory or a source solution/project file, and MUST fail closed with a structured `CliResult` diagnostic when the payload descriptor is absent.
- `FR-004`: Any command that still requires source files MUST be explicitly classified source/developer-only and MUST NOT be exposed as a normal installed command.

No source in releases:

- `FR-005`: Every release artifact of every in-scope channel (the global-tool package and the MSIX/Store package) MUST contain only the compiled executable and its runtime payload; none MUST contain the **tool's own build tree** (`scaffold-dotnet.slnx`, `src/Hx.*`, the tool's `tools/*.csproj`) or a full source-tree archive. The template NuGet pack and the manifest/grammar payload are legitimate runtime payload, not source.
- `FR-006`: Release packaging MUST fail closed if any produced artifact contains a source marker.

Distribution channels (drop Velopack):

- `FR-007`: The product and its runtime MUST NOT depend on Velopack; the Velopack package reference, the `VelopackApp` startup hook, and the root-level launcher stub MUST be absent from the shipped CLI.
- `FR-008`: The installed `hx` command path for every channel MUST be a synchronous compiled CLI that returns stdout, stderr, and the process exit code normally; no channel MUST install an asynchronous launcher/stub that drops command output.
- `FR-009`: `hx` MUST be published as a public NuGet.org .NET global tool (package id `Heurex.SpeckitDoti`) that installs with a one-command `dotnet tool install -g Heurex.SpeckitDoti` experience, updates with `dotnet tool update -g Heurex.SpeckitDoti`, and places a working `hx` on PATH on Windows, Linux, and macOS.
- `FR-010`: `hx` MUST be packageable as a Microsoft Store MSIX that exposes `hx` on PATH via a console execution alias and runs as a console application.
- `FR-011`: The MSIX/Store build MUST NOT contain any in-app self-update code path; updates for that channel are delivered by the Microsoft Store.
- `FR-012`: Self-contained per-OS GitHub app archives MUST be out of scope for this feature; GitHub release guidance for this hotfix MUST direct users to the public NuGet global tool for cross-platform install/update and to the Microsoft Store MSIX for the Windows installer/update channel.

Updates — two planes:

- `FR-013`: Tool-on-machine updates MUST be delivered per channel — `dotnet tool update` for the global-tool channel and the Microsoft Store for the MSIX channel — and installed `hx version` MUST report the active channel and its update command.
- `FR-014`: Repo-owned Doti asset install, repair, migration, and update MUST be performed by `hx doti install --repo <path>` using the `.doti` payload bundled beside the installed `hx`; there MUST be no top-level repository-updater command.
- `FR-015`: `hx doti install --repo` MUST reconcile the bundled payload version against the target repo's recorded payload version: equal → verify/repair; repo older → migrate forward while preserving operator-modified managed files; repo newer → refuse; and MUST report installed, preserved, blocked, and removed paths with reasons.
- `FR-016`: Top-level and `hx doti` help MUST make repo Doti update/migration discoverable by describing `doti install` as installing, repairing, migrating, or updating Doti workflow assets in an explicit `--repo` target.

Renderer consistency:

- `FR-017`: All `hx` root, group, and leaf help, all parse/validation errors, and all command results MUST be produced by the shared kernel renderer — as the `CliResult` JSON envelope when machine output is selected, otherwise through the shared human renderer — and MUST honor `--help-mode plain`, `--plain-help`, `HX_HELP_MODE=plain`, and `NO_COLOR`.
- `FR-018`: No `hx` command surface MUST fall through to System.CommandLine's built-in help, version, or parse-error rendering; a fail-closed check MUST assert that no command in any tool root emits System.CommandLine default help/error output.

Per-command source-free behavior:

- `FR-019`: Installed `hx prereq check` MUST load prerequisite policy from the installed payload, not a source checkout.
- `FR-020`: Installed `hx new` MUST create a repo using installed template/payload assets, not by locating the `speckit-doti` source solution.
- `FR-021`: Installed `hx version` (and `version --repo <path>`) MUST remain read-only and MUST report the running executable path, application directory, and active channel without requiring a source checkout.
- `FR-022`: `hx describe --json` MUST mark which commands are installed-mode versus source/developer-only, and MUST report the active distribution channel and its update mechanism.

Release proof:

- `FR-023`: Release proof MUST, for each in-scope channel, install the artifact into a location containing no source files and exercise the exact documented operator command path, asserting it returns the `CliResult` envelope and correct exit codes for `hx --help`, `hx version --json`, `hx prereq check --json`, `hx new --name <Tmp> --output <temp> --json` (scaffolds from installed payload), and `hx doti install --repo <temp> --json`.
- `FR-024`: Release proof MUST fail if the documented installed command path differs from the path it tested, if that path drops command output, or if any normal installed command fails because a source solution or project file cannot be found.

Template inheritance:

- `FR-025`: The scaffolded application template MUST inherit these rules: release-installed application commands run without their source repository, ship no source in release artifacts, carry no Velopack stub, resolve payload beside the executable, and route all output through the shared renderer.

Expanded scope — enforcement substrate, profile layering, and workflow absorption (built in the phases recorded in `docs/tasks/007-…-tasks.md`):

- `FR-026`: doti's value MUST be the non-forgeable proof + fail-closed chokepoint substrate over the shared spec-driven workflow; the workflow steps MUST be positioned to ride/absorb the shared (Spec Kit) layer rather than diverge silently.
- `FR-027`: A spec MAY be revised after later stages without invalidating the cycle; a spec change MUST trigger re-clarify + re-analyze of dependent artifacts under a defined Living-Spec persistence model, not a blocking re-stamp cascade.
- `FR-028`: Task completion MUST be order-enforced: a task MUST NOT be completable before its declared predecessors/phase are complete and the prior phase gate is green; out-of-order completion MUST fail closed.
- `FR-029`: The repo's declared profile MUST own the gate ladder (which gates run and in which mode); the gate MUST read the ladder from the declared profile rather than hardcode it, and the `GateProof` MUST record the active profile + coverage.
- `FR-030`: doti MUST install into an existing repo without imposing the Heurex structure; an opinionated gate (Sentrux/ArchUnitNET) MUST run only when the declared profile enables it; a gate declared enforced but missing its config MUST fail closed (no delete-config bypass); a profile that does not enable a gate MUST skip it advisory.
- `FR-031`: Profiles MUST compose via a layered resolution stack (overrides → profile → core) with prepend/append/wrap, so governance layers onto base templates + gate ladders without forking.
- `FR-032`: The scaffold template MUST be a standalone `dotnet new` NuGet pack independent of doti; `hx new` MUST be a thin orchestration of template instantiation + `hx doti install --repo` + first smoke.
- `FR-033`: The tool/release payload MUST ship tool manifests + grammars only (no large binaries); per-RID tool binaries MUST be fetched + hash-verified on demand.
- `FR-034`: doti MUST provide an enforced bug mini-cycle (assess→fix→test): assess is read-only and produces a verdict/remediation contract, fix is the only writer and is bound to the assessment, test verifies honestly; each stage is proof-bound.
- `FR-035`: Any doti command that ingests a URL MUST apply a trust policy: refuse `file:`/loopback/RFC1918/cloud-metadata, allowlist hosts, and treat fetched content as data, never instructions.
- `FR-036`: `implement` MUST support scoped, resumable runs gated by task-completion markers, sub-agent delegation of parallel tasks, and spec-of-specs decomposition for features too large for one context.
- `FR-037`: The spec and tasks templates MUST support prioritized user stories (P1/P2/P3 with an independent test) and MVP-first phase organization.
- `FR-038`: doti skills/commands MUST be authorable once and rendered to multiple agent formats via one registrar.
- `FR-039`: doti MUST provide a `converge` command that reconciles the codebase against spec/plan/tasks and appends remaining unbuilt work as tasks.
- `FR-040`: doti's requirement-quality checklist MUST meet the "unit tests for English" bar (Completeness/Clarity/Consistency/Measurability/Coverage with ≥80% traceability references).
- `FR-041`: The README/docs MUST document the install profiles/tiers and what each enforces, the scaffold-vs-`doti install` distinction, the channels, the upgrade flow, and the manifest-ships/binary-fetches model.
- `FR-042`: `hx describe --json` and help MUST surface the active profile/tier + gate ladder, the installed-vs-source command mode, the active channel, and its update mechanism.
- `FR-043`: `/06-doti-arch-review` MUST be given the changed source-file list automatically: `hx` (reusing the impact planner's git-diff + project-graph closure) MUST emit the changed/affected source files as machine-readable arch-review context, so the review and its per-lens sub-agents receive the changed files without rediscovering them.
- `FR-044`: The default release version intent MUST be **minor** for a normal feature cycle (any new spec), and **patch** MUST apply only to a **bug-fix-only cycle** (the assess→fix→test bug mini-cycle of FR-034). This changes doti's prior patch-by-default behavior; an explicit `--major|--minor|--patch` still overrides.

## Success Criteria

- `SC-001`: On Windows, Linux, and macOS, after `dotnet tool install` of `hx`, `hx --help`, `hx version --json`, `hx prereq check --json`, and `hx new ...` run with no `scaffold-dotnet.slnx`/`.slnx`/`.csproj` lookup failure.
- `SC-002`: After installing the MSIX, `hx version --json` runs through the PATH alias and the installed package contains no source files.
- `SC-003`: A search of normal installed command code paths finds no dependency on `ScaffoldRoot.Resolve`, `scaffold-dotnet.slnx`, or source project markers outside explicitly named source/developer-only code.
- `SC-004`: No produced in-scope release artifact (global-tool package or MSIX) contains `.slnx`, `.csproj`, `src/`, `scaffold/`, or a full source tree; the packaging step fails closed when one is present.
- `SC-005`: The repository contains no Velopack package reference, no `VelopackApp` usage, and no `vpk` invocation in the product/runtime/release path.
- `SC-006`: `hx --help` and `hx doti --help` show that repo Doti update/migration is performed with `hx doti install --repo <path>`, render with the shared branded style on a rich terminal and ANSI-free in plain mode, and no command surface (including an unknown-option error and `--version`) emits System.CommandLine default output.
- `SC-007`: `hx doti install --repo` against a repo whose recorded payload is older migrates forward and preserves an operator-modified managed file (reported preserved/blocked); against a repo whose payload is newer, it refuses.
- `SC-008`: Release proof records the installed command path tested for each channel and fails when that path differs from the documented operator command path, or when `version` works but another operational command fails on a missing source file.

## Key Entities

- **Distribution Channel**: one of {global tool, Microsoft Store MSIX}, each with its install command, its PATH-resolvable `hx` entry, and its update authority (`dotnet tool update` / the Store).
- **Installed Command Path**: the exact `hx` entry point documented for users on a given channel and verified by release proof — synchronous, compiled, source-independent.
- **Payload Descriptor**: the non-source manifest shipped beside the installed `hx` that records payload version, tool version, schema version, channel, and installed mode; the marker installed commands resolve against (never a solution/project file).
- **Released Payload**: the files packaged beside installed `hx` that replace source-checkout dependencies — templates, `.doti` assets, prerequisite manifest, vendored tools, release metadata.
- **Source/Developer Command**: a command intended only for a `speckit-doti` source checkout; may reference solution/project files but is excluded from the normal installed surface.

## Deterministic Surfaces

- `hx --help`, `hx <group> --help`, `hx <leaf> --help`, parse/validation errors, and all results: MUST use the shared kernel renderer (`CliResult` / human renderer); no System.CommandLine default help or error path.
- `hx version --json`: MUST report installed executable/application identity and active channel without source lookup.
- `hx prereq check --json`: MUST use the installed prerequisite manifest/configuration.
- `hx new --json`: MUST use installed template and payload assets.
- `hx doti install --repo <path> --json`: MUST use installed `.doti` payload and report version reconciliation (install/repair/migrate/refuse) with per-path reasons.
- `hx describe --json`: MUST mark installed vs source/developer commands and report channel + update mechanism.
- `hx release ... --json`: MUST produce the in-scope channel artifacts (global-tool package; the curated MSIX payload/layout) — not a `vpk pack` or self-contained app archives — run the per-channel source-free install smoke, and record tested command paths and payload/source checks in the release identity. Where a channel's packaging tooling is unavailable in the current environment (e.g. `makeappx`/Windows SDK or Store credentials), it MUST be marked advisory rather than faked.
- `payload.manifest.json` beside the executable, `hx.config.json`, and `.doti/release.json`: configure installed/release behavior without requiring source files at runtime.
- `.github/workflows/store-release.yml`: MUST assemble the MSIX from the curated published executable + payload, never from a full source archive (`git archive HEAD`).

## Architecture Impact

Affected areas:

- `tools/Hx.Scaffold.Cli`: remove the Velopack package reference and `VelopackApp.Build().Run()`; route command wiring through a kernel hardening entry so help/errors never reach System.CommandLine defaults; replace `ScaffoldRoot.Resolve` usage in installed commands with the payload resolver; classify installed vs source/developer commands.
- `src/Hx.Scaffold.Core`: introduce a `PayloadRoot` resolver anchored on `AppContext.BaseDirectory` + the payload descriptor; retarget `LocalReleaseService` from the `vpk`/Velopack path to producing the global-tool package and the curated MSIX layout, plus the per-channel source-free install smoke and the no-source packaging gate; demote `scaffold-dotnet.slnx` discovery to a source/developer-only path.
- `tools/Hx.Cli.Kernel`: add the single-renderer hardening (own help, version, and parse-error rendering for every command tree) and the fail-closed renderer check.
- `tools/Hx.Doti.Core`: add payload-version stamping and reconciliation (migrate-forward, preserve operator-modified managed files via the existing canonical hash baseline) to `DotiInstaller`; update `doti` group help wording.
- `tools/Hx.Tooling.Contracts`: add channel + command-mode + per-channel install-proof fields to the release/describe contracts.
- `.github/workflows/store-release.yml` and `release.yml`: curated no-source MSIX payload plus public NuGet global-tool package publishing; remove the Velopack/`vpk` staging and self-contained app archive publishing; run the source-free per-channel smoke before upload.
- `packaging/`: align or remove manifests that reference non-produced artifacts; the MSIX manifest gains a console `AppExecutionAlias`.
- `scaffold/templates/dotnet-cli`: inherit the standalone, source-free, no-Velopack, single-renderer, dotnet-tool-packable rules.
- README, CHANGELOG, `packaging/PUBLISHING.md` and `STORE.md`, Doti agent context, and generated skills: released-package instructions use the three channels and never imply a source checkout.

## Sentrux And Hygiene Impact

- Keep CLI commands thin; payload resolution, packaging, and reconciliation live in core services (`cliSurfaceConfinement`/`cliDelegation` families).
- Removing the vendored Velopack (`vpk`) package reduces the gitignored large-binary vendor surface; the `velopack` tool manifest and fetch path are retired.
- Installed payload files are release assets, not source archives; hygiene scans should treat the per-channel payload as such.
- No new shell runners are introduced in the repo; MSIX packaging (`makeappx`) and Store submission run in CI workflow YAML, not repo-tracked shell scripts.
- No broad Sentrux baseline changes expected.

## Assumptions

- Drop-Velopack is an operator decision (2026-06-25) under a no-signing-budget constraint. Verified premises: `store-release.yml:53` staged the full source tree via `git archive HEAD`; `Program.cs` ran `VelopackApp.Build().Run()` in every build; `hx`'s marquee commands shell out to the .NET SDK (`new` → `dotnet new`/`build`, `release` → `dotnet publish`), so the audience already has the SDK and a self-contained runtime bundle's primary benefit does not apply.
- Microsoft Store server-side signing provides end-user trust for the MSIX without an operator-owned certificate; a GitHub-downloaded (sideloaded) MSIX is not a supported double-click install without a trusted cert, so the trusted Windows installer/update channel is the Store and the cross-platform GitHub-facing instruction is the public NuGet global tool.
- macOS is supported via the global tool (no Gatekeeper quarantine under the `dotnet` host); unsigned macOS GitHub app archives are deferred until notarization or another trust story exists.
- Auto-update is per-channel: the Store updates the MSIX automatically; the global tool updates via `dotnet tool update` (operator-run, optionally surfaced by a `version` notice). Background silent auto-update of a standalone binary is out of scope without signing.
- MSIX packaging and Store submission remain CI-driven (`store-release.yml`) because `makeappx`/the Windows SDK and Store credentials are not assumed on a developer machine; `hx release` locally produces the global-tool package and the curated MSIX layout, and the proof smokes what the current environment supports.

## Acceptance

Command-backed now:

- Source scan proving installed command paths still reference `ScaffoldRoot.Resolve` / `scaffold-dotnet.slnx` (the defect to remove).
- `store-release.yml:53` `git archive HEAD` reproduction showing the MSIX layout receives the full source tree (the no-source violation to remove).
- `Program.cs` `VelopackApp.Build().Run()` plus the Velopack package reference (the dependency to remove).

Required before implementation is accepted:

- Build and focused unit tests for payload resolution, prereq/new/version/doti-install source-free behavior, payload-version reconciliation, renderer hardening (no System.CommandLine fall-through), the no-source packaging gate, and per-channel install smoke.
- A real local global-tool install smoke (`dotnet tool install` from the produced package) proving the documented command path works without source files; a curated MSIX layout proven source-free; CI MSIX/Store smoke advisory until run in CI.
- `gate run --profile normal` before drift review and `gate run --profile release` before release.

## Clarifications

### 2026-06-25

- Q: Should the .NET global tool be published to public NuGet.org, a GitHub release `.nupkg`, or a GitHub Packages NuGet feed? -> A: Publish it to public NuGet.org so the normal install/update UX is `dotnet tool install -g <package-id>` and `dotnet tool update -g <package-id>`.
- Q: Are self-contained per-OS GitHub app archives in scope for this feature, or is the GitHub-facing install path satisfied by the public NuGet global tool plus Microsoft Store MSIX? -> A: Defer self-contained per-OS GitHub app archives. This hotfix is scoped to the public NuGet global tool and Microsoft Store MSIX, with GitHub release guidance pointing to those channels instead of direct app archives.
