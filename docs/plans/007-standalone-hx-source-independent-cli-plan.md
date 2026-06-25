# Plan: Standalone, Source-Free Hx CLI and Distribution

Spec: `docs/specs/007-standalone-hx-source-independent-cli.md`. Scope (operator-narrowed hotfix): two channels — a public NuGet.org .NET global tool (primary, all OS) and a Microsoft Store MSIX (Windows) — a source-free installed CLI, removal of Velopack, single-renderer hardening, the two update planes, and a fail-closed no-source-in-release gate. Self-contained GitHub app archives are deferred.

## Technical Context

- **Stack:** .NET 10 (`net10.0`), `System.CommandLine` 2.0.9, `Spectre.Console` 0.57.0, `Microsoft.Extensions.Configuration*` 10.0.8. Product CLI is `hx` (assembly `Hx.Scaffold.Cli`, `OutputType=Exe`).
- **To remove:** `Velopack` 1.2.0 package reference, `VelopackApp.Build().Run()` ([Program.cs:7](../../tools/Hx.Scaffold.Cli/Program.cs)), the `vpk`-backed `LocalReleaseService` path, and the vendored `vpk` tool (`tools/velopack/*`).
- **Root defect (verified):** installed `new`/`prereq`/`version` call `ScaffoldRoot.Resolve(Directory.GetCurrentDirectory())` ([New.cs:23](../../tools/Hx.Scaffold.Cli/ScaffoldCommands.New.cs), [Prereq.cs:23/59](../../tools/Hx.Scaffold.Cli/ScaffoldCommands.Prereq.cs), [Version.cs:19](../../tools/Hx.Scaffold.Cli/ScaffoldCommands.Version.cs)), whose marker is the source solution `scaffold-dotnet.slnx`; the release payload never ships the `.slnx`, so the installed CLI throws.
- **No-source violation (verified):** `store-release.yml:53` stages the full source tree via `git archive --format=tar HEAD` into the MSIX layout.
- **Renderer seam (verified):** `CliApp.Invoke` renders branded help only for invocations the hand-rolled `CliHelpRequestParser.TryParse` recognizes; everything else falls through to `root.Parse(args).Invoke()` → System.CommandLine's default unbranded help / `--version` / parse-error output. Three tool roots (Impact/Runner/Scaffold) + the template `Agent` each wire their own root.
- **Doti forward-copy (verified):** `DotiInstaller.Install` copies managed `.doti` subdirs with `File.Copy(overwrite:true)` ([DotiInstaller.cs:233](../../tools/Hx.Doti.Core/DotiInstaller.cs)) with no version/hash awareness; the conflict-aware `ManagedAssetManifest` + `CanonicalContentHasher` baseline exists but is wired only into legacy *removal*. There is no semantic payload version (only `int SchemaVersion` + a hardcoded `DotiGeneratedBy(8, …)`).
- **Audience premise (verified):** `hx`'s marquee commands shell out to the .NET SDK (`new` → `dotnet new`/`dotnet build`, `release` → `dotnet publish`), so the target audience already has the SDK — the global-tool channel adds no new prerequisite and a self-contained runtime bundle's primary benefit does not apply.
- **No undecided premises remain** (both spec clarifications resolved: public NuGet.org feed; GitHub archives deferred).

## Constitution Check (gate — before design)

PASS.
- **Deterministic Ownership / Bootstrap Honesty:** new enforcement is deterministic gate checks (no-Velopack-reference, no-source-in-payload, renderer-no-fallthrough); planned-but-absent packaging steps are marked advisory below, never reported as proof.
- **Template Boundary:** the template inherits the standalone/source-free/no-Velopack rules (FR-025); finishing logic stays in the CLI/core, static layout in the template.
- **Public Hygiene:** improves — drops the vendored `vpk` large binary; forbids source in any artifact.
- **Cross-Platform:** the global tool is the cross-OS path; no shell runners added (MSIX `makeappx` runs in CI YAML only).
- **Codified Cycle:** this work rides the cycle; no hand commits.
- **Channel Independence (Thin Adapter):** all new behavior lands in `*.Core` (`PayloadRoot` + release in `Hx.Scaffold.Core`, reconciliation in `Hx.Doti.Core`, renderer in `Hx.Cli.Kernel`); CLI deltas are wiring-only.

## Research (resolve unknowns)

### R1 — Drop Velopack; two channels
- **Decision:** Distribute `hx` as (1) a public NuGet.org .NET global tool (primary, Windows/Linux/macOS) and (2) a Microsoft Store MSIX (Windows). Remove the Velopack package, the `VelopackApp` hook, the root launcher stub, the vendored `vpk`, and the `vpk`-based `LocalReleaseService` path.
- **Rationale:** no Authenticode/Apple signing budget makes the unsigned Velopack `Setup.exe` the friction to avoid; the Microsoft Store re-signs MSIX server-side (trust without an operator cert) and delivers updates; the SDK is a hard runtime prerequisite of `hx`, so the global tool is frictionless and signing-free.
- **Alternatives rejected:** keep Velopack (unsigned-installer friction, cannot touch the Store, the root stub is the dropped-output bug); self-contained GitHub archives (unsigned macOS is Gatekeeper-blocked; deferred per spec FR-012).

