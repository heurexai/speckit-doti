# Plan: Standalone installer (shared store) + thin-CLI enforcement + release pipeline

> Spec: `docs/specs/standalone-installer-and-thin-cli.md`. Chosen design: the **Shared-Store SDK Model**. Delivered as one v0.1.0 cycle, tasks phased: installer P1 (store + offline `new`) → installer P2 (doctor/update) → thin-CLI enforcement → CI release + version tracking + README.

## Technical Context

- **Stack:** .NET 10, C#, System.CommandLine, the agent-first `CliResult` kernel, xUnit v3, ArchUnitNET, vendored Sentrux/Gitleaks/GitVersion (win-x64), GitVersion-backed versioning.
- **Approach:** Re-package `Hx.Scaffold.Cli` as a self-contained, single-file **win-x64 installer** whose generation payload (template content, `doti/` + `.doti/` trees, the 12 vendored source projects, tool manifests, prerequisite manifest, and the three tool binaries + sentrux grammar) is carried as **embedded resources**, extracted to a temp "source root" on `new` — removing the `scaffold-dotnet.slnx` CWD coupling. The bundled binaries are installed **once** into a shared, versioned, RID-keyed **tool store**; generated solutions resolve tools from the store (in-repo fallback), so no binary is copied per solution. doti skills stay localised + gain a version stamp. A trusted prerequisite preflight checks .NET SDK/Git and directories before `new`; Windows can run an explicit winget remediation flow when the trusted manifest allows it. `doctor`/`update` reconcile store/skill drift. The scaffold's thin-CLI design becomes a build-failing gate (template + scaffold-own). CI builds the installer on a release tag and publishes a GitHub Release.
- **Constraints:** `RepositoryPathResolver.ResolveInside`'s in-repo escape guard MUST stay intact (store resolution is a *separate* resolver). All tool provisioning stays fail-closed + SHA-256-verified (reuse `FileHashing` + the `ToolFetcher` pipeline). Generation still needs the .NET SDK + git on PATH; `new` must detect them before output mutation. Windows automatic prerequisite installation is winget-only, operator-approved, and driven only by release-defined package/source metadata. win-x64-only for v0.1.0; store + resolution RID-keyed.
- No unresolved `[NEEDS CLARIFICATION]` — the spec's open choices are decided in Research below.

## Constitution Check (gate — before design)

PASS against `.doti/memory/constitution.md`. (This cycle also *adds* a ninth principle, **Channel Independence (Thin Adapter)** — FR-012.)

- **Deterministic Ownership** — tools provisioned only from pinned manifests + SHA-256; the store verifies on populate and on resolve; fail-closed; the gate stays the authority.
- **Bootstrap Honesty** — store/resolver, `doctor`/`update`, the new ArchUnit families, the scaffold-own arch project, and the release workflow are marked **advisory until built**; not reported as gate proof early.
- **Template Boundary** — the template still owns static solution content; the installer/CLI owns dynamic finishing, now including store population + the skill version stamp. No new responsibility crosses into the template engine.
- **Public Hygiene** — the installer exe + ~127 MB bundled binaries are **CI release artifacts** (gitignored, never committed); signing material (if later) is a secret. Manifests stay pinned+hashed.
- **Cross-Platform Rule** — win-x64 honest; non-win fails closed; no PowerShell/Bash *runners* are added to generated repos (the release workflow is CI for this repo, .NET + `gh`).
- **Codified Cycle / Engineering Discipline** — through the cycle; premises verified against the mapped code (the design workflow read every site); commit via the sanctioned path.
- **Prerequisite trust boundary** — prerequisite package/source metadata is release-defined and managed; target repos cannot inject executable URLs or downgrade hard requirements.

No violation → Complexity Tracking empty.

## Research (resolve unknowns)

