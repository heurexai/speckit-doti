<div align="center">

<img src="media/doti-banner.jpg" width="820" alt="speckit-doti — the agentic .NET spec-driven development starter kit, by Heurex" />

# speckit-doti

**A .NET starter kit for spec-driven development with AI coding agents.**

`dotnet new hx-dotnet-cli` scaffolds a complete, **compiling** .NET 10 solution — pure domain library + agent-first CLI + unit & [ArchUnitNET](https://github.com/TNG/ArchUnitNET) architecture tests + dual-engine architecture gating — **and installs a deterministic, command-enforced workflow** (doti) based on [GitHub Spec Kit](https://github.com/github/spec-kit). Your AI agent (Claude Code, Codex) then builds inside guardrails that won't let it skip a step or commit unverified work.

[![.NET](https://img.shields.io/badge/.NET-10.0-C9A961?style=flat-square&labelColor=1A1F4D)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-C9A961?style=flat-square&labelColor=1A1F4D)](LICENSE)
[![Based on Spec Kit](https://img.shields.io/badge/based_on-Spec_Kit-C9A961?style=flat-square&labelColor=1A1F4D)](https://github.com/github/spec-kit)
[![Sentrux fork](https://img.shields.io/badge/vendors-Sentrux_(Heurex_fork)-C9A961?style=flat-square&labelColor=1A1F4D)](https://github.com/heurexai/sentrux)

</div>

---

## Why .NET devs use it

- **One command, a project that builds.** `dotnet new hx-dotnet-cli` emits a layered .NET 10 solution — domain library, agent-first CLI, xUnit tests, ArchUnitNET architecture tests, security analyzers, gate configs — that compiles and tests **green on day one**. No wiring, no "TODO: add tests later."
- **Architecture rules that fail the build.** [ArchUnitNET](https://github.com/TNG/ArchUnitNET) thin-CLI families plus a vendored [Sentrux](https://github.com/heurexai/sentrux) boundary engine run in the gate. Layering drift is a red build, not a code-review nit.
- **Guardrails your agent can't talk its way around.** Unlike Spec Kit's advisory markdown prompts, every doti stage is backed by a CLI command that emits a hash-bound proof, and the gate ladder is **fail-closed** — Doti-owned transition and release paths refuse work they did not verify.
- **Agent-first by design.** Every operation is a JSON-first command, so Claude Code or Codex can drive, verify, and report on the whole spec → ship loop with no human in the path. Human help uses one shared rich/plain renderer across root menus and subcommands (`--help-mode plain` / `--plain-help` / `NO_COLOR` for ANSI-free output).

## 30-second quickstart

```bash
# install the CLI (any OS, needs the .NET 10 SDK) — or the Microsoft Store on Windows
dotnet tool install --global Heurex.SpeckitDoti

# scaffold a new agent-first .NET solution (doti is installed automatically)
hx new --name Acme.Widget --output ./Acme.Widget --company Acme --agents codex,claude

# it builds and tests green immediately
dotnet build ./Acme.Widget/Acme.Widget.slnx -c Release
dotnet test  ./Acme.Widget/Acme.Widget.slnx -c Release
```

Then point your agent at the slash-commands and let the gates do the enforcing:

```
/01-doti-specify → /02-doti-clarify → /03-doti-plan → /04-doti-tasks → /05-doti-analyze
→ /06-doti-arch-review → /07-doti-implement → /08-doti-drift-review → /09-doti-release
```

Full setup (build the toolkit, install hooks, run the gate) is in [Get started](#get-started).

---

## Table of contents

- [What is speckit-doti?](#what-is-speckit-doti)
- [Why we built this](#why-we-built-this)
- [The scaffold (the starter kit)](#the-scaffold-the-starter-kit)
  - [What `dotnet new` generates](#what-dotnet-new-generates)
  - [The generated project's architecture](#the-generated-projects-architecture)
  - [The toolkit's own architecture](#the-toolkits-own-architecture)
- [The doti workflow](#the-doti-workflow)
- [Sentrux — our fork](#sentrux--our-fork)
- [Customise it to your process](#customise-it-to-your-process)
- [Install profiles & tiers](#install-profiles--tiers)
- [Get started](#get-started)
- [CLI reference](#cli-reference)
- [The deterministic gate](#the-deterministic-gate)
- [Status](#status)
- [Acknowledgements](#acknowledgements)
- [Contributing and support](#contributing-and-support)
- [License](#license)

---

## What is speckit-doti?

It's two things in one repo, and **the scaffold (the starter kit) is the headline**:

1. **A scaffold / `dotnet new` template.** `dotnet new hx-dotnet-cli` (or `Hx.Scaffold.Cli new`) generates a ready-to-build .NET 10 solution — a pure domain library, an agent-first CLI, xUnit tests, ArchUnitNET architecture tests, build-time security analyzers, and the rules/configs for the gate — **and installs the doti workflow into it**. It compiles and tests green on day one.

2. **A workflow.** doti is a Spec Kit-based, spec-driven development process (`specify → … → drift-review → release`) where every stage emits a command-backed, hash-bound proof and the gate ladder is fail-closed. It rides on top of whatever the scaffold generated.

In short: **speckit-doti is the starter kit; doti is the process it installs.** You get a project that builds immediately and is wired for an AI coding agent to develop it safely — spec-first, architecture-gated, and unable to commit work it didn't verify.

## Why we built this

[GitHub Spec Kit](https://github.com/github/spec-kit) showed that **specify → plan → tasks → implement**, driven by an AI agent, beats vibe-coding. We use it and believe in it. But two gaps stopped us shipping real .NET work with it.

### vs. plain Spec Kit — the workflow is *enforced*, not suggested

Spec Kit's commands are markdown prompts and its quality checks are prose. Nothing stops an agent — working largely unattended — from skipping clarification, ignoring the plan, or committing unverified work. **"The prompt told it to" is not "the tooling made it."** doti backs every stage with a deterministic CLI command and a hash-bound proof, and makes transition/release proof fail closed. (Full comparison: [doti vs Spec Kit](#doti-vs-spec-kit).)

### vs. "just use an AI assistant" — guardrails are the answer

Handing a capable agent a `.csproj` and hoping is how you get plausible code that quietly violates your architecture, drops the test it couldn't make pass, and commits anyway. speckit-doti replaces hope with **enforcement**: architecture tests that fail the build, a deterministic gate ladder, and coded transition/release paths that re-verify fresh passing proof before anything lands. The agent moves fast *inside* boundaries it cannot cross.

There was also **no opinionated, _compiling_ .NET starting point** that bakes the workflow, the architecture rules, and the gates into a project you can build on the first commit. speckit-doti is that starting point — and it takes an opinionated stance on three things Spec Kit leaves to judgement:

1. **Process** — the cycle is *stamped and checked*, not suggested.
2. **Architecture & design** — declared boundaries are *gated* on every change, by two engines.
3. **A comprehensive, agent-first CLI** — every operation is a JSON-first command, so an *agent* can drive, verify, and report on the whole loop with no human in the path.

---

## The scaffold (the starter kit)

This is where most of the value lives.

### What `dotnet new` generates

```bash
hx new --name Acme.Widget --output ./Acme.Widget --company Acme --agents codex,claude
```

produces a complete, buildable solution **and installs doti into it**:

```
Acme.Widget/
├─ src/
│  ├─ Acme.Widget/                      # domain library — pure, no CLI/IO/network deps
│  └─ Acme.Widget.Cli/                  # agent-first CLI — Program + the Agent host
├─ test/
│  ├─ Acme.Widget.Tests/               # unit tests (xUnit v3)
│  └─ Acme.Widget.Architecture.Tests/  # ArchUnitNET architecture gates
├─ rules/                               # architecture / hygiene / security / sentrux contracts
├─ .sentrux/                            # Sentrux boundary config
├─ .doti/ + agent skill files           # the doti workflow, installed for codex/claude
├─ global.json                          # SDK pin
└─ Acme.Widget.slnx
```

```bash
dotnet build Acme.Widget.slnx -c Release   # green
dotnet test  Acme.Widget.slnx -c Release   # unit + architecture tests pass
```

The template is registered as **`hx-dotnet-cli`** ("Heurex .NET CLI scaffold — library + CLI + tests"). `--agents` controls which agents' skill files are rendered.

### The generated project's architecture

The point of the scaffold is that good design is **enforced from the first commit**, not aspired to:

- **Layered & dependency-directed.** A pure domain `library` that may depend on nothing, and a `CLI` that may depend only on the library — enforced by tests, not just documented.
- **Agent-first CLI.** Commands return a `CliResult` envelope — _Status / Identity / Diagnostics / Direction / Result_, plus _Effects / Progress_ — serialized as LF-normalized, JSON-first output. A single `Agent` host is the only type allowed to write to the console, so output is one machine-consumable chokepoint. Structured error codes (`<PREFIX><NNNN>`), a `describe` command for self-description, and NDJSON streaming come built in.
- **Architecture gated by code.** `test/*.Architecture.Tests` runs the ArchUnitNET families declared in `rules/architecture.json` on every `dotnet test`; generated projects and the toolkit currently dog-food the thin-CLI Channel Independence checks:

  | family | enforces |
  | --- | --- |
  | `cliSurfaceConfinement` | the CLI carries no business-logic types (`*Service`/`*Repository`/…); logic lives in the library (thin adapter) |
  | `cliDelegation` | command-dispatch types delegate to core/library code instead of carrying business logic |

  Sentrux enforces the path/layer boundary graph and cycle constraints from `.sentrux/rules.toml`; `.NET` analyzers enforce the build-time security rules.

- **Security at the build.** .NET analyzer security rules (CA3xxx/CA5xxx) are errors via `Directory.Build.props` — the build itself is the SAST gate.

### The toolkit's own architecture

The repo that produces all of the above is itself a layered .NET 10 solution (and it dog-foods doti):

- **`Hx.Cli.Kernel`** — the agent-first CLI kernel (the `CliResult` envelope, the JSON writer, the error-code registry + stability check, `describe`) shared by every tool *and* the template.
- **`Hx.Tooling.Contracts`** — shared contracts.
- **Engine cores** (no console/IO concerns): `Hx.Scaffold.Core` (generation, vendoring, doti install), `Hx.Doti.Core` (renders skills from one source), `Hx.Cycle.Core` (cycle-state proofs), `Hx.Gate.Core` (the ladder + `GateProof`), `Hx.Impact.Core` (affected-test planning), `Hx.Sentrux.Core`, `Hx.Security.Core`, `Hx.Version.Core`, `Hx.Runner.Core`.
- **Thin CLIs**: `Hx.Runner.Cli` (the workhorse — gate / architecture / sentrux / hygiene / version / security / doti), `Hx.Impact.Cli` (`plan`), `Hx.Scaffold.Cli` (`new`).
- **`scaffold/`** — the `hx-dotnet-cli` template pack.
- **`tools/{gitleaks,sentrux,gitversion}`** — vendored, pinned, SHA-256-verified binaries.

---

## The doti workflow

doti is the process layer the scaffold installs. It ships as numbered slash-command skills (Claude: `/01-doti-specify`; the same skills render for Codex). **Always start with `01-Specify` and run them in order** — each stage's proof is a prerequisite for the next, and `doti cycle check` enforces that chain fail-closed.

```mermaid
%%{init: {'theme':'base','themeVariables':{'fontFamily':'Space Mono, ui-monospace, monospace','lineColor':'#C9A961','primaryTextColor':'#F4F6F9'}}}%%
flowchart LR
  S[01 specify]:::shared --> C[02 clarify]:::shared --> P[03 plan]:::shared --> T[04 tasks]:::shared --> A[05 analyze]:::shared --> AR[06 arch-review]:::doti --> I[07 implement]:::shared --> DR[08 drift-review]:::doti --> R[09 release]:::doti
  classDef shared fill:#1A1F4D,stroke:#2D3575,color:#F4F6F9;
  classDef doti fill:#1A1F4D,stroke:#C9A961,color:#C9A961;
```

<sub>Gold-bordered stages are **doti-only** — no Spec Kit equivalent. The rest map to Spec Kit commands.</sub>

### doti vs Spec Kit

|                    | GitHub Spec Kit                          | doti (in speckit-doti)                             |
| ------------------ | ---------------------------------------- | -------------------------------------------------- |
| Delivery           | Python CLI that bootstraps templates     | A .NET scaffold that generates a compiling project |
| Commands           | Markdown prompts the agent should follow | Prompts **backed by** deterministic CLI tools      |
| Quality checks     | Advisory prose                           | Fail-closed gates that block the build / commit    |
| Process state      | Implicit                                 | Explicit, hash-bound stage proofs + freshness      |
| Commit             | Up to the agent                          | Owned by coded Doti transition/release paths       |
| Architecture       | Out of scope                             | First-class, dual-engine, gated on every change    |

We kept what makes Spec Kit good — the spec-first sequence, clarification before planning, cross-artifact analysis — and built the enforcement layer underneath it.

### The stages

> **Constitution (not a command).** doti keeps project principles in `.doti/core/memory/constitution.md`, referenced by the plan and analyze stages. _Spec Kit equivalent:_ `/speckit.constitution` — doti's is a static, version-controlled document instead of a generative command, so the principles are a stable input rather than something regenerated each run.

1. **`/01-doti-specify`** — Author the numbered feature spec → `docs/specs/{NNN-feature}.md` (for example `docs/specs/001-numbered-specs.md`; use the same full `NNN-feature` slug for cycle state, plan, and tasks).
   _Spec Kit:_ `/speckit.specify`. Same role; doti hash-stamps the stage so later stages can detect if the spec shifted underneath them.

2. **`/02-doti-clarify`** — Resolve ambiguities by asking blocking questions one at a time, folding each answer into `## Clarifications`.
   _Spec Kit:_ `/speckit.clarify`. Same intent; doti additionally enforces a fixed **operator-question format** (Context / Why it matters / Options / Recommendation / Assumptions / Confidence) with an evidence requirement.

3. **`/03-doti-plan`** — Produce the implementation plan → `docs/plans/{feature}-plan.md`.
   _Spec Kit:_ `/speckit.plan`. Same role; the plan is a hash-bound artifact in the cycle.

4. **`/04-doti-tasks`** — Break the plan into executable tasks → `docs/tasks/{feature}-tasks.md`.
   _Spec Kit:_ `/speckit.tasks`. Same role. (No `taskstoissues` equivalent — tasks stay local and gated.)

5. **`/05-doti-analyze`** — Cross-artifact consistency and coverage review across spec / plan / tasks.
   _Spec Kit:_ `/speckit.analyze`. Same intent.

6. **`/06-doti-arch-review`** — _(doti-only)_ Review the architecture impact and verify the ArchUnitNET + Sentrux configs actually measure the intended boundaries — **before any code is written**.
   _Spec Kit:_ no equivalent.

7. **`/07-doti-implement`** — Execute the tasks.
   _Spec Kit:_ `/speckit.implement`. Same role; doti attaches an explicit engineering-discipline contract (root-cause not symptom, prove every claim, no silenced checks, a 95%-confidence bar).

8. **`/08-doti-drift-review`** — _(doti-only)_ Compare the actual diff against the approved plan/design and check for drift between source assets and installed files.
   _Spec Kit:_ loosely related to `/speckit.converge`, but it's a **gate** that catches divergence before you can commit, not a task-appender.

9. **`/09-doti-release`** — _(doti-only)_ Run the release gate, produce the command-backed `hx release --major|--minor|--patch` proof, then push and verify the Doti-owned release tag through CI.
    _Spec Kit:_ no equivalent.

> Spec Kit's `/speckit.checklist` has no dedicated doti command — doti folds completeness / clarity / consistency checking into `/doti-analyze` and the gate.

---

## Sentrux — our fork

doti's architecture gate runs on **two** independent engines: [ArchUnitNET](https://github.com/TNG/ArchUnitNET) (in-code rules) and [Sentrux](https://github.com/heurexai/sentrux) (a boundary/quality sensor for AI agents). We maintain a **fork** — [github.com/heurexai/sentrux](https://github.com/heurexai/sentrux) — because the scaffold needs .NET/C#-aware analysis that upstream didn't provide:

- **.NET project-reference edges** — `<ProjectReference>` entries feed the dependency graph.
- **C# type dependency edges** — type-level dependencies inferred from C# source.
- **`.sentruxignore`** — gitignore-syntax exclusions that apply **even to git-tracked files**, so generated `scaffold/templates/` content is kept out of the scan.
- **Cycle edge diagnostics** — per-cycle edge chains pinpoint the exact imports forming each dependency cycle.
- **Fork build identity** — an embedded Windows version resource; the gate verifies the binary reports `Heurex fork`.

The fork is MIT-licensed, released at [heurexai/sentrux/releases](https://github.com/heurexai/sentrux/releases). doti ships the **manifest, not the binary**: the pinned `tools/sentrux/*.version.json` and the C# grammars travel in the package/repo, while the platform binary is **fetched on demand and SHA-256-verified against that manifest** (`hx tools fetch`; currently `v0.5.11` for declared RIDs). The same manifest-ships/binary-fetches model covers Gitleaks and GitVersion, so the distribution stays lean (no ~100 MB of binaries) and every fetch is hash-gated. `sentrux verify` re-checks the manifest, hash, grammar, and fork identity before every gate run; a RID with no matching asset fails closed.

---

## Customise it to your process

doti is single-sourced and configurable — adapt it to your team without forking the toolkit:

- **The workflow / skills** — edit `.doti/core/skills.json` (stage names, intents, next-stage hints, the operator-question protocol) and re-render with `doti render-skills`. Installed skill files are generated — don't hand-edit them.
- **The stage model** — `.doti/core/workflows/doti/workflow.yml` defines stage order, the artifact each stage `produces`, and prerequisites.
- **Architecture rules** — edit a project's `rules/architecture.json` + its ArchUnitNET tests, and `.sentrux/rules.toml` (layers / boundaries / constraints) for the Sentrux engine.
- **Gate strictness** — `gate run --profile <auto|advisory|normal|release>` tunes the ladder per context (dev vs release).
- **Principles** — `.doti/core/memory/constitution.md` holds the governing principles the plan/analyze stages read.
- **Agents** — `--agents codex,claude` renders the workflow for the agents you actually use.

---

## Install profiles & tiers

doti has **two entry points** and **three enforcement tiers**, so a repo adopts exactly as much governance as it's ready for.

### Two ways in

- **`hx new` (scaffold)** — generates a *new* repo: a compiling .NET 10 solution (pure library + agent-first CLI + tests) wired for the full Heurex structure, with doti installed. The Tier 3 starting point.
- **`hx doti install --repo <path>` (install)** — installs the doti workflow into an *existing* repo. It never touches your source; it lays down the `.doti` payload, renders the agent skills, and arms the insurance hook. Choose the tier that fits the repo.

### Three tiers — what each gate-enforces

A tier sets which steps of the gate ladder are **enforced** vs **skipped**. The cycle proofs + chokepoints are always on; the structural gates scale up:

| Tier | For | Adds on top of the baseline | Sentrux + ArchUnitNET |
| --- | --- | --- | --- |
| **`workflow-only`** | any repo, any language | nothing — just the spec → ship cycle | skipped |
| **`dotnet-lib`** | an existing .NET library/repo | the standard .NET gates (restore/build/test, security, version) | skipped |
| **`dotnet-cli-heurex`** | the Heurex structure (what `hx new` generates) | the standard .NET gates **+** the opinionated Sentrux + ArchUnitNET structural gates | **enforced** |

The **baseline** every tier enforces: the `specify…release` stage proofs, ordered-task completion, task-hash binding, hygiene, and the commit chokepoints. `hx describe --json` reports the active tier and its exact ladder. (`dotnet-cli` is the legacy alias of `dotnet-cli-heurex`.)

### Staying current — `/doti-upgrade`

`/doti-upgrade` drives **both** update planes in one operator action:

- **The `hx` tool** — `dotnet tool update -g Heurex.SpeckitDoti` (NuGet global tool), or the Microsoft Store (MSIX).
- **This repo's `.doti`** — `hx doti install --repo .` reconciles the payload *version-aware*: an operator-modified managed file is kept (the bundled version is staged as a `.new` sidecar to merge), an operator-*deleted* asset is not resurrected without `--force`, and a repo whose payload is *ahead* of the bundled one is refused. It reports the tool vX→vY transition and the per-file install/migrate/preserve/block result.

---

## Get started

### Install the released toolkit

Current release: [**v0.5.0**](https://github.com/heurexai/speckit-doti/releases/tag/v0.5.0).

`hx` ships through **two channels**, both installing the same source-free CLI — per-RID tool binaries (Gitleaks/Sentrux/GitVersion) are fetched and hash-verified on demand, never bundled:

- **.NET global tool** (any OS) — `dotnet tool install --global Heurex.SpeckitDoti`, then `hx`. Update with `dotnet tool update --global Heurex.SpeckitDoti`. Requires the .NET 10 SDK.
- **Microsoft Store** (Windows, MSIX) — search "speckit-doti" in the Store, or `winget install Heurex.speckit-doti` (the `msstore` source). Store-signed, so no SmartScreen prompt.

```bash
# .NET global tool — installs `hx` onto your PATH
dotnet tool install --global Heurex.SpeckitDoti
hx version --json
hx new --name Acme.Widget --output ./Acme.Widget --company Acme --agents codex,claude
```

Use the same `hx` to inspect a doti-enabled repository. Installed `hx` is updated via its channel (`dotnet tool update -g Heurex.SpeckitDoti`, or the Microsoft Store); repo workflow assets are installed or repaired with `hx doti install`.

```powershell
# report the running hx version, the target repo's installed scaffold version,
# and exact managed workflow/skill modifications
hx version --repo . --json

# repair/reinstall repo-owned Doti workflow assets when needed
hx doti install --repo <target-repo> --agents codex,claude --json
```

The previous repository updater subcommand has been removed. Update installed `hx` through its channel (`dotnet tool update -g Heurex.SpeckitDoti`, or the Microsoft Store), and use `hx doti install --repo <path> --agents codex,claude --json` to install, repair, or migrate repo-owned Doti workflow assets. `--repo` is required; the command never defaults to the current directory. Its JSON proof classifies the target as `installed-new-target`, `installed-empty-target`, `installed-non-empty-non-doti-target`, or `upgraded-existing-doti-repo` and reports installed/preserved/removed/skipped/blocked paths with reasons. Live repo configuration and baselines, including `.doti/release.json`, cycle/gate state, prerequisite overrides, and Sentrux state, remain target-owned.

When repairing a repo that already has doti v0.5 installed, project-owned feature docs are not silently renamed. Leave implemented or completed historical specs on their existing filenames. If an open, unimplemented legacy spec is still unnumbered, migrate it before continuing: choose the next `NNN-` prefix, rename the matching `docs/specs`, `docs/plans`, and `docs/tasks` artifacts to the same numbered slug, then re-stamp `specify` with that `NNN-short-name`. All subsequent new specs use the numbered format.

`.doti/release.json` is live repo configuration. Existing release manifests are preserved by Doti install/repair work. Older repos that do not have one must add the manifest before running `hx release`.

### Local release output

The current unreleased release train includes `007-standalone-hx-source-independent-cli`, which makes installed `hx` run source-free, ships the two distribution channels (NuGet global tool + Microsoft Store MSIX), removes Velopack, and flips the default version intent to cycle-type-aware (feature → `minor`, bug-fix-only → `patch`).

`hx release` packs the declared product as a framework-dependent .NET global tool (with a source-free install smoke), records the Microsoft Store MSIX channel proof, and writes `release.identity.json`, from the committed repository state. Operational `hx` commands require an executable-adjacent `hx.config.json` loaded through Microsoft Configuration before they inspect or mutate a target repo. The target repository declares the product to publish in `.doti/release.json`: product/package name, publish `.csproj`, published executable name, and output executable name. This lets a vendored `hx` release the target repo's own product without requiring `tools/Hx.Scaffold.Cli` to exist in that repo.

Release tagging is command-backed. `hx release [--major|--minor|--patch]` validates the intent against the GitVersion-calculated version (a feature cycle defaults to `minor`, a bug-fix-only cycle to `patch`), creates or verifies the local annotated `v<version>` tag, packs the global-tool package (`dotnet pack`) and records the MSIX channel proof, and reports the tag push command needed for GitHub CI. `hx release` does not push tags — pushing the `v*` tag triggers `release.yml` (NuGet Trusted Publishing) and the Store MSIX workflow.

When `hx.config.json` enables local release output, the verified artifact set is copied to:

```text
<localReleaseOutput.directory>/<projectName>/<version number>
<localReleaseOutput.directory>/<projectName>/latest
```

The installed `hx.config.json` file lives next to `hx.exe` and is the only supported local-release configuration source. If `localReleaseOutput.enabled` is true, `localReleaseOutput.directory` must be an absolute path or `hx release` fails before reading `.doti/release.json`, calculating a tag, or writing artifacts. If local output is disabled, the command still reports a release result with an explicit skipped-copy reason.

```json
{
  "schemaVersion": 1,
  "localReleaseOutput": {
    "enabled": true,
    "directory": "D:\\heurex-releases"
  }
}
```

```powershell
.\hx.exe release --repo . --json
.\hx.exe release --repo . --minor --json
.\hx.exe release --repo . --patch --rid win-x64 --json
```

Manual or agent-performed file copying is not release proof; use the command's `LocalReleaseResult` envelope, including its config source/path, channel package artifacts, payload checks, version directory, latest directory, or disabled-copy reason.

Publishing to the two channels is automated on a pushed `v*` tag — NuGet Trusted Publishing (`release.yml`) and the Microsoft Store MSIX (`store-release.yml`); see [packaging/PUBLISHING.md](packaging/PUBLISHING.md) and [packaging/STORE.md](packaging/STORE.md).

The vendored tools (Gitleaks, Sentrux, GitVersion) are **not** bundled in the package — installed `hx` fetches each per-RID binary on demand and hash-verifies it into a shared per-user store (the standard Windows per-user data directory, or the XDG data dir — `HX_TOOL_STORE` overrides), so generated solutions resolve them from there with no ~127 MB per-project copy. The [.NET 10 SDK](https://dotnet.microsoft.com/) + Git are still required to build the generated solution; `hx prereq check` reports those prerequisites and directory readiness before `new` mutates anything.

### Build from source

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/) (10.0.300+), Git, and PowerShell or bash. Vendored tools (Gitleaks, Sentrux, GitVersion) are pinned and SHA-256-verified per their manifests under `tools/`, and fetched operationally — the large binaries are gitignored. The scaffold CLI uses `.doti/core/prerequisites.json` as the trusted prerequisite manifest; Windows automatic remediation is available only through `hx prereq install` with release-defined winget package/source metadata and an exact `--confirm-plan` digest.

```bash
# 1. build + test the toolkit itself
dotnet build scaffold-dotnet.slnx -c Release
dotnet test  scaffold-dotnet.slnx -c Release

# 2. check prerequisites, then scaffold a new agent-first .NET solution
#    (doti and the insurance hook are installed automatically)
dotnet run --project tools/Hx.Scaffold.Cli -- prereq check --for new --output ./Acme.Widget --json
dotnet run --project tools/Hx.Scaffold.Cli -- new \
  --name Acme.Widget --output ./Acme.Widget --company Acme --agents codex,claude

# 3. (optional) repair/re-arm the insurance pre-commit hook in an existing repo
dotnet run --project tools/Hx.Runner.Cli -- doti install-hooks --repo ./Acme.Widget

# 4. run the deterministic gate
dotnet run --project tools/Hx.Runner.Cli -- gate run --repo ./Acme.Widget --profile auto --json
```

Then drive development with the slash-commands, in order — each stage's proof is a prerequisite for the next:

```
/01-doti-specify → /02-doti-clarify → /03-doti-plan → /04-doti-tasks → /05-doti-analyze
→ /06-doti-arch-review → /07-doti-implement → /08-doti-drift-review → /09-doti-release
```

## CLI reference

From an installed build these run source-free as `hx <command>` (the unified surface). From a source checkout, run `dotnet run --project tools/Hx.Runner.Cli -- <command>` (use `Hx.Impact.Cli` for `plan`, and `Hx.Scaffold.Cli` for `new`, `version`, `release`, and `prereq`). Add `--json` for the machine envelope. Human `--help` is rich by default on capable terminals and supports `--help-mode plain`, `--plain-help`, `HX_HELP_MODE=plain`, and `NO_COLOR` for ANSI-free output.

| Command | What it does |
| --- | --- |
| `new` _(Hx.Scaffold.Cli)_ | Generate a new agent-first .NET solution and install doti |
| `version --repo <path>` _(Hx.Scaffold.Cli)_ | Report running hx identity, target repo scaffold version, and managed Doti modification state |
| `release [--repo <path>] [--major\|--minor\|--patch] [--rid <rid>]` _(Hx.Scaffold.Cli)_ | Validate executable-local `hx.config.json`, validate release intent, create/verify the local GitVersion tag, pack the declared product as a framework-dependent .NET global tool (`dotnet pack`) with a source-free install smoke, record the Microsoft Store MSIX channel proof, and copy the verified artifacts to `<package>/<version>` plus `<package>/latest` when local output is enabled |
| `prereq check --for <new\|version\|generated-validation>` _(Hx.Scaffold.Cli)_ | Check trusted .NET SDK/Git/directory prerequisites without installing anything |
| `prereq install --for <new> --confirm-plan <digest>` _(Hx.Scaffold.Cli)_ | Run an explicitly approved Windows winget install plan from trusted manifest metadata, then re-check prerequisites |
| `gate run --profile <auto\|advisory\|normal\|release>` | Run the full deterministic gate ladder, including task-completion proof for an active Doti cycle → one fail-closed `GateProof` |
| `security scan` | Package-vulnerability SCA (`dotnet list package --vulnerable`) + analyzer SAST status |
| `architecture test` | ArchUnitNET per-family proof |
| `sentrux verify` / `sentrux check` | Verify the vendored Sentrux binary / run the boundary analysis |
| `hygiene scan` / `hygiene gitleaks verify` | Working-tree hygiene + secret scanning |
| `version calculate` | Read-only GitVersion semantic version calculation; release tags are created only by `hx release --major\|--minor\|--patch` |
| `tools fetch [--rid] [--tool all\|gitleaks\|sentrux\|gitversion]` | Download + SHA-256-verify the vendored tool binaries/packages from their pinned manifests (fail-closed on mismatch) |
| `plan` _(Hx.Impact.Cli)_ | Affected-test planner (project-graph reverse closure → covering test projects) |
| `doti cycle stamp [--release-intent <major\|minor\|patch>] \| status \| check` | Stamp a stage (prereq-checked), including release-stage GitVersion intent signaling / show cycle state / fail-closed prereq check |
| `doti task-hash stamp [--feature <NNN-slug>]` | Refuse unchecked tasks and stamp canonical `doti-task-hash` markers for checked tasks |
| `doti payload check --repo <path>` | Verify scaffold-installed Doti payload parity by installing to a temp target and comparing managed `.doti` assets plus rendered skills/entrypoints |
| `doti question check` | Validate operator-question format compliance |
| `doti render-skills` / `hx doti install --repo <path>` / `doti install-hooks` | Re-render skills / install the workflow into an explicit classified target and auto-arm the hook / repair the pre-commit hook |
| `describe` | Self-describe the CLI surface as JSON |

## The deterministic gate

`gate run` aggregates an ordered, fail-closed ladder into one change-set-bound `GateProof`:

1. **Hygiene** — working-tree / staged scope checks
2. **Secret scanning** — vendored [Gitleaks](https://github.com/gitleaks/gitleaks)
3. **Affected-test planning** — project-graph reverse closure scopes the `normal`/`advisory` test run; `release` runs the full suite; the gate proof records planner, test-scope, and execution hashes
4. **Task completion** — when a Doti cycle is active, the feature task ledger must exist, every required task must be checked, and every checked task must carry a valid canonical `doti-task-hash`
5. **Build & test**
6. **Architecture** — [ArchUnitNET](https://github.com/TNG/ArchUnitNET) families + [Sentrux](https://github.com/heurexai/sentrux) boundary analysis
7. **Doti payload parity** — install Doti into a temp target and compare managed `.doti` source files plus rendered skills/entrypoints
8. **Security** — package-vulnerability SCA + build-integrated analyzer SAST (CA3xxx/CA5xxx as errors); enforced at `release`, advisory in dev
9. **Versioning** — [GitVersion](https://github.com/GitTools/GitVersion)

The gate never creates a Sentrux baseline, and persists its proof so Doti transition/release paths can require a _fresh, passing_ one whose affected-test hashes recompute against the current change set.

## Status

Current published release: [v0.5.0](https://github.com/heurexai/speckit-doti/releases/tag/v0.5.0). That older release used legacy platform archives. The product release surface is now the framework-dependent .NET global tool `Heurex.SpeckitDoti` on NuGet.org (Trusted Publishing) plus the Microsoft Store MSIX channel — there is no Velopack installer, and no source archive is accepted as the Doti product; `release.identity.json` records the per-channel proofs. The deterministic release gate remains command-backed and fail-closed.

The vendored tools self-provision: `tools fetch` downloads + SHA-256-verifies each tool binary/package (including GitVersion and Sentrux `v0.5.11`) from its pinned manifest, fail-closed on mismatch, and `new` runs it best-effort so a generated project ends up with the release/gate tools it needs without a manual step.

## Acknowledgements

- **[GitHub Spec Kit](https://github.com/github/spec-kit)** — speckit-doti is **based on Spec Kit**'s spec-driven development workflow and command model; we adapted its `specify → clarify → plan → tasks → analyze → implement` sequence to .NET and added the deterministic enforcement, the scaffold, and the architecture gating. Spec Kit is itself influenced by the work and research of John Lam.
- **[Sentrux (Heurex fork)](https://github.com/heurexai/sentrux)** — architecture & boundary analysis engine; forked to add .NET/C#-aware dependency analysis (see [Sentrux — our fork](#sentrux--our-fork)). MIT.
- **[ArchUnitNET](https://github.com/TNG/ArchUnitNET)** by TNG — in-code structural architecture rules.
- **[Gitleaks](https://github.com/gitleaks/gitleaks)** — secret scanning (vendored).
- **[GitVersion](https://github.com/GitTools/GitVersion)** — semantic versioning from git history (vendored).
- **[System.CommandLine](https://github.com/dotnet/command-line-api)** — the parser behind the agent-first kernel.
- **[xUnit](https://github.com/xunit/xunit)** — the test framework.

## Contributing and support

Contributions are welcome under the MIT license and DCO sign-off. Start with [CONTRIBUTING.md](CONTRIBUTING.md), use [SUPPORT.md](SUPPORT.md) for help, follow the [Code of Conduct](CODE_OF_CONDUCT.md), and report security-sensitive issues through [SECURITY.md](SECURITY.md).

## License

Licensed under the [MIT License](LICENSE). Copyright (c) 2026 Heurex.
