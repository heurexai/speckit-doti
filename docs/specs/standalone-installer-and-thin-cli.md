# Specification: Standalone installer (shared tool store) + thin-CLI enforcement + release pipeline

> Combined v0.1.0 cycle. WHAT/WHY only — mechanism deferred to the plan. Three workstreams in one cycle: (A) a standalone, versioned installer that provisions tools into a shared store and installs localised, version-stamped doti skills, with an update/compatibility command; (B) thin-CLI architecture enforcement; (C) the release pipeline + version tracking + README. Chosen design: the **Shared-Store SDK Model** (the only one meeting the shared-location requirement; the per-solution-bundled alternative was rejected for ~127 MB-per-solution duplication).

## Goal

A user can download **one versioned installer**, run it, and start a new agentic .NET project with no repository clone and no network after download. The heavy vendored tool binaries live **once** in a shared, versioned store (not duplicated per solution); each generated solution carries only its **localised, version-stamped** doti skills and pinned manifests, and can be reconciled with the store when tools drift out of date or too new. In the same release, the scaffold makes its central design rule — **thin CLI, fat core** — a build-failing gate (so the channel stays a channel), and ships a real release pipeline so users can see the version and what changed. This turns speckit-doti from a clone-and-run repo into an installable, versioned product.

## Scope

