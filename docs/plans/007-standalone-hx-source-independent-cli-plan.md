# Plan: Standalone, Source-Free Hx CLI and Distribution

Spec: `docs/specs/007-standalone-hx-source-independent-cli.md`. Distribution core: two channels — a public NuGet.org .NET global tool `Heurex.SpeckitDoti` (primary, all OS) and a Microsoft Store MSIX (Windows) — a source-free installed CLI, removal of Velopack, single-renderer hardening, the two update planes, and a fail-closed no-source-in-release gate. Self-contained GitHub app archives are deferred. **Expanded scope (007 program):** beyond the distribution core, 007 now also covers the enforcement substrate (Living-Spec evolution, ordered-task enforcement), profile-driven tiered/structure-agnostic gates, scaffold/doti separation + binary-fetch, and the Spec Kit workflow absorptions — built in 8 sequential phases per `docs/tasks/007-…-tasks.md` (decisions R8–R12 below).

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
- **Decision:** Add a `PayloadRoot` resolver in `Hx.Scaffold.Core`. Precedence: `HX_PAYLOAD_ROOT` override → `AppContext.BaseDirectory` (the host-executable directory, correct under single-file and a packed tool) → **fail closed**. The marker is a non-source `payload.manifest.json` shipped beside the executable (never `.slnx`). The descriptor is **integrity-verified on every path** (including the override) and resolution fails closed with an `Integrity` `CliResult` when it is absent, **unparseable, or an unsupported `schemaVersion`** (mirroring `StageModel.Load`); an active `HX_PAYLOAD_ROOT` emits a diagnostic. Installed `new`/`prereq`/`version` resolve via `PayloadRoot`; `ScaffoldRoot` is narrowed to its pure source-walk (`Find(startDirectory)`) for source/developer-only callers — its `AppContext.BaseDirectory`/`HX_PAYLOAD_ROOT` fallback branch is **deleted** so there is exactly one executable-anchored resolver (`PayloadRoot`) and one source-walk, preventing an installed command from "accidentally working" via the old fallback.
- **Rationale:** `AppContext.BaseDirectory` is the single-file-correct primitive (`Assembly.Location` is empty / IL3000 under single-file); a dedicated descriptor avoids overloading a content file and carries the payload version needed by R5.
- **Alternatives rejected:** keep walking for `scaffold-dotnet.slnx` (the defect); cwd-first walk (breaks the moment `hx` runs outside a checkout); `Assembly.Location` (unreliable under single-file).

