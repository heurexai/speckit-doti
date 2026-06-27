<div align="center">

<img src="media/heurex-lockup-horizontal.svg" width="220" alt="Heurex" />

<br />
<br />

<img src="media/speckit-doti-guarded-scaffold.png" width="240" alt="speckit-doti guarded scaffold showing spec, scaffold, test, gate, and release" />

# speckit-doti

**A spec-driven .NET starter kit and enforced AI-agent workflow.**

`hx new` gives you a compiling .NET solution on day one. Doti gives your AI coding agent a governed path from idea to release: spec, clarify, plan, tasks, analyze, architecture review, implementation, drift review, and release, each backed by command-checked proof instead of trust-me prose.

[![.NET](https://img.shields.io/badge/.NET-10.0-C9A961?style=flat-square&labelColor=1A1F4D)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-C9A961?style=flat-square&labelColor=1A1F4D)](LICENSE)
[![Based on Spec Kit](https://img.shields.io/badge/based_on-GitHub_Spec_Kit-C9A961?style=flat-square&labelColor=1A1F4D)](https://github.com/github/spec-kit)
[![Global tool](https://img.shields.io/badge/install-dotnet_tool-C9A961?style=flat-square&labelColor=1A1F4D)](#quickstart)

</div>

---

## Why speckit-doti

AI can write code quickly. That is no longer the hard part. The hard part is getting useful software out of an agent without losing the thread: what was agreed, what changed, what was tested, what architecture rules still hold, and what proof exists before release.

speckit-doti turns that loop into a working repo contract.

| If you code | If you do not code |
| --- | --- |
| Start from a clean .NET 10 scaffold with a domain library, thin CLI, xUnit tests, ArchUnitNET checks, Sentrux boundaries, security analyzers, and release tooling already wired. | Start from intent, not source files. You can review specs, plans, tasks, gate results, and release proof without pretending to audit every line of code. |
| Keep agents inside a fail-closed workflow. Stage proofs, task hashes, architecture gates, hygiene, security, and release checks are command-backed. | Use an AI agent with operational guardrails. The workflow creates checkpoints a founder, PM, operator, or technical reviewer can understand. |
| Build agent-facing tools. Every serious surface is JSON-first and source-free through installed `hx`, so Codex or Claude can drive the loop reliably. | Avoid vibe-code handoff. The output is a repo with evidence, not a chat transcript full of promises. |

If you already know GitHub Spec Kit, think of speckit-doti as the same spec-first idea with a .NET scaffold, deterministic gates, and release proof added.

| Familiar Spec Kit idea | Original Spec Kit | speckit-doti |
| --- | --- | --- |
| Project start | `specify init` installs Spec Kit workflow assets into a project. | `hx new` creates a compiling .NET repo, or `hx doti install --repo <path>` adds Doti to an existing repo. |
| Workflow shape | `constitution`, `specify`, `clarify`, `plan`, `tasks`, `analyze`, `implement`, with optional `checklist`, `converge`, and `taskstoissues`. | `specify`, `clarify`, `plan`, `tasks`, `analyze`, plus Doti-only `arch-review`, `drift-review`, and `release`. |
| Enforcement model | Agent prompts, templates, scripts, review gates, and checklists guide the process. | CLI-backed stage proofs, freshness checks, task hashes, pre-release gates, and fail-closed transition checks enforce the process. |
| Architecture | Technology-independent; architecture depends on the plan and project conventions. | Opinionated .NET architecture is generated and checked with ArchUnitNET, Sentrux, analyzers, and rule files. |
| Agent integration | Broad integration ecosystem for many agents and IDEs. | Focused rendered skills for Codex and Claude today, with JSON-first `hx` commands that agents can drive directly. |
| Existing repos | Flexible brownfield workflow and customization through templates, presets, extensions, and bundles. | Tiered adoption: `workflow-only`, `dotnet-lib`, or full `dotnet-cli-heurex`, so existing repos can adopt only the gates they are ready for. |
| Drift handling | `converge` appends remaining work by comparing current code against spec, plan, and tasks. | `drift-review`, `review-context`, `converge`, and advisory local `drift-candidates` separate deterministic proof from helpful review narrowing. |
| Release | Implementation is the normal end of the core flow; release remains project-owned. | Release is part of the governed flow: gate proof, release train handling, version intent, local tag proof, and channel packaging evidence. |

speckit-doti is not a no-code app and it does not remove engineering judgment. It gives coders and non-coders the same shared control surface: documented intent, generated tasks, deterministic checks, and a release path that refuses to fake proof.

---

## What it is

speckit-doti has three layers:

1. **The scaffold** - `hx new` creates a complete .NET 10 repo that builds and tests immediately.
2. **The Doti workflow** - a Spec Kit-inspired stage model with hash-bound proof, rendered agent skills, task completion checks, review recovery, drift review, and release train handling.
3. **The `hx` tool** - an installed, source-free CLI that exposes the workflow and gate commands without requiring a checkout of this repository.

In short: **the scaffold gives your agent a sane project; Doti gives it enforceable operating rules.**

---

## Quickstart

Install the released CLI, scaffold a repo, then run the generated solution.

```bash
dotnet tool install --global Heurex.SpeckitDoti

hx new --name Acme.Widget --output ./Acme.Widget --company Acme --agents codex,claude

dotnet build ./Acme.Widget/Acme.Widget.slnx -c Release
dotnet test  ./Acme.Widget/Acme.Widget.slnx -c Release
```

Then drive the repo with the generated agent skills:

```text
/01-doti-specify
/02-doti-clarify
/03-doti-plan
/04-doti-tasks
/05-doti-analyze
/06-doti-arch-review
/07-doti-implement
/08-doti-drift-review
/09-doti-release
```

The installed `hx` command is the operational path. Source-checkout commands are for development of speckit-doti itself.

---

## What `hx new` generates

```text
Acme.Widget/
|-- src/
|   |-- Acme.Widget/                     # pure domain library
|   `-- Acme.Widget.Cli/                 # thin, agent-first CLI
|-- test/
|   |-- Acme.Widget.Tests/               # xUnit tests
|   `-- Acme.Widget.Architecture.Tests/  # ArchUnitNET gates
|-- rules/                               # architecture, hygiene, security, Sentrux policy
|-- .sentrux/                            # boundary analysis config
|-- .doti/                               # Doti workflow assets, state, templates, profiles
|-- .agents/ and/or .claude/             # rendered agent skills
|-- global.json                          # SDK pin
`-- Acme.Widget.slnx
```

The generated architecture is intentionally opinionated:

- **Pure domain core** - business logic lives outside the CLI and avoids console, IO, network, and process concerns.
- **Thin CLI adapter** - command handlers parse, delegate, and render through one agent-friendly output channel.
- **Machine-readable results** - commands return a stable `CliResult` envelope for agents, with rich/plain human help for operators.
- **Architecture gates** - ArchUnitNET checks (the `cliSurfaceConfinement` and `cliDelegation` families keep the CLI thin and confined to a `*.Core` library) and Sentrux boundary analysis catch dependency drift before release.
- **Security and hygiene** - analyzers, package vulnerability checks, Gitleaks, hygiene policy, and tool hash verification are part of the gate story.

---

## The Doti workflow

Doti keeps the useful shape of GitHub Spec Kit, then adds enforcement.

| Stage | Purpose | Enforcement |
| --- | --- | --- |
| `specify` | Define the feature in `docs/specs/{NNN-slug}.md`. | Stage proof binds the spec content. |
| `clarify` | Ask and resolve blocking product questions. | Operator-question format is validated. |
| `plan` | Produce the implementation design. | Plan proof binds the approved approach. |
| `tasks` | Create executable tasks. | Task ledger becomes the implementation contract. |
| `analyze` | Check spec, plan, and tasks for gaps. | Coverage and consistency are reviewed before code. |
| `arch-review` | Review architecture impact before implementation. | Doti-only stage; scopes ArchUnitNET/Sentrux expectations. |
| `implement` | Execute the tasks. | Task completion is checked and hash-stamped. |
| `drift-review` | Compare the actual diff with the approved plan. | Doti-only stage; catches implementation/spec drift before release. |
| `release` | Package, tag, and prove the release. | Doti-only stage; release proof is command-backed. |

Each stage is stamped through `hx doti cycle`. Later stages check that prerequisites are present, fresh, and valid. The result is a workflow an agent can follow, but not quietly skip.

---

## Proofs, gates, and recovery

The main branch now includes the 007/008 release-train work: source-free installed `hx`, tiered Doti install, review recovery, deterministic change context, and advisory local semantic drift candidates.

Key capabilities:

- **Source-free workflow surface** - installed `hx` exposes `gate run`, `architecture test`, `sentrux verify/check`, `hygiene scan`, `security scan`, `version calculate`, `doti cycle`, `doti render-skills`, `doti install`, `doti payload check`, `doti install-hooks`, and `impact plan`.
- **Tiered adoption** - install Doti into different repo shapes without pretending every repo is the Heurex scaffold.
- **Task-hash completion** - checked tasks must carry canonical `doti-task-hash` markers; stale or missing hashes fail the gate.
- **Review recovery** - `hx doti cycle refresh-plan` and `refresh --apply-safe` show exactly which stale proofs can be safely reinterpreted and which stages must rerun.
- **Change context** - `hx doti review-context` and `hx impact plan --for change-context` turn changed files into review and gate data.
- **Release-train handling** - multiple completed, unreleased feature cycles can be carried into one release, with drift detection between them.
- **Advisory semantic drift candidates** - `hx doti drift-candidates` can suggest likely missing docs/tests/help updates using local models only; it is never release proof.

---

## Install tiers

`hx doti install --repo <path>` installs or repairs Doti workflow assets in an explicit target repo. It preserves operator changes, refuses unsafe version drift, and does not default to the current directory.

| Tier | Best for | Gate behavior |
| --- | --- | --- |
| `workflow-only` | Any repo, any language. | Enforces the spec-to-release workflow without .NET structure gates. |
| `dotnet-lib` | Existing .NET libraries and services. | Adds restore, build, test, security, version, and hygiene checks. |
| `dotnet-cli-heurex` | Repos created by `hx new`. | Adds the full Heurex scaffold contract, including ArchUnitNET and Sentrux structural gates. |

Tier answers "which gates exist?" The gate lane, passed as `hx gate run --profile auto|advisory|normal|release`, answers "how hard should this run right now?"

---

## CLI map

Use `--json` for the machine envelope. Use `--help-mode plain`, `--plain-help`, `HX_HELP_MODE=plain`, or `NO_COLOR` for ANSI-free human output.

| Command | What it does |
| --- | --- |
| `hx new` | Generate a new .NET solution and install Doti. |
| `hx version --repo <path>` | Report tool identity and the target repo's installed Doti/scaffold state. |
| `hx prereq check --for new` | Check .NET SDK, Git, and directory readiness before mutation. |
| `hx doti install --repo <path>` | Install, repair, migrate, or update Doti workflow assets. |
| `hx doti cycle status/check/stamp` | Report, enforce, or stamp stage proofs. |
| `hx doti cycle refresh-plan` | Show stale proof recovery steps without mutating anything. |
| `hx doti cycle refresh --apply-safe` | Rebind only safe-to-reinterpret stale proofs. |
| `hx doti task-hash stamp` | Stamp canonical hashes for completed tasks. |
| `hx doti review-context` | Emit deterministic review context for the current change set. |
| `hx doti drift-candidates` | Run advisory local semantic drift search. |
| `hx doti bug assess/fix/test` | Run the enforced bug mini-cycle. |
| `hx doti converge` | Compare feature prose and tasks for requirement coverage. |
| `hx gate run --profile normal` | Run the deterministic gate ladder and emit a `GateProof`. |
| `hx architecture test` | Run ArchUnitNET architecture rule families. |
| `hx sentrux verify/check` | Verify and run Sentrux boundary analysis. |
| `hx hygiene scan` | Run public-release hygiene checks. |
| `hx security scan` | Run package-vulnerability and analyzer-backed security checks. |
| `hx tools fetch` | Fetch and SHA-256-verify pinned tool binaries on demand. |
| `hx release --minor --repo <path>` | Validate release intent, create/verify the local tag, and produce channel proof. |
| `hx describe --json` | Self-describe the CLI surface for agents. |

---

## Distribution and release model

The intended installed product is the `hx` CLI, not this source tree.

- **NuGet global tool** - `dotnet tool install --global Heurex.SpeckitDoti`; update with `dotnet tool update --global Heurex.SpeckitDoti`.
- **Microsoft Store MSIX** - Windows channel with Store signing and Store-managed updates.
- **No Velopack** - the current release design removes the prior Velopack installer/update path.
- **No source archive as product** - release proof checks that installed artifacts run without a speckit-doti source checkout.
- **Tool binaries fetch on demand** - Gitleaks, Sentrux, and GitVersion are resolved from pinned manifests and hash-verified into a shared per-user store.

`hx release` owns local release proof and tag creation. Pushing the `v*` tag is what triggers publishing workflows for NuGet and the Store.

---

## Customizing Doti

Doti is generated from repo-owned source assets. Edit the source, then render and check drift.

| Area | Source |
| --- | --- |
| Workflow stages and agent skill text | `.doti/core/skills.json` and `.doti/core/templates/` |
| Stage order and produced artifacts | `.doti/core/workflows/doti/workflow.yml` |
| Repo principles | `.doti/core/memory/constitution.md` |
| Architecture gates | `rules/architecture.json` and architecture tests |
| Sentrux boundaries | `.sentrux/rules.toml` |
| Doti tier | `.doti/integration.json` and `.doti/profiles/` |

Useful checks:

```bash
hx doti render-skills --repo . --agents codex,claude --check --json
hx doti payload check --repo . --json
hx gate run --repo . --profile normal --json
```

---

## Status and honesty

- speckit-doti targets .NET 10 and assumes a terminal, Git, and the .NET SDK.
- It is built for AI-assisted development, but it is not a substitute for product ownership, architecture judgment, or release responsibility.
- Semantic drift candidates are advisory only. A clean candidate list is not proof.
- `workflow-only` can help non-.NET repos adopt the Doti process, but the full scaffold value is .NET-focused.
- Current main includes release-train work beyond the older public release notes. Check [CHANGELOG.md](CHANGELOG.md) for what is published vs. unreleased.

---

## Acknowledgements

- [GitHub Spec Kit](https://github.com/github/spec-kit) - the workflow model speckit-doti builds on and enforces.
- [ArchUnitNET](https://github.com/TNG/ArchUnitNET) - in-code architecture rules for .NET.
- [Sentrux](https://github.com/heurexai/sentrux) - structural boundary analysis, using the Heurex fork for .NET/C# support.
- [Gitleaks](https://github.com/gitleaks/gitleaks) - secret scanning.
- [GitVersion](https://github.com/GitTools/GitVersion) - semantic version calculation from Git history.
- [System.CommandLine](https://github.com/dotnet/command-line-api) - command parsing behind the CLI surfaces.
- [xUnit](https://github.com/xunit/xunit) - generated and toolkit test framework.

## Contributing and support

Contributions are welcome under the MIT license and DCO sign-off. Start with [CONTRIBUTING.md](CONTRIBUTING.md), use [SUPPORT.md](SUPPORT.md) for help, follow the [Code of Conduct](CODE_OF_CONDUCT.md), and report security-sensitive issues through [SECURITY.md](SECURITY.md).

## License

Licensed under the [MIT License](LICENSE). Copyright (c) 2026 Heurex.