**Included**
- A standalone, downloadable, versioned installer for win-x64 that bundles the three vendored tool binaries (gitleaks, sentrux + its C# grammar, gitversion) and runs fully offline after download.
- A shared, versioned tool store; generated solutions resolve tools from it (no per-solution binary copies).
- `new` runnable from any directory (no scaffold-checkout requirement); doti skills installed localised + version-stamped.
- A diagnose/update capability (`doctor`/`update`) for tool/skill version drift, usable from the installer and from within a generated solution.
- A trusted prerequisite preflight for `new` so required system tools and directories are checked before a no-coder/operator or their agent starts generation.
- Windows-only, operator-approved prerequisite installation through winget when the trusted manifest declares a supported winget package for the missing requirement.
- Thin-CLI architecture enforcement: a constitution principle, plan/arch-review hooks, new ArchUnit families in the generated template, the same families dogfooded on the scaffold's own CLIs, and a CLI complexity budget.
- A CI release pipeline that builds the installer and publishes a GitHub Release with it attached; repository version tracking (baseline tag + changelog/notes); README install section linked to the Releases page.

**Excluded**
- linux-x64 / osx-arm64 tool binaries and installers (no upstream vendored binaries exist yet; the store layout is RID-keyed so they drop in later). Non-win-x64 hosts fail closed as today.
- Publishing to NuGet.org or GitHub Packages, and a secondary `dotnet tool` distribution channel (deferred; GitHub Release asset is the v0.1.0 channel).
- Adding a `downloadUrl` for the sentrux grammar (it remains bundle-sourced); fetching the grammar over the network is out of scope.
- Changing the doti workflow stages, the gate ladder semantics, or the generated solution's runtime behavior beyond tool resolution.
- Automatically installing the .NET SDK, Git, package managers, or other system prerequisites without explicit operator approval and a trusted install plan.
- Automatically installing prerequisites on non-Windows hosts or through a package manager other than winget.
- Trusting target-repo or cache-local configuration to provide executable download URLs for missing prerequisites.

## Functional Requirements

**A. Standalone installer + shared store**
- `FR-001`: The toolkit MUST be distributable as a standalone, versioned installer a user downloads and runs to install speckit-doti without cloning the repository.
- `FR-002`: The installer MUST bundle the vendored tool binaries (gitleaks, sentrux + grammar, gitversion) so installation completes fully offline with no network download.
- `FR-003`: On install, each bundled binary MUST be placed once into a shared, versioned, RID-keyed tool store and verified against its manifest SHA-256 before being usable; a verification failure MUST fail closed (no unverified binary retained).
- `FR-004`: `new` MUST run from any working directory (no requirement to be inside a scaffold checkout) and MUST produce a complete, compiling solution with doti installed — outcome-equivalent to today's `Hx.Scaffold.Cli new`.
- `FR-005`: Generated solutions MUST NOT contain copies of the tool binaries; they MUST resolve tools from the shared store via their pinned, hash-verified manifest, fall back to an in-repo path when one is present, and fail closed when neither verifies.
- `FR-006`: The deterministic gate (architecture / sentrux / hygiene / version / security) in a generated solution MUST resolve its tools from the shared store and behave identically to today's in-repo resolution.

**B. Localised, version-stamped skills**
- `FR-007`: doti skills MUST be installed localised per-solution (rendered per selected agents), tailorable per solution, exactly as today.
- `FR-008`: Each generated solution MUST record a version stamp of the scaffold and tool versions it was generated against, single-sourced from the vendored manifests (so it cannot disagree with the solution's own pins at generation time).

**C. Update / compatibility**
- `FR-009`: Users MUST be able to **diagnose** a solution's tool/skill version state against the shared store and the solution's pinned manifests, reporting per tool: up-to-date / out-of-date / too-new / hash-mismatch.
- `FR-010`: Users MUST be able to **update** a solution to reconcile drift — (re)populate the shared store for the pinned versions and/or re-render the localised skills — **idempotently**, never silently downgrading a solution that was generated by a newer toolkit.
- `FR-011`: The diagnose/update capability MUST be available both from the installed toolkit and from within a generated solution, so a cloned solution can self-heal.

**D. Thin-CLI architecture enforcement**
- `FR-012`: The constitution MUST include a **Channel Independence (Thin Adapter)** principle: core logic lives in `*.Core` libraries and is drivable from any channel; CLI/entry projects are thin adapters that parse → delegate to core → render, holding no business logic (they may construct and inject channel adapters into pure core).
- `FR-013`: The plan and arch-review stages MUST require a channel-boundary check — new logic lives in core, and the CLI delta is wiring-only — so thinness is reviewed before code.
- `FR-014`: Generated repos MUST enforce, as default architecture gates that fail the build, that (a) types in a `*.Cli` namespace are limited to channel-adapter roles (`cliSurfaceConfinement`) and (b) command types delegate to a core/library type (`cliDelegation`) — each with a non-vacuity guard, in `rules/architecture.json` + the template's architecture tests.
- `FR-015`: The scaffold's OWN solution MUST enforce the same thin-CLI families on its CLIs (`Hx.*.Cli`) via an architecture test project (dogfooding).
- `FR-016`: A complexity budget MUST bound CLI command-body size so logic cannot accumulate inside an allowed command type.

**E. Release pipeline + version tracking + README**
- `FR-017`: CI MUST, on a release tag, produce the standalone installer package (acquire the verified binaries, publish the self-contained executable, assemble the archive) and publish a GitHub Release with the installer attached.
- `FR-018`: The repository MUST carry release version tracking — a baseline annotated tag and a changelog / release notes — so users can see the current version and what changed.
- `FR-019`: The README install / getting-started section MUST be updated to install via the downloadable installer and link to the Releases page.

**F. Prerequisite preflight for new solutions**
- `FR-020`: Before creating a new solution, `new` MUST evaluate a trusted release-defined prerequisite manifest for the host platform and selected scaffold profile.
- `FR-021`: The prerequisite manifest MUST identify hard requirements such as compatible .NET SDK and Git, command probes, supported version ranges, and operator-facing install guidance.
- `FR-022`: Missing, outdated, unsupported, or untrusted hard prerequisites MUST fail `new` before output-directory mutation and MUST report precise human/JSON diagnostics with safe next actions.
- `FR-023`: `new` MUST validate required directories before mutation, including output path availability, parent write access, temporary/cache location readiness, payload readability, and shared tool-store accessibility.
- `FR-024`: Installation guidance for missing prerequisites MUST come from the verified release-defined manifest or immutable built-in trust anchors; target repo, cache, or user-editable manifests MUST NOT inject executable download URLs or weaken hard requirements.
- `FR-025`: Generated solutions MUST carry the trusted prerequisite policy needed for later build/test/gate/update validation, while any repo-local prerequisite extension is additive only and cannot override release-defined requirements.
- `FR-026`: On Windows only, `new` MUST offer automatic prerequisite installation through winget when the trusted release-defined prerequisite manifest declares a supported winget package/source for the missing prerequisite.
- `FR-027`: Before executing winget, `new` MUST present the exact install plan and obtain explicit operator permission for that plan; `--force`, JSON mode, or an agent retry MUST NOT imply install permission.
- `FR-028`: `new` MUST verify winget availability and use only trusted release-defined winget package/source metadata; repo-local extensions MUST NOT add or override winget install actions.
- `FR-029`: After winget completes, `new` MUST rerun prerequisite probes and continue generation only if every hard prerequisite is detected at a supported version.
- `FR-030`: Failed, cancelled, unavailable, or unverifiable winget installs MUST stop before output mutation and report safe next actions.
- `FR-031`: Non-Windows hosts MUST reject automatic prerequisite installation with platform-unsupported diagnostics and trusted manual remediation guidance.

## Success Criteria

- `SC-001`: On a clean win-x64 machine with no repo clone, a user can download the installer, run it, and `new` a solution that builds and tests green — fully offline after the download completes.
- `SC-002`: Installing N solutions on one machine yields exactly **one** copy of each tool version on disk (the shared store), not N copies.
- `SC-003`: A generated solution's `gate run` passes resolving every tool from the shared store, with no tool binaries inside the solution tree.
- `SC-004`: `doctor` correctly classifies a solution whose pinned tool version is absent / older / newer than the store; `update` reconciles it, and a second `update` run produces no change (idempotent).
- `SC-005`: A CLI-namespace type that is not an allowed channel-adapter role, or a command type that does not delegate to core, makes `dotnet test` fail — in both a generated repo and the scaffold's own solution.
- `SC-006`: Pushing a release tag produces a GitHub Release whose attached installer, when downloaded and run, reproduces `SC-001` (the release artifact *is* the installer).
- `SC-007`: The README's documented install steps match the actual release artifact and link to the Releases page.
- `SC-008`: On a machine without a compatible .NET SDK or Git, `hx new --json` fails before creating a partial solution and reports the missing prerequisite plus trusted install guidance.
- `SC-009`: Tampering with the release-defined prerequisite manifest or attempting to override install sources from a repo-local manifest causes `new` to fail closed before generation.
- `SC-010`: A non-writable output parent or unavailable temporary/cache directory is reported before template generation and leaves the target path unchanged.
- `SC-011`: On Windows with winget available and trusted package metadata present, the approved automatic-install flow installs the missing prerequisite through winget, reruns probes, and starts generation only after the prerequisite verifies.
- `SC-012`: On Windows without winget or without a trusted winget package mapping for the missing prerequisite, `new` refuses automatic install and reports instructions-only remediation before output mutation.
- `SC-013`: On Linux or macOS, requesting automatic prerequisite installation for `new` returns platform-unsupported diagnostics and does not invoke a package manager.

## Key Entities

- **Shared tool store** — a versioned, RID-keyed directory outside any solution holding verified tool binaries; carries its own index of installed tool/version/RID/hash entries.
- **Tool manifest (`*.version.json`)** — the pinned contract (version, RID asset, download URL, archive/executable SHA-256) that is the source of truth for both verification and version classification.
- **Solution version stamp** — a per-solution record of the scaffold + tool versions the solution was generated against, derived from the manifests.
- **Standalone installer archive** — a per-RID, versioned download containing the self-contained executable plus its embedded generation payload and bundled binaries.
- **Prerequisite manifest** — the trusted release-defined list of external system requirements, probes, version policy, command applicability, and install guidance for `new` and generated-repo validation.
- **Windows prerequisite install plan** — the exact trusted winget package/source actions that an operator approves before `new` may attempt automatic prerequisite installation on Windows.

## Deterministic Surfaces

- `new`, `doctor`, `update` (scaffold CLI) — `new` exists today (repo-bound); the location-independent + doctor/update behavior is **planned (advisory until built)**.
- `tools fetch` (runner CLI) — implemented; repurposed to write into the shared store and act as the online fallback. `doti tools doctor` / `doti tools update` — **planned (advisory)**.
- `gate run`, `architecture test`, `sentrux verify/check`, `version`, `security scan` — implemented; their tool resolution is **changed** to go through the shared store (in-repo fallback).
- Shared-store resolver + populator — **planned (advisory)**.
- `rules/architecture.json` + the template architecture tests (new `cliSurfaceConfinement` / `cliDelegation` families) and a new scaffold-own architecture test project — **planned (advisory)**.
- `.github/workflows/` release packaging, `CHANGELOG.md`, annotated tags, GitHub Release — **planned (advisory)**.
- JSON proof envelopes (`CliResult`) for `doctor`/`update` follow the existing agent-first contract.
- Trusted prerequisite manifest and preflight result for `new` — **implemented in the current working tree, pending final command proof**; output uses the existing `CliResult` diagnostics and next-actions contract.
- Windows-only winget prerequisite install flow for `new` — **implemented in the current working tree, pending final command proof**; unavailable on non-Windows and unavailable without trusted winget package metadata.

## Architecture Impact

- New shared-store types (a store path/layout resolver, a populator, and a `ToolStoreResolver`) placed so both the gate engine and the scaffold installer share one resolver (a shared assembly, e.g. contracts or runner-core; the vendored source set must include it).
- `ToolVendor` stops copying tool `bin/` into solutions (manifests/config/LICENSE only).
- Tool-resolution sites (GitVersion, gitleaks/sentrux validators + path resolver + grammar stager, and `ToolFetcher`'s write target) route through the store resolver with in-repo fallback.
- **`RepositoryPathResolver.ResolveInside` is explicitly UNCHANGED** — the in-repo escape guard is preserved; store resolution lives in a separate resolver, never by loosening the guard.
- Scaffold CLI gains an embedded generation payload + a self-contained/single-file publish profile + `doctor`/`update` commands, and drops the `scaffold-dotnet.slnx` CWD coupling.
- Prerequisite policy loading belongs in core/shared logic, not in CLI command bodies; CLI surfaces remain thin adapters over manifest-backed preflight results.
- Windows winget installation must be modeled as a permissioned remediation step with post-install probe verification, not as hidden behavior inside ordinary generation.
- A new architecture test project enforces thin-CLI families on the scaffold's own CLIs; the template gains the two new families. The constitution and the plan/arch-review templates gain the channel-independence principle/check.

## Sentrux And Hygiene Impact

- The shared store lives **outside** the repo and MUST NOT be scanned. Generated solutions no longer carry tool `bin/` binaries, so the dependency graph and hygiene scope are cleaner (no large gitignored binaries to skew signal).
- Adding the scaffold-own architecture test project and the store resolver to the project graph requires updating `rules/architecture.json` and `.sentrux/rules.toml` (layers/boundaries) in the same change, and confirming cross-engine consistency in arch-review (the new families measure the intended boundary; the store resolver sits in core, not CLI).
- Public hygiene: the installer executable, the ~127 MB bundled binaries, and any signing material are build/release artifacts — produced by CI, never committed. CHANGELOG and release notes are public.
- Prerequisite manifests and examples must not contain private machine paths, private package feeds, secrets, or mutable untrusted download URLs.
- Winget install reports must record only trusted package/source/probe provenance and outcome, not private package-source credentials or full environment dumps.

## Assumptions

(Reasonable defaults chosen where the brief was silent — override any.)

- **Store location**: per-user data directory on Windows and the XDG data directory elsewhere, with an `HX_TOOL_STORE` env override for a machine-global/CI store. Per-user avoids elevation; the gate and installer MUST honor the same override.
- **Code-signing**: the v0.1.0 installer ships **unsigned** with a documented SmartScreen/AV note; Authenticode signing is deferred to a later release once a certificate is provisioned.
- **PATH/system**: the installer is **download-and-run** (extract + invoke); it does **not** modify the user's PATH or system settings. A PATH shim is a later opt-in.
- **Template carriage**: the installer embeds a **prebuilt `Hx.Scaffold.Templates` nupkg** (no runtime `dotnet pack`); generation still requires the .NET SDK + git on PATH for `dotnet new` / build / `git init` (the installer self-contains acquisition, not the dev toolchain), and `new` must detect those prerequisites before mutating output.
- **Prerequisite installation**: the installer does not silently install system prerequisites. On Windows, it offers an explicit operator-approved winget install flow when trusted package metadata exists; on non-Windows, and when winget metadata is absent, remediation remains instructions-only.
- **Update bump policy**: `update` reconciles the **store to the solution's pin** by default; changing a solution's pinned tool version requires an explicit flag, and a too-new solution is never silently downgraded.
- **Cross-platform**: v0.1.0 is win-x64-only (the only vendored binaries); the archive is named `…-win-x64` to make that explicit; the store + resolution are RID-keyed for later RIDs.
- **Distribution / version**: GitHub Release asset is the sole channel; baseline version **v0.1.0**; notes = a curated CHANGELOG plus GitHub's auto-generated PR list.
- **CLI complexity budget**: enforced via the existing Sentrux per-function constraints if per-layer budgets are supported by the pinned release; otherwise via a tightened global budget or a dedicated check (resolved in plan).
- **Cycle shape**: delivered as one combined cycle releasing a single v0.1.0; tasks are phased (installer P1 → installer P2 → thin-CLI → CI/README) so drift-review stays tractable.

## Acceptance

- **Command-backed today**: `dotnet build`/`test`, `architecture test`, `tools fetch`, `hygiene`, and the existing gate ladder on win-x64.
- **Advisory until built in this cycle** (do not report as gate proof until implemented): the shared store + resolver + populator, location-independent `new`, `doctor`/`update` (+ `doti tools` mirror), the two new ArchUnit families, the scaffold-own architecture test project, the CLI complexity budget, the release workflow, the self-contained single-file installer, and CHANGELOG/release automation. The trusted prerequisite manifest/preflight and Windows-only operator-approved winget prerequisite installation are implemented in the current working tree, pending final command proof.

## Clarifications

(Populated by `/doti-clarify` — no blocking `[NEEDS CLARIFICATION]` markers were required; the materially-open choices are recorded under Assumptions for the operator to confirm or override.)

### 2026-06-23
- The later prerequisite-manifest wrinkle refines `new`: before creating a solution, `hx` must check trusted manifest-backed prerequisites and required directories, fail hard with no partial output when hard requirements are missing, and offer operator-approved install guidance without trusting repo/cache-local executable download URLs.
- The Windows automation refinement allows `new` to install missing prerequisites automatically only on Windows, only through winget, only when the trusted manifest declares a supported package/source, and only after explicit operator permission for the exact install plan.