- **Distribution format.** Decision: a per-RID **self-contained single-file** `hx.exe` (`dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`), shipped as a GitHub Release archive `speckit-doti-<version>-win-x64.zip`. Rationale: a RID-agnostic `dotnet tool` is a poor carrier for 127 MB of win-x64 native binaries (forces the payload onto every platform; wrong store location). Alternatives rejected: plain `PackAsTool` (RID-agnostic IL); .NET 10 RID-specific tool (`ToolPackageRuntimeIdentifiers`) — viable later as a secondary channel once a feed exists, deferred.
- **Payload carriage.** Decision: embed the generation payload (template **prebuilt nupkg**, `doti/`+`.doti/` trees, the 12 source projects, tool manifests, the three binaries + grammar) as embedded resources; a pre-pack MSBuild target stages them from the live repo. On `new`, extract to a temp dir used as `sourceRepoRoot`. Rationale: makes `new` location-independent and offline. Alternatives rejected: read from a live checkout (today's coupling); pack the template at runtime (needs the source csproj).
- **Store location.** Decision: per-user data directory on Windows (XDG data dir on non-Windows) + `HX_TOOL_STORE` override for machine-global/CI. Rationale: no elevation; the override lets the gate and installer agree on one store. Alternatives rejected: machine-global system data default (needs elevation).
- **Resolver placement.** Decision: put `ToolStore` + `ToolStoreResolver` in **`Hx.Runner.Core`** (alongside `ToolFetcher`). Rationale: `Hx.Version.Core`, `Hx.Sentrux.Core`, and `Hx.Security.Core` already depend on `Hx.Runner.Core`, and it is in the vendored source set, so generated solutions get it with **no new project/layer**. Alternatives rejected: a new `Hx.Scaffold.Store` project (extra layer + vendor entry); `Hx.Tooling.Contracts` (it is contracts-only, no logic).
- **Security invariant.** Decision: do NOT loosen `RepositoryPathResolver.ResolveInside`; add `ToolStoreResolver.Resolve(tool, version, rid, expectedSha) → verified store path | null`, and change each tool-resolution site to *try store → fall back in-repo → fail closed*. Rationale: keeps the "no path escapes the repo" guard for in-repo assets. Alternatives rejected: allow out-of-repo paths in `ResolveInside` (erodes the guard for all callers).
- **Template carriage.** Decision: embed a **prebuilt** `Hx.Scaffold.Templates.<ver>.nupkg`; `TemplateGenerator` switches from runtime `dotnet pack` to `dotnet new install <embedded nupkg>` in the sandbox. Rationale: removes the source-csproj dependency at `new` time. Alternatives rejected: pack-at-runtime (needs repo source).
- **Sentrux grammar.** Decision: remains **bundle-sourced** (it has a SHA-256 but no `downloadUrl`); the installer/bundle is its only source; `doctor` surfaces a clear error if a solution pins a sentrux version whose grammar the installer didn't bundle. Alternatives rejected: add a grammar `downloadUrl` (out of scope; upstream fork change).
- **CLI complexity budget (FR-016).** Decision: enforce via a small **deterministic ArchUnit-adjacent check** if Sentrux per-layer budgets are unconfirmed for the pinned release — verified during arch-review. Rationale: the structural families (confinement + delegation) are the primary teeth; the budget is the backstop. Alternatives rejected: a blanket tighter global `max_fn_lines` (would fire on legitimate core functions).
- **Version baseline + notes.** Decision: baseline annotated tag **v0.1.0**; `CHANGELOG.md` (Keep a Changelog) + GitHub auto-generated PR list in the Release body. Rationale: matches the squash-PR history; "linked to the releases folder". Alternatives rejected: release-please/semantic-release (non-.NET dependency vs the existing GitVersion).
- **Prerequisite manifest and preflight.** Decision: add a trusted release-defined prerequisite manifest carried in the installer payload and generated repo metadata, with a core preflight service in `Hx.Scaffold.Core`. `new` runs the check before payload extraction side effects, template install, `dotnet new`, store population, Git initialization, or output mutation. Rationale: the no-coder/operator path must fail early with precise, trusted next actions rather than surfacing a later `dotnet new` or `git init` failure. Alternatives rejected: hard-code checks in `ScaffoldCommands.New`; trust repo-local config for download URLs; let downstream subprocess failures act as detection.
- **Windows winget remediation.** Decision: expose `hx prereq check` and `hx prereq install` as the intentional install surface; ordinary `new` emits diagnostics/next actions but does not install implicitly. The initial trusted winget mappings are `Microsoft.DotNet.SDK.10` for .NET SDK 10 and `Git.Git` for Git, both verified from winget on 2026-06-23 with `winget show --id ... --exact`. Rationale: separate install commands satisfy the operator-approved automation requirement while preventing agent retries, `--force`, or JSON mode from becoming implicit package-manager execution. Alternatives rejected: `new --install-missing`; non-Windows package-manager automation in this cycle; raw installer URLs copied from winget output.

## Design

**Workstream A — shared store + offline, location-independent `new` (installer P1)**

- `tools/Hx.Runner.Core/Tools/ToolStore.cs` (new): `Root()` (per-OS + `HX_TOOL_STORE`), `PathFor(tool, version, rid, exeName)`, `.store-manifest.json` read/write with a file lock for transactional, additive writes.
- `tools/Hx.Runner.Core/Tools/ToolStoreResolver.cs` (new): `Resolve(...) → verified store path | null` (SHA-256 check), used by every resolution site.
- `tools/Hx.Runner.Core/Tools/StorePopulator.cs` (new): write verified bytes (from embedded payload or `ToolFetcher`) into the store; reuse `FileHashing` + fail-closed.
- Resolution-site edits (store-first → in-repo fallback → fail closed): `Hx.Version.Core/GitVersionTool.cs` (`Verify`/`ResolveExecutable`), the gitleaks validator + the sentrux validator/`SentruxToolPathResolver`/`SentruxChecker`/`SentruxBaselineRunner`/`SentruxGrammarStager`, and `ToolFetcher.Fetch` (write target → store).
- `src/Hx.Scaffold.Core/ToolVendor.cs`: replace `DirectoryCopy.Copy(_, _, _ => true)` with a predicate excluding `bin/` and `grammars/` (carry manifests/config/LICENSE only). `RefreshGitleaksConfig` unchanged.
- `tools/Hx.Scaffold.Cli/` + `src/Hx.Scaffold.Core/`: `EmbeddedPayloadExtractor` (new) replaces `ScaffoldRoot.Find(CWD)` for the installed path (keep `ScaffoldRoot.Find` as a dev/self-host fallback when the marker exists). `TemplateGenerator` installs the embedded prebuilt nupkg.
- `Hx.Scaffold.Cli.csproj`: self-contained single-file publish profile (conditional, so dev `dotnet run` stays fast); `StageEmbeddedPayload` pre-pack target; `<Version>` wired from GitVersion; `ScaffoldNewRunner` populates the store before first smoke.

**Workstream A2 — trusted prerequisite preflight and Windows winget remediation**

- `src/Hx.Scaffold.Core/Prerequisites/*` (new): manifest model/loader, command applicability, probe runner, directory checks, check report, install-plan builder, and Windows winget adapter.
- `doti/core/prerequisites.json`: hard requirements for compatible .NET SDK and Git; command mapping for `new`, `update`, repo-aware `version`, and generated-repo validation; trusted instruction text; Windows winget mappings for `Microsoft.DotNet.SDK.10` and `Git.Git`.
- `ScaffoldNewRunner`: call prerequisite preflight before output mutation, payload extraction side effects, template install, store population, first smoke, or Git initialization.
- `ScaffoldCommands`/`ScaffoldCommandFactory`: add `hx prereq check --for <new|update|version|generated-validation>` and `hx prereq install --for <new|update> [--confirm-plan <digest>]`; both return `CliResult`, appear in `describe`, and respect rich/plain/JSON output.
- `WingetPrerequisiteInstaller`: Windows-only; verify winget availability, present exact trusted package/source plan, require operator approval bound to a plan digest, execute winget, rerun probes, and refuse continuation unless every hard prerequisite verifies. Non-Windows and missing package mappings return instructions-only diagnostics.

**Workstream B — localised version-stamped skills + doctor/update (installer P2)**

- `tools/Hx.Doti.Core/DotiInstaller.cs`: `WriteMetadata` also emits `.doti/tool-stamp.json` (scaffoldVersion + per-tool version/rid/sha from the manifests) and extends `DotiIntegration`.
- `doctor`/`update` engine (in `Hx.Runner.Core` or `Hx.Doti.Core`): three-way classify (stamp vs store vs pinned manifest) → per-tool up-to-date/out-of-date/too-new/hash-mismatch; `update` reconciles store↔pin and re-renders skills, idempotent, never silent-downgrade.
- Command surface: `hx doctor`/`hx update` in `Hx.Scaffold.Cli/Program.cs`; mirrored `doti tools doctor`/`doti tools update` in `Hx.Runner.Cli/RunnerCommands.cs` (so a cloned solution self-heals). JSON `CliResult` proofs.

**Workstream C — thin-CLI enforcement**

- `doti/core/memory/constitution.md` (+ rendered `.doti/memory/constitution.md`): add **Channel Independence (Thin Adapter)**.
- `doti/core/templates/plan-template.md` + `commands/doti-arch-review.md`: add the channel-boundary check/checklist item.
- Template: `scaffold/templates/dotnet-cli/rules/architecture.json` + `.../test/HxScaffoldSample.Architecture.Tests/ArchitectureTests.cs` — add families `cliSurfaceConfinement` (CLI-namespace types limited to `Program`/`Agent`/`*Command(s)`/`*Module`) and `cliDelegation` (`*Commands` depend on a library type), each with a non-vacuity guard.
- Scaffold-own dogfood: new `test/Hx.Architecture.Tests/` project loading the `Hx.*.Cli` + `*.Core` assemblies, applying the same families; new **root** `rules/architecture.json` (absent today) declaring them; add the project to `scaffold-dotnet.slnx`.
- CLI complexity budget per Research.

**Workstream D — CI release + version tracking + README**

- `.github/workflows/release.yml` (new): on `v*` tag → checkout → `tools fetch` (acquire verified binaries CI lacks) → `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true` → assemble `speckit-doti-<ver>-win-x64.zip` → `gh release create <tag> --generate-notes` + attach the archive (needs `contents: write`).
- `CHANGELOG.md` (new, Keep a Changelog) seeded with v0.1.0; baseline annotated tag `v0.1.0`; optional `GitVersion.yml` to pin calculation.
- `README.md`: rewrite install/get-started around downloading the installer, linked to the Releases page.

**Architecture delta (rules kept mutually consistent):**

- `ToolStore`/`ToolStoreResolver`/`StorePopulator` live in `Hx.Runner.Core` (existing **core** layer) — `.sentrux/rules.toml` needs **no new layer/path**; `rules/architecture.json` (root, new) is added for the scaffold-own arch test but is consistent with the existing Sentrux core/cli direction.
- New `test/Hx.Architecture.Tests` project added to `scaffold-dotnet.slnx`; test projects are not in the Sentrux `[[layers]]` (which list contracts/core/cli) so no boundary change — confirm in arch-review.
- Template `rules/architecture.json` gains two families (six structural + capability + output + **two CLI-confinement** → ten); the template `.sentrux` already forbids core→cli, consistent.
- Constitution gains Channel Independence; plan/arch-review templates gain the channel-boundary check.

## CLI surface & error contract

New commands `hx doctor`/`hx update` (scaffold CLI), `hx prereq check`/`hx prereq install` (scaffold CLI), and `doti tools doctor`/`doti tools update` (runner CLI). All return the `CliResult` envelope (JSON-first), appear in `describe`, and emit appended, frozen error codes:

- **`validation` / `tool-store-version-unavailable`** (`VAL####`) — the version a solution pins is absent from the store (out-of-date store). Exit class **Validation**.
- **`validation` / `tool-store-version-too-new`** (`VAL####`) — the solution was generated by a newer toolkit than is present; refuse to downgrade. Exit class **Validation**.
- **`integrity` / `tool-store-hash-mismatch`** (`ITG####`) — a store binary's SHA-256 ≠ manifest. Exit class **Integrity**.
- **`internal` / `tool-store-populate-failed`** (`INT####`) — writing the store failed (I/O/lock). Exit class **Internal**.
- **`validation` / `prerequisite-missing`** (`VAL####`) — a hard prerequisite such as .NET SDK or Git is absent. Exit class **Validation**.
- **`validation` / `prerequisite-unsupported-version`** (`VAL####`) — a probe found the tool but outside the supported manifest range. Exit class **Validation**.
- **`validation` / `prerequisite-directory-unavailable`** (`VAL####`) — output/temp/cache/store directories cannot be used before mutation. Exit class **Validation**.
- **`validation` / `prerequisite-install-not-approved`** (`VAL####`) — a winget install plan was not explicitly approved or the plan digest changed. Exit class **Validation**.
- **`validation` / `prerequisite-winget-unavailable`** (`VAL####`) — Windows automatic install was requested but winget is absent, blocked, or unverifiable. Exit class **Validation**.
- **`validation` / `prerequisite-winget-failed`** (`VAL####`) — winget failed/cancelled or the post-install probe still fails. Exit class **Validation**.
- **`integrity` / `prerequisite-manifest-untrusted`** (`ITG####`) — the release-defined prerequisite manifest is missing, schema-invalid, hash-mismatched, or otherwise untrusted. Exit class **Integrity**.

Registered append-only in `errorcodes/registry.json`, then `errorcodes render` (regenerates `ErrorCodes.g.cs`) + `errorcodes check`. `describe` gains the new command groups + their options + exit classes.

## Command Availability

| Area | Command | Status |
| --- | --- | --- |
| Build / Test | `dotnet build`/`test scaffold-dotnet.slnx -c Release` | implemented (the PR's CI gate) |
| Tool fetch | `tools fetch [--rid] [--tool]` | implemented; **repurposed** to write into the store |
| Architecture | `architecture test` (+ two new families) | implemented; families NEW (this cycle) |
| Error codes | `errorcodes render` + `errorcodes check` | implemented; new store/prerequisite codes must be appended and checked |
| Scaffold | `new` (location-independent) | implemented (repo-bound); location-independence NEW |
| Prerequisite preflight | `hx prereq check` plus automatic preflight in `new` | NEW (advisory until built); fail-before-output-mutation |
| Windows prerequisite install | `hx prereq install` | NEW (advisory until built); Windows-only, winget-only, explicit operator approval |
| Store / health | `hx doctor` / `hx update`, `doti tools doctor` / `doti tools update` | **NEW (advisory until built)** |
| Release | `.github/workflows/release.yml`, GitHub Release | **NEW (advisory until built)** |

Planned-but-absent surfaces are advisory; no implemented gate is downgraded.

## Constitution Check (gate — after design)

PASS — re-evaluated. The store is owned by the CLI (Template Boundary intact); the security guard is preserved by adding a separate resolver (not loosening `ResolveInside`); prerequisite executable install metadata is trusted release payload only; binaries/exe stay CI artifacts (Public Hygiene); planned surfaces marked advisory (Bootstrap Honesty). No new violation → Complexity Tracking empty.

## Complexity Tracking

(None — Constitution Check passed before and after design.)

## Risks

- **Store/in-repo dual resolution** widens the resolution path — both branches need test coverage; mitigate with explicit store-hit, in-repo-fallback, and fail-closed unit tests at every site.
- **Embedded-payload staging drift** — the pre-pack target must mirror the path lists in `SourceVendor`/`DotiInstaller`/`ToolVendor`; if it misses a path, `new` breaks. Mitigate by sourcing the lists from the same constants and a smoke that runs `new` from the published exe.
- **Single-file self-extract + unsigned exe** may trip SmartScreen/AV (documented; signing deferred).
- **Concurrent `new`/`update`** mutating the shared store needs the `.store-manifest.json` file lock to stay transactional.
- **Sentrux grammar** is not re-fetchable — bundle is the only source; `doctor` must surface a clear error rather than degrade.
- **Local gate vs CI** — the full `gate run` needs the win-x64 binaries + grammar; commit/release go through PR + CI (the enforced gate), consistent with this session's prior PRs.
- **Prerequisite spoofing** — target repos or caches must not be able to redirect a no-coder to a malicious installer. Keep package/source metadata release-defined, include manifest identity in reports, and reject repo-local executable URL or winget overrides.
- **Winget side effects** — winget can require elevation, policy consent, UI, or a new shell session before PATH changes are visible. The install flow must report these states, rerun probes, and stop before `new` mutates output unless verification succeeds.