### R2 — Source-free payload resolution (`PayloadRoot`)
- **Decision:** Add a `PayloadRoot` resolver in `Hx.Scaffold.Core`. Precedence: `HX_PAYLOAD_ROOT` override → `AppContext.BaseDirectory` (the host-executable directory, correct under single-file and a packed tool) → **fail closed**. The marker is a non-source `payload.manifest.json` shipped beside the executable (never `.slnx`). Installed `new`/`prereq`/`version` resolve via `PayloadRoot`; `ScaffoldRoot` (the `.slnx` walk) is retained but reachable only from explicitly source/developer-only callers.
- **Rationale:** `AppContext.BaseDirectory` is the single-file-correct primitive (`Assembly.Location` is empty / IL3000 under single-file); a dedicated descriptor avoids overloading a content file and carries the payload version needed by R5.
- **Alternatives rejected:** keep walking for `scaffold-dotnet.slnx` (the defect); cwd-first walk (breaks the moment `hx` runs outside a checkout); `Assembly.Location` (unreliable under single-file).

### R3 — Global-tool packaging + vendored-binary delivery
- **Decision:** Package `hx` as a **RID-specific self-contained .NET global tool** (`PackAsTool`) per declared RID (win-x64, linux-x64, osx-arm64/x64), bundling that RID's vendored tool binaries (`gitleaks`/`sentrux`/`gitversion`) + pinned manifests + templates + `.doti` as payload content beside the entry assembly (resolved by `PayloadRoot`). The MSIX bundles the win-x64 payload. Cross-RID provisioning for *generated* repos continues to use the existing hash-verified `ToolFetcher` (fetch-if-missing).
- **Rationale:** keeps in-scope installed commands (new/version/prereq/doti install) working from the bundled payload; avoids bloating each package with all RIDs. (This session's fact-checked review confirmed .NET 10 RID-specific self-contained tools carry native payloads.)
- **Alternatives rejected:** bundle every RID (package bloat); fetch all binaries on first run (network dependency for the core install path).
- **Availability:** the `dotnet pack`/`PackAsTool` + content-layout wiring is **planned/advisory** until implemented; exact RID-tool packaging invocation confirmed at implement.

### R4 — Renderer hardening (single renderer; no SCL fall-through)
- **Decision:** The kernel owns ALL help, `--version`, and parse-error rendering. Replace the fall-through `root.Parse(args).Invoke()` with kernel-owned handling: parse, then render branded help (`CliRenderer`) for any help request and a `CliResult` (ExitClass `Usage`/`Validation`) for parse errors through the shared renderer, suppressing System.CommandLine's built-in help/error output. Provide one `CliApp.Harden(root, meta, banner, tagline)` chokepoint invoked by all three tool roots and ported into the template `Agent`. Add a fail-closed kernel test enumerating every command in every root and asserting that unknown-option, missing-required-arg, `--version`, and leaf/group/root help never emit SCL default strings.
- **Rationale:** the hand-rolled `CliHelpRequestParser` + fall-through is the seam that leaks unbranded output (it misses `/h`, `--version`, and some leaf forms); routing through one chokepoint + a fail-closed test prevents regressions across all roots and generated repos.
- **Alternatives rejected:** `CommandLineBuilder`/`AddMiddleware` (removed in System.CommandLine 2.0.9); extending the hand-rolled parser (keeps reimplementing the parser and will drift again).
- **Availability:** the exact suppression binding (per-command `HelpAction` override vs post-`Parse` interception of `ParseResult.Errors`/`Action`) is confirmed at implement; both are within the 2.0.9 surface already used (`Parse`/`ParseResult`/`Invoke`).

### R5 — Doti payload-version reconciliation (FR-015)
- **Decision:** Stamp a semantic payload version into the bundled payload — `.doti/payload.json { payloadVersion, toolVersion, schemaVersion }` — and write it into the target repo on install. On `hx doti install --repo`, read the target's `payload.json` and branch: equal → verify/repair; repo older → migrate forward; repo newer → **refuse**. In the FORWARD copy, reuse the existing `ManagedAssetManifest` + `CanonicalContentHasher`: before overwriting each managed file, hash target vs the recorded baseline — match → overwrite + report `installed`/updated; diverged (operator-modified) → without `--force` report `blocked`/`preserved` and write the incoming file as `<file>.new`; with `--force` overwrite + report. Rename `DotiInstaller`'s `sourceRepoRoot` parameter to payload-root semantics and rephrase its "run from the scaffold repo root" error in installed-payload terms.
- **Rationale:** the conflict-aware machinery already exists (used for legacy removal) and the `preserved`/`blocked` result lists are already in `DotiInstallResult`; this makes the forward path truthful and honors the declared conflict policy instead of clobbering customizations.
- **Alternatives rejected:** blind overwrite (current behavior; destroys operator edits); full three-way merge (out of scope for the hotfix).

### R6 — No-source-in-release gate (FR-005/006)
- **Decision:** At packaging time (global-tool pack and MSIX layout assembly), enumerate the staged payload and fail closed if any source marker is present (`*.slnx`, `*.csproj`, `src/`, `scaffold/`, or a full source-tree archive). Replace `store-release.yml`'s `git archive --format=tar HEAD` with curated staging (published exe + `.doti` + win-x64 tool bins + `hx.config.json` + `payload.manifest.json`). Mirror the assertion in `LocalReleaseService` and a unit test.
- **Rationale:** structurally enforces the operator's #1 constraint; manual review is not proof.
- **Alternatives rejected:** rely on staging discipline (the current `git archive HEAD` shows discipline already failed).

### R7 — MSIX curated payload + console alias
- **Decision:** `store-release.yml` stages the curated payload and an `AppxManifest.xml` with a console `uap5:AppExecutionAlias` exposing `hx` on PATH; no in-app self-update path (Velopack already removed). `makeappx`/Store submission remain CI-only.
- **Rationale:** Store-delivered updates, PATH alias, source-free; `makeappx`/Windows SDK + Store credentials are not assumed on a developer machine.
- **Availability:** MSIX build/submission is **CI-only/advisory**; local proof is the global-tool install smoke + a source-free MSIX-layout check.

## Design

### Files likely to change
- `tools/Hx.Scaffold.Cli/Program.cs` — remove `VelopackApp.Build().Run()`; route through `CliApp.Harden`.
- `tools/Hx.Scaffold.Cli/Hx.Scaffold.Cli.csproj` — drop the `Velopack` package; add `PackAsTool`/tool packaging properties and payload content items.
- `tools/Hx.Scaffold.Cli/ScaffoldCommands.New.cs` / `.Prereq.cs` / `.Version.cs` — `ScaffoldRoot.Resolve(cwd)` → `PayloadRoot.Resolve()`; surface channel + command-mode.
- `src/Hx.Scaffold.Core/ScaffoldRoot.cs` (+ new `PayloadRoot.cs`) — `PayloadRoot` resolver on `AppContext.BaseDirectory` + `payload.manifest.json`; `ScaffoldRoot` demoted to source/developer-only.
- `src/Hx.Scaffold.Core/Release/*` — retarget `LocalReleaseService` from `vpk`/Velopack to producing the global-tool package + curated MSIX layout; per-channel source-free install smoke; the no-source packaging gate; delete `VelopackTool`/`VelopackArtifactClassifier`.
- `tools/Hx.Cli.Kernel/CliApp.cs` (+ `CliRenderer.cs`, retire/absorb `CliHelpRequest.cs`) — `CliApp.Harden`; own help/`--version`/parse-error rendering.
- `tools/Hx.Doti.Core/DotiInstaller.cs` (+ `ManagedAssets/*`) — payload-version stamp + reconciliation reusing the baseline hasher; group-help wording (FR-016).
- `tools/Hx.Tooling.Contracts/*` — channel + command-mode + per-channel install-proof fields on the release/describe contracts; `DotiInstallResult` reconciliation reasons (already has preserved/blocked).
- `tools/Hx.Gate.Core/*` — wire the no-Velopack, no-source, and renderer-no-fallthrough checks into `gate run`.
- `.github/workflows/store-release.yml` + `release.yml` — curated no-source MSIX payload + NuGet global-tool publish; remove Velopack/`vpk` staging and self-contained-archive publishing; source-free per-channel smoke before upload.
- `packaging/msix/AppxManifest.xml`, `packaging/PUBLISHING.md`, `packaging/STORE.md`, `packaging/winget/*`, `packaging/homebrew/*` — console alias; correct or remove manifests referencing non-produced artifacts.
- `scaffold/templates/dotnet-cli/*` (incl. its `Program.cs`/`Agent`) — inherit the rules (FR-025).
- `errorcodes/registry.json` (+ regenerated `ErrorCodes.g.cs`) — new diagnostics (below).
- README / CHANGELOG / `.doti/agent-context.md` / `.doti/core/skills.json` (re-render) — install/update via the two channels; drop Velopack/`vpk`/`release.json`-Velopack wording.

### Architecture delta
- **No new ArchUnitNET family and no Sentrux layer/boundary change.** All new logic lands in `*.Core` and the CLI delta is wiring-only, so the existing `cliSurfaceConfinement` / `cliDelegation` families already hold; removing the `Velopack` dependency only *shrinks* the Sentrux graph. `rules/architecture.json` and `.sentrux/rules.toml` are unchanged except for removing any Velopack-specific allowances if present.
- The design intent is instead encoded as **new deterministic gate checks** wired into `gate run` (R4/R6) and a kernel test (R4) — fail-closed, not advisory. `/06-doti-arch-review` validates the two engines still encode the same (unchanged) structural intent.

## CLI surface & error contract

Changed commands (no new top-level command; `doti install` gains an `--repo update/migrate` semantic and an optional `doti update --repo` alias):

- **Error codes** (registered in `errorcodes/registry.json`, append-only via `errorcodes check`):
  - `Integrity_PayloadRootMissing` — installed payload descriptor (`payload.manifest.json`) absent beside `hx`. Exit class **Integrity**.
  - `Validation_DotiRepoPayloadAhead` — target repo's recorded payload is newer than the installed tool's; refuse. Exit class **Validation**.
  - `Integrity_ReleaseArtifactContainsSource` — a produced artifact/payload contains a source marker; packaging fails closed. Exit class **Integrity**.
  - Parse/help errors reuse existing `Usage_*` / `Validation_*` codes mapped from `ParseResult` — no new code.
- **Exit class:** unchanged small set (Success 0, Usage 2, Validation 3, Integrity 4, Internal 70).
- **`describe` entry:** each command gains an installed-vs-source/developer mode flag; `version`/`describe` add the active distribution channel + its update mechanism (FR-022).
- **Envelope:** all output is the `CliResult` envelope (JSON-first); no direct console writes; help/errors flow through `CliRenderer`.
- **Channel boundary (core types the CLI delegates to):** `PayloadRoot` (`Hx.Scaffold.Core`) for resolution; `LocalReleaseService` (`Hx.Scaffold.Core`) for packaging + no-source gate + smoke; `DotiInstaller` (`Hx.Doti.Core`) for reconciliation; `CliApp`/`CliRenderer` (`Hx.Cli.Kernel`) for rendering. CLI methods only parse → delegate → render.

## Command Availability

| Area | Command | Status |
| --- | --- | --- |
| Build | `dotnet build .\scaffold-dotnet.slnx -c Release` | implemented |
| Test | `dotnet test .\scaffold-dotnet.slnx -c Release` | implemented |
| Gate | `dotnet run --project tools/Hx.Runner.Cli -- gate run --repo . --profile normal --json` | implemented |
| Architecture | `dotnet run --project tools/Hx.Runner.Cli -- architecture test --repo . --json` | implemented |
| Doti install (repo) | `hx doti install --repo <path> --json` | implemented; payload-version reconciliation (R5) is **planned** |
| Payload resolution | `PayloadRoot.Resolve()` source-free path | **planned** (advisory until implemented) |
| Global-tool pack | `dotnet pack` `PackAsTool` per RID | **planned/advisory** |
| MSIX build | `store-release.yml` `makeappx` curated layout | **planned; CI-only/advisory** |
| Release retarget | `hx release` → global-tool + MSIX layout + source-free smoke (no `vpk`) | **planned/advisory** |
| Renderer hardening | `CliApp.Harden` + fail-closed no-fallthrough test | **planned** |
| No-source gate | packaging-time source-marker check | **planned** |

No existing gate is downgraded; all planned items are advisory until their deterministic check exists.

## Constitution Check (gate — after design)

PASS. The design keeps logic in `*.Core` (Channel Independence), adds deterministic fail-closed checks rather than advisory-as-proof (Bootstrap Honesty), introduces no shell runners (`makeappx` is CI YAML), improves Public Hygiene (drops vendored `vpk`; forbids source in artifacts), and stays cross-platform (global tool on all three OSes). No principle is bent; **Complexity Tracking is empty.**

## Complexity Tracking

(none)

## Risks

- **Global-tool RID packaging** (R3): the exact `dotnet pack` `PackAsTool` + per-RID native-content layout for .NET 10 is confirmed at implement; risk that content layout under a packed tool needs `PayloadRoot` tuning — mitigated by the install smoke (FR-023).
- **Renderer suppression** (R4): a missed surface could still leak SCL default output — mitigated by the fail-closed test enumerating every command/option across all three roots + the template.
- **Cross-RID `hx new`** (R3): vendoring for a *generated* repo of a foreign RID relies on the network `ToolFetcher`; offline foreign-RID generation may degrade (advisory, pre-existing).
- **MSIX/Store smoke is CI-only** (R7): local proof is the global-tool install + a source-free MSIX-layout check; the full Store path is validated only in CI.
- **Broad blast radius:** touching `Program.cs`, the release service, and the kernel may make the affected-test planner escalate to a full-gate run at implement — expected, not a regression.