### R3 — Global-tool packaging + vendored-binary delivery
- **Decision:** Package `hx` as a **RID-specific self-contained .NET global tool** (`PackAsTool`) per declared RID (win-x64, linux-x64, osx-arm64/x64), bundling the **pre-built `Hx.Scaffold.Templates` template nupkg** + pinned manifests + grammars + `.doti` as payload content beside the entry assembly (resolved by `PayloadRoot`). **Per-RID tool binaries (`gitleaks`/`sentrux`/`gitversion`) are NOT bundled — they are fetched + hash-verified on demand via `ToolFetcher` per R11/FR-033** (this supersedes the earlier bundle-the-bins wording). The MSIX bundles the same manifests+grammars payload.
- **Rationale:** keeps in-scope installed commands (new/version/prereq/doti install) working from the bundled payload; avoids bloating each package with all RIDs. (This session's fact-checked review confirmed .NET 10 RID-specific self-contained tools carry native payloads.)
- **Alternatives rejected:** bundle every RID (package bloat); fetch all binaries on first run (network dependency for the core install path).
- **Offline reconciliation (R3 ↔ R11):** moving binaries to on-demand fetch (R11/FR-033) means `hx new`'s built-in first smoke (which runs Gitleaks/Sentrux) now needs the network on a fresh install — the very dependency R3 rejected. Resolved per FR-032: the binary-dependent smoke steps **degrade to advisory/Skipped when binaries are absent and unreachable** (the host-RID binary MAY instead be bundled to keep the core path offline-clean — chosen at implement), and FR-023's `hx new` proof asserts success without those steps. The "core path stays bundled-payload-clean" claim is preserved by *not* hard-failing the core path on a missing fetched binary.
- **Availability:** the `dotnet pack`/`PackAsTool` + content-layout wiring is **planned/advisory** until implemented; exact RID-tool packaging invocation confirmed at implement.

### R4 — Renderer hardening (single renderer; no SCL fall-through)
- **Decision:** The kernel owns ALL help, `--version`, and parse-error rendering. Replace the fall-through `root.Parse(args).Invoke()` with kernel-owned handling: parse, then render branded help (`CliRenderer`) for any help request and a `CliResult` (ExitClass `Usage`/`Validation`) for parse errors through the shared renderer, suppressing System.CommandLine's built-in help/error output. Provide one `CliApp.Harden(root, meta, banner, tagline)` chokepoint invoked by all three tool roots and ported into the template `Agent`. The fail-closed kernel test MUST be **structural, not enumerative-by-omission**: it asserts that the *only* terminal action reachable on any parsed tree is the kernel-owned one (forbid any reachable `HelpAction`/default error action) and that every `RootCommand` constructed anywhere routes through `CliApp.Harden` — so a command added later (e.g. `doti bug assess`, `converge`) that isn't enrolled **fails the test** rather than silently regaining the fall-through. Coverage must include unknown-**subcommand** (`hx frobnicate`) and unknown-mid-path token (`hx doti frobnicate`) and `--version`, not only unknown-option (the current `CliHelpRequestParser` handles neither `--version` nor unknown-subcommand).
- **Suppression binding (resolved, not deferred):** spike and record the exact suppression point before T014 — remove/replace each command's default `HelpAction` and the implicit `VersionOption`, and intercept `ParseResult.Errors`/`Action` post-`Parse` — both within the System.CommandLine 2.0.9 surface already used. T014 cannot rely on a mechanism confirmed only at implement when T013 asserts the outcome.
- **Rationale:** the hand-rolled `CliHelpRequestParser` + fall-through is the seam that leaks unbranded output (it misses `/h`, `--version`, and some leaf forms); routing through one chokepoint + a fail-closed test prevents regressions across all roots and generated repos.
- **Alternatives rejected:** `CommandLineBuilder`/`AddMiddleware` (removed in System.CommandLine 2.0.9); extending the hand-rolled parser (keeps reimplementing the parser and will drift again).
- **Availability:** the exact suppression binding (per-command `HelpAction` override vs post-`Parse` interception of `ParseResult.Errors`/`Action`) is confirmed at implement; both are within the 2.0.9 surface already used (`Parse`/`ParseResult`/`Invoke`).

### R5 — Doti payload-version reconciliation (FR-015)
- **Two descriptors, one binding (resolved):** the bundled "current" version lives in the beside-exe `payload.manifest.json` (`PayloadDescriptor`); the per-repo record is `.doti/payload.json` (`RepoPayloadStamp { payloadVersion, toolVersion, schemaVersion }`). On install, `RepoPayloadStamp.payloadVersion` is copied **verbatim** from `PayloadDescriptor.payloadVersion` (an invariant test asserts equality); FR-015 reconciliation reads the bundled version from `PayloadRoot`'s descriptor and compares it to the target's `RepoPayloadStamp`. `Hx.Tooling.Contracts` defines **two distinct records**, not one ambiguous `PayloadManifest`.
- **Decision:** On `hx doti install --repo`, read the target's `.doti/payload.json` and branch: **absent (brownfield first install) → fresh install through the conflict-aware path (existing un-baselined `.doti` files reported `blocked`/`preserved`, never blind-overwritten)**; equal → verify/repair **running the same hash-baseline preservation as forward** (equal-version repair never clobbers operator edits); repo older → migrate forward; repo newer → **refuse**. In the forward/equal copy, reuse the existing `ManagedAssetManifest` + `CanonicalContentHasher`: before overwriting each managed file, hash target vs the recorded baseline — match → overwrite + report `installed`/updated; diverged (operator-modified) → without `--force` report `blocked`/`preserved` and write the incoming file as `<file>.new`; with `--force` overwrite + report. Reuse the legacy-removal path's existing "no baseline → blocked" handling; extend the reported states to cover **operator-deleted** (do not resurrect without `--force`) and **modified-and-removed-upstream** (report `blocked`, preserve the orphaned edit).
- **Safety (resolved):** every managed write (overwrite, migrate-forward, `.new` sidecar) routes through `RepositoryPathResolver.ResolveInside(repoRoot, …)` and fails closed on any `..`/absolute/symlink escape (the payload's path list is treated as untrusted). The install is made idempotent/resumable — stage-and-swap or write the baseline + `.doti/payload.json` transactionally **first**, so a crash mid-write re-runs to the same state instead of leaving files with a stale/absent baseline. Rename `DotiInstaller`'s `sourceRepoRoot` parameter to payload-root semantics, rephrase its "run from the scaffold repo root" error in installed-payload terms, and replace the Velopack-referencing failure string at `ScaffoldCommands.DotiInstall.cs:35`.
- **Rationale:** the conflict-aware machinery already exists (used for legacy removal) and the `preserved`/`blocked` result lists are already in `DotiInstallResult`; this makes the forward path truthful and honors the declared conflict policy instead of clobbering customizations.
- **Alternatives rejected:** blind overwrite (current behavior; destroys operator edits); full three-way merge (out of scope for the hotfix).

### R6 — No-source-in-release gate (FR-005/006)
- **Decision:** At packaging time (global-tool pack and MSIX layout assembly), enumerate the staged payload and fail closed if the **tool's own build tree** is present (`scaffold-dotnet.slnx`, `src/Hx.*`, the tool's `tools/*.csproj`, or a `git archive HEAD` tree) — while ALLOWING the template NuGet pack + manifest/grammar payload (product payload, not the tool's source). Replace `store-release.yml`'s `git archive --format=tar HEAD` with curated staging (published exe + `.doti` + win-x64 tool bins + `hx.config.json` + `payload.manifest.json`). Mirror the assertion in `LocalReleaseService` and a unit test.
- **Rationale:** structurally enforces the operator's #1 constraint; manual review is not proof.
- **Alternatives rejected:** rely on staging discipline (the current `git archive HEAD` shows discipline already failed).

### R7 — MSIX curated payload + console alias
- **Decision:** `store-release.yml` stages the curated payload and an `AppxManifest.xml` with a console `uap5:AppExecutionAlias` exposing `hx` on PATH; no in-app self-update path (Velopack already removed). `makeappx`/Store submission remain CI-only.
- **Rationale:** Store-delivered updates, PATH alias, source-free; `makeappx`/Windows SDK + Store credentials are not assumed on a developer machine.
- **Availability:** MSIX build/submission is **CI-only/advisory**; local proof is the global-tool install smoke + a source-free MSIX-layout check.

### R8 — Living-Spec evolution model (FR-027)
- **Decision:** Adopt a Living-Spec model: the spec is the contract, plan/tasks regenerate. A mid-cycle spec edit re-stamps specify/clarify and triggers re-clarify + re-analyze of dependents instead of the forward-only cascade blocking transitions; encode the relaxation in `Hx.Cycle.Core` doc-stage transition rules.
- **Enforced, not honor-system (resolved):** today freshness is per-artifact-hash only (`FreshnessEvaluator` hashes a stage's own `produces` artifact; the `plan` stage does **not** bind the upstream `spec.md` hash), so under a naive relaxation an operator could edit `spec.md`, skip re-analyze, and `plan`/`tasks` stay "fresh." The fix: a doc stage's proof MUST additionally bind the **content hash of each prerequisite artifact at stamp time**, so editing `spec.md` propagates staleness to `plan`/`tasks` (forcing re-analyze/re-stamp) even though their own files are untouched. The relaxation removes only the throwaway re-stamp cascade; it still refuses a genuinely stale dependent. A regression test asserts a post-stamp spec edit blocks transition until re-analyze re-stamps.
- **Rationale:** the forward-only cycle makes normal spec evolution (this very revision) painful; a defined model removes the friction without losing proof binding.

### R9 — Ordered task-completion enforcement (FR-028)
- **Decision:** Verify completed tasks form a valid phase/predecessor prefix; an out-of-order check fails closed (`Validation_TaskOutOfOrder`). Pairs with R12's scoped/resumable implement.
- **Greenfield, not "extend" (resolved):** `DotiTaskCompletion` today has **no** phase/predecessor/`[P]` concept (it parses `- [x] \`T###\`` lines and validates hashes only), and the tasks doc expresses order as prose `## Phase` headers — not machine-readable. So a **prior task** must define the machine-readable phase/predecessor schema in the tasks template (where does a task declare predecessors / its phase) for the checker to key on; ordering keys on `TaskId`, not `LineNumber`, so a pure reorder/renumber does not invalidate stamped completions. **Split the two halves:** (a) the predecessor/phase-prefix check is a clean extension of the existing parser; (b) the "prior phase's gate is green" half is redefined as "the prior phase's tasks are all checked + hash-valid" — answerable by `DotiTaskCompletion` itself **without** referencing a gate proof — to break the circularity (the gate already *includes* the task-completion step, so it must not transitively depend on its own output). The boundary is a transition (gate runs after a phase's last task lands, before the next phase's first), not a per-task precondition.
- **Rationale:** turns the "phases are sequential" directive into a machine gate so agents cannot cherry-pick.

### R10 — Tier-driven, structure-agnostic, bypass-safe gate layering (FR-029–031)
- **Tier ≠ lane (resolved — B1):** `gate run --profile` already means **lane** (`auto|advisory|normal|release`, `LaneResolver`); `hx new --profile` means **template profile**. The new gate-ladder axis is **tier**, read from `.doti/integration.json` (defined in `.doti/profiles/<name>/profile.json`), and `GateRunner.Run` gains a **separate tier/ladder parameter** — it is NOT read from the `--profile` argument. Surface text uses "tier"; the existing `integration.json` value `dotnet-cli` aliases to `dotnet-cli-heurex` and is migrated on `hx doti install --repo`. The three curated `profile.json` files and the `gates:{ "<step>": "enforced"|"advisory"|"skip" }` ladder schema do not exist yet and are authored in T006.
- **Decision:** the declared tier owns the gate ladder; `GateRunner` reads `profile.gates[step].mode` (defaulting a missing step to today's behavior) instead of hardcoding. Opinionated gates run only when the tier enables them; declared-enforced-but-**missing-or-malformed**-config = **Fail** (no delete/malformed bypass); not-enabled = Skip advisory.
- **GateProof binds the ladder (resolved — H1):** `GateProof` records the active tier + ladder coverage, and `GateProofValidator` **recomputes and verifies** that coverage exactly as it recomputes the affected-test plan — a proof minted under a narrower-than-declared ladder (a silent tier **downgrade**, e.g. editing `integration.json` to a weaker tier, or editing `profile.json` to set a gate advisory) is **refused at transition/release**. Decide explicitly whether tier/coverage are inside the canonical proof-hash boundary (they are — coverage is load-bearing) and treat pre-007 proofs without these fields as stale/re-run, not a crash.
- **Reduced generality (resolved — simpler-alternative):** FR-029/030 need only a `gates:{step→mode}` map, not a composition algebra. **Defer** R10's prepend/append/wrap layering (T008) until a real fourth profile needs to extend a third — consistent with the plan's own Complexity-Tracking discipline. Ship the three tiers as three concrete files.
- **Rationale:** lets doti install into existing repos as the enforcement layer without imposing the Heurex structure, while keeping enforcement non-bypassable.

### R11 — Scaffold/doti separation + vendored-binary fetch (FR-032–033)
- **Decision:** Ship the template as a standalone `dotnet new` NuGet pack (`Hx.Scaffold.Templates`); `hx new` = instantiate pack + `hx doti install --repo` + smoke. The doti/tool payload ships manifests + grammars only; per-RID binaries (Sentrux/Gitleaks/GitVersion) are fetched + hash-verified via the existing `ToolFetcher`.
- **`hx new` source-vendor gap (resolved — H3):** today `ScaffoldNewRunner`→`SourceVendor.Vendor` copies 12 `tools/Hx.*` **source** projects into every generated repo; a source-free installed `hx` has none. T021/FR-032 MUST decide and state what a generated repo's workflow tooling becomes — the leading option: the generated repo consumes `hx` as the installed global tool (`dotnet tool`/Store) and the template carries only its own product source, so `SourceVendor`'s tool-source copy is **removed**, not re-pointed. Whatever is chosen is stated in T021 (this is the load-bearing decision of the split).
- **Vendored-binary trust (resolved — B4):** the current pin is self-referential TOFU — `compute-tool-hashes.yml` downloads from the same upstream URL the fetcher uses, `sha256sum`s it, and pastes the result; there is no out-of-band attestation, and the workflow already pins Sentrux v0.5.10 while the manifest ships v0.5.11. Fix: the fetch verifies an **upstream-published signed checksum / artifact attestation** (Gitleaks/GitVersion publish a `checksumUrl`; the Heurex Sentrux fork must publish signed checksums or GitHub artifact attestations), the manifest records hash **provenance**, and a gate fails when the hash-capture tool version ≠ the manifest version. Fetch is concurrency-safe (unique temp + atomic rename) and wraps network failure into a structured `CliResult` (not a raw `HttpRequestException`). Remove the stale `tools/velopack/...` entry from `ToolFetcher.ManifestRelativePaths` (else `FetchAll` fails for all tools).
- **Rationale:** cleanly separates the no-source rule (tool build tree forbidden) from required payload (template pack + manifests/grammars); shrinks the tool package; unifies "new repo" and "add doti" on the `hx doti install` path.

### R12 — Spec Kit workflow absorptions (FR-034–040)
- **Decision:** Absorb Spec Kit's structure/methodology while keeping doti's enforcement: the assess→fix→test bug mini-cycle; the URL trust policy; context-budget/resumable implement; user-story-priority + MVP-first templates; one-source→many-agent transpilation; `converge`; and the "unit tests for English" checklist bar — each landed as proof-bound doti behavior, not honor-system prose.
- **Rationale:** Spec Kit (reviewed at `D:\temp\spec-kit`) is honor-system with no enforcement; its methodology is valuable but its gates are not — doti adds the teeth.
- **Availability:** Phase-6 planned work; advisory until built.

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
- **No new ArchUnitNET family and no Sentrux layer/boundary change.** All new logic lands in `*.Core` and the CLI delta is wiring-only, so the existing `cliSurfaceConfinement` / `cliDelegation` families already hold; removing the `Velopack` dependency only *shrinks* the Sentrux graph. `rules/architecture.json` and `.sentrux/rules.toml` are unchanged except for removing any Velopack-specific allowances if present. **(Distribution scope R1–R7 only — the expanded scope R8–R12 DOES add new commands/core types; its architecture delta is in the [Expanded-Scope Design](#expanded-scope-design-r8r12) section below.)**
- The design intent is instead encoded as **new deterministic gate checks** wired into `gate run` (R4/R6) and a kernel test (R4) — fail-closed, not advisory. `/06-doti-arch-review` validates the two engines still encode the same (unchanged) structural intent.

## CLI surface & error contract

Changed commands (no new top-level command; `doti install` gains install/repair/migrate/update reconciliation in a `--repo` target — no separate `doti update` alias, per FR-016):

- **Error codes** (registered in `errorcodes/registry.json`, append-only via `errorcodes check`):
  - `Integrity_PayloadRootMissing` (registry `{category:integrity, suffix:payload-root-missing}`) — installed payload descriptor absent/unparseable/unsupported-schema beside `hx`. Exit class **Integrity**.
  - `Integrity_DotiRepoPayloadAhead` (registry `{category:integrity, suffix:doti-repo-payload-ahead}`) — target repo's recorded payload is newer than the installed tool's; refuse. **Exit class Integrity** (corrected from Validation per the data-contract lens: it sits beside the two `Integrity_*` siblings in this feature and is a non-bypassable safety refusal protecting on-disk operator data, like the managed-asset family `ITG0006/0007` — not the `tool-store-version-too-new` Validation precedent).
  - `Integrity_ReleaseArtifactContainsSource` — a produced artifact/payload contains a source marker; packaging fails closed. Exit class **Integrity**.
  - `Integrity_PayloadDescriptorInvalid` — descriptor present but unparseable or unsupported `schemaVersion`. Exit class **Integrity** (FR-003 hardening).
  - Note: registry entries use the `category`/`suffix` shape (rendered to `PREFIX00NN` + name `category.suffix`); the `PascalCase_Name` forms here are the generated C# enum names, and the registration tasks MUST be written in registry shape to pass `errorcodes render`/`check`.
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

| Violation | Why needed | Simpler alternative rejected because |
| --- | --- | --- |
| Living-Spec (R8) relaxes the forward-only doc-stage transition rule | The forward-only cycle blocks normal spec evolution on uncommitted upstream edits (hit repeatedly this session) | A strict forward-only cycle forces throwaway re-work / out-of-band commits for any post-specify spec change; Living-Spec keeps diff-bound proof binding while permitting re-clarify/re-analyze on a spec edit |

## Expanded-Scope Design (R8–R12)

The Design / CLI-surface / Command-Availability / Architecture-delta / Constitution-Check sections above cover the distribution scope (R1–R7). The expanded scope adds:

### Files likely to change (R8–R12)
- **R8 Living-Spec / R9 ordered-task enforcement:** `tools/Hx.Cycle.Core/` (doc-stage transition relaxation; `Tasks/DotiTaskCompletion.cs` order check), `tools/Hx.Gate.Core/GateRunner.cs` (task-completion step).
- **R10 profile layering / structure-agnostic / composition:** `tools/Hx.Gate.Core/GateRunner.cs`, `.doti/profiles/*/profile.json`, `.doti/integration.json`, `tools/Hx.Tooling.Contracts/` (GateProof profile+coverage).
- **R11 scaffold/doti separation + fetch:** `src/Hx.Scaffold.Core/TemplateGenerator.cs`, `ScaffoldNewRunner.cs`, `scaffold/Hx.Scaffold.Templates.csproj`, `tools/Hx.Doti.Core/`.
- **R12 absorptions:** new `*.Core` services + commands/skills for the bug mini-cycle (assess/fix/test), `converge`, the shared URL trust policy, context-budget/resumable implement, template methodology, multi-agent transpilation (skill renderer), checklist depth — across `tools/Hx.Doti.Core`, `tools/Hx.Runner.Cli`, `.doti/core/skills.json`, `.doti/core/templates/`.

### CLI surface & error contract (R8–R12)
New/changed surface: `doti bug assess|fix|test`, `doti converge`, the arch-context emitter (`Hx.Impact.Cli plan --for arch-review --json` — chosen spelling, reusing the planner, **not** a new `doti arch-context`), the `/doti-upgrade` skill, the gate's task-completion + tier steps. **Each new command names the `*.Core` type it delegates to** (CLI = parse → delegate → `CliResult`): `doti bug assess/fix/test` → a `BugCycleService` in `Hx.Doti.Core` (assess read-only/verdict, fix the only writer bound to the assessment, test honest-verify — distinct entry points enforcing the writer boundary); `doti converge` → a `ConvergeService` in `Hx.Doti.Core`; the tier ladder → `GateRunner` reading a `TierLadder`/profile resolver in `Hx.Gate.Core`; ordered-task → `DotiTaskCompletion` in `Hx.Cycle.Core`; the URL policy → a shared `UrlTrustPolicy` in a `*.Core` hardening type. The new command surface MUST be enrolled in the existing `cliSurfaceConfinement`/`cliDelegation` families (the architecture test asserts the family-id list verbatim — `cliSurfaceConfinement`, `cliDelegation` — so the README/skills/agent-context rewrites in T043/T044 MUST keep those two ids verbatim or `Architecture_guidance_matches_declared_families` fails).
**New error codes — enumerate ALL before their producing task** (registry `category`/`suffix` shape, append-only, frozen by `errorcodes check`): `{validation, task-out-of-order}` (R9), `{integrity, profile-gate-missing-config}` (R10 — Integrity: a non-bypassable enforcement refusal), `{validation, url-blocked}` (R12 trust policy), `{validation, bug-fix-unbound}` (a fix not bound to an assessment), `{validation, bug-assessment-missing}`, `{validation, converge-input}` (converge precondition). These are added to the registration task (T005 / per-producer) — they MUST NOT be invented ad-hoc at implement, since the first ship freezes name + exit class forever. Each new command gains a `describe` entry; exit classes stay the existing small set.

### Command Availability (R8–R12) — all **planned/advisory** until built
profile-driven gate ladder; ordered-task enforcement; Living-Spec evolution; bug mini-cycle; `converge`; multi-agent transpilation.

### Architecture delta (R8–R12) — corrects the distribution-scope claim
The expanded scope **does** add CLI surface + core types (new commands + services). They MUST stay confined to `*.Core` (CLI = wiring-only) so the existing `cliSurfaceConfinement`/`cliDelegation` families cover them; **no new ArchUnitNET family is expected, but the new command surface must be enrolled in those families** and the new core types placed within existing Sentrux layers without introducing cycles. The tier-driven gate ladder makes the gate config-driven (not hardcoded) — a behavior change, not a layer change. `/06-doti-arch-review` MUST validate: new commands confined to `*.Core`; the bug-cycle / `converge` / tier model introduce no new layer cycle.

**Contract-shape decisions (data-contract lens):** retargeting `LocalReleaseResult` off Velopack is a **breaking change to `release.identity.json`** — make the rename **additive/channel-neutral** (`PackageId`/`Channel`/`Artifacts` rather than `Velopack*`), explicitly decide the fate of the now-orphaned frozen code `ITG0008 integrity.velopack-artifact-missing` (cannot be renamed — repurpose its message or leave it orphaned-but-frozen), and state whether 007 bumps the single shared `JsonContractDefaults.SchemaVersion` or accepts an unversioned identity-format change. `GateProof` gaining tier + coverage changes its canonical hash — decide the fields are **inside** the canonical boundary and treat pre-007 persisted proofs as stale/re-run (test asserts an old proof shape is re-run, not NRE'd). The two payload descriptors reuse-or-fork the shared `SchemaVersion` deliberately (reusing the global constant means an unrelated contract bump silently churns the descriptor schema).

### Constitution re-check (R8–R12)
PASS with one justified deviation (Complexity Tracking): the absorptions land as proof-bound `*.Core` behavior (Channel Independence); ordered-task + profile enforcement are deterministic fail-closed gates (Bootstrap Honesty, Codified Cycle); no shell runners (Cross-Platform); transpilation stays single-source render. The Living-Spec model (R8) relaxes the forward-only cycle — justified, not a dilution: it preserves diff-bound proofs and only permits re-clarify/re-analyze on a spec edit instead of blocking.

## Risks

- **Global-tool RID packaging** (R3): the exact `dotnet pack` `PackAsTool` + per-RID native-content layout for .NET 10 is confirmed at implement; risk that content layout under a packed tool needs `PayloadRoot` tuning — mitigated by the install smoke (FR-023).
- **Renderer suppression** (R4): a missed surface could still leak SCL default output — mitigated by the fail-closed test enumerating every command/option across all three roots + the template.
- **Cross-RID `hx new`** (R3): vendoring for a *generated* repo of a foreign RID relies on the network `ToolFetcher`; offline foreign-RID generation may degrade (advisory, pre-existing).
- **MSIX/Store smoke is CI-only** (R7): local proof is the global-tool install + a source-free MSIX-layout check; the full Store path is validated only in CI.
- **Broad blast radius:** touching `Program.cs`, the release service, and the kernel may make the affected-test planner escalate to a full-gate run at implement — expected, not a regression.
