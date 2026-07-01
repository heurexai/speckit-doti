<div align="center">

<img src="media/heurex-lockup-horizontal.svg" width="220" alt="Heurex" />

<br />
<br />

<img src="media/speckit-doti-guarded-scaffold.png" width="240" alt="speckit-doti guarded scaffold showing spec, scaffold, test, gate, and release" />

# speckit-doti

**A spec-driven .NET starter kit and enforced AI-agent workflow.**

`hx new` gives you a compiling .NET solution on day one. Doti gives your AI coding agent a governed path from idea to release: spec, clarify, plan, architecture review, tasks, analyze, implementation, drift review, and release, each backed by command-checked proof instead of trust-me prose.

[![NuGet](https://img.shields.io/nuget/v/Heurex.SpeckitDoti?style=flat-square&labelColor=1A1F4D&color=C9A961&label=nuget)](https://www.nuget.org/packages/Heurex.SpeckitDoti)
[![.NET](https://img.shields.io/badge/.NET-10.0-C9A961?style=flat-square&labelColor=1A1F4D)](https://dotnet.microsoft.com/)
[![platform](https://img.shields.io/badge/platform-win--x64_·_linux--x64_·_osx--arm64-C9A961?style=flat-square&labelColor=1A1F4D)](#quickstart)
[![output](https://img.shields.io/badge/output-JSON_%2B_human-C9A961?style=flat-square&labelColor=1A1F4D)](#quickstart)
[![gates](https://img.shields.io/badge/gates-fail--closed-C9A961?style=flat-square&labelColor=1A1F4D)](#proofs-gates-and-recovery)
[![versioning](https://img.shields.io/badge/versioning-GitVersion-C9A961?style=flat-square&labelColor=1A1F4D)](#quickstart)
[![cycle](https://img.shields.io/badge/cycle-%2F01–%2F09_%2B_utilities-C9A961?style=flat-square&labelColor=1A1F4D)](#the-doti-cycle)
[![install](https://img.shields.io/badge/install-dotnet_tool_·_MS_Store-C9A961?style=flat-square&labelColor=1A1F4D)](#quickstart)
[![License: MIT](https://img.shields.io/badge/License-MIT-C9A961?style=flat-square&labelColor=1A1F4D)](LICENSE)
[![Based on Spec Kit](https://img.shields.io/badge/based_on-GitHub_Spec_Kit-C9A961?style=flat-square&labelColor=1A1F4D)](https://github.com/github/spec-kit)

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
| Workflow shape | `constitution`, `specify`, `clarify`, `plan`, `tasks`, `analyze`, `implement`, with optional `checklist`, `converge`, and `taskstoissues`. | `specify`, `clarify`, `plan`, then Doti-only `arch-review` (right after `plan`), `tasks`, `analyze`, `implement`, `drift-review`, and `release`; the project `constitution` is an unnumbered `/doti-constitution` skill that `plan` and `arch-review` consume. |
| Constitution | A single constitution document, SemVer-versioned with a Sync Impact Report on each amendment. | **Two layers** — **§1** inherited doti invariants (cited, already gate/ArchUnit/Sentrux/GitVersion-enforced, never re-declared) + **§2** project declarations (the only operator-authored content) — re-injected **fresh** into `plan` and `arch-review` via `hx doti constitution`; amendments are tracked by the cycle + git, with **no** SemVer doc-version line or Sync Impact Report ritual. |
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
/04-doti-arch-review
/05-doti-tasks
/06-doti-analyze
/07-doti-implement
/08-doti-drift-review
/09-doti-release
```

The installed `hx` command is the operational path. Source-checkout commands are for development of speckit-doti itself.

---

## Configuration

`hx new` derives or auto-configures almost everything a repo needs — copyright, package id, namespaces, the Sentrux baseline and boundary layers, the architecture rules, and the hooks. The handful of things **you** actually choose — supplied in one shot as a JSON config (`--config`, agent-friendly) or an interactive wizard (`--interactive`, human-friendly), or individually as below:

| What you set | Default | How |
| --- | --- | --- |
| **Project name** | — (required) | `hx new --name` (or `--config`/`--interactive`) |
| **Company / owner** | `Heurex` | `hx new --company` (or `--config`/`--interactive`) |
| **Output directory** | — (required) | `hx new --output` |
| **Agent toolchains** | `codex,claude` | `hx new --agents` (subset of `codex`, `claude`) |
| **Package description** | sample text | `--config`/`--interactive` (or `<Description>` in the CLI `.csproj`) |
| **Repository URL** | none | `--config`/`--interactive` (or `<RepositoryUrl>`) |
| **License** | `MIT` | `--config`/`--interactive` (or `<PackageLicenseExpression>`) |
| **Version series seed** | `0.1.0` | `--config`/`--interactive` (or `next-version` in `GitVersion.yml`) |
| **Local release directory** | unset | `DOTI_RELEASE_ROOT` env var (or `hx.config.json`) — machine-local, not committed |
| **NuGet publish** | none | `--config`/`--interactive` (parameterizes `release.yml`; the operator-only OIDC policy/secret/branch-protection print as a checklist, never executed) |
| **Constitution §2** | placeholders | `--config`/`--interactive`, or `/doti-constitution` (domain, tech stack, coding style, security, performance) |

`--config` and `--interactive` are mutually exclusive and 1:1 over one schema; both validate **before** any file is generated, persist your choices to a tracked `.doti/setup.json` so installs/upgrades reuse the same intent, and are available on `hx doti install` too (it applies the doti-layer subset to an existing repo). **`hx doti config show [--json]`** renders the effective configuration with a **default-vs-custom** indicator for every key. Shipped in `029-scaffold-setup-config`.

Everything else — the Sentrux baseline (set by the first build's smoke run), boundary layers and architecture namespaces, package id and copyright, the `dev`/`main` branches, and the pre-commit and DCO hooks — is derived from the above or configured automatically.

**Full reference:** [docs/configuration.md](docs/configuration.md) lists every setting Doti reads, grouped by what it drives, with purpose, valid values, and defaults.

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
| `arch-review` | Review the design right after `plan`, before tasks and analyze depend on it. | Doti-only stage; scopes ArchUnitNET/Sentrux expectations; a BLOCKER sends you back to `plan`. |
| `tasks` | Create executable tasks. | Task ledger becomes the implementation contract. |
| `analyze` | Check spec, plan, and tasks for gaps. | Coverage and consistency are reviewed before code. |
| `implement` | Execute the tasks. | Task completion is checked and hash-stamped. |
| `drift-review` | Compare the actual diff with the approved plan. | Doti-only stage; catches implementation/spec drift before release. |
| `release` | Package, tag, and prove the release. | Doti-only stage; release proof is command-backed. |

Each stage is stamped through `hx doti cycle`. Later stages check that prerequisites are present, fresh, and valid. The result is a workflow an agent can follow, but not quietly skip.

### Unnumbered utility skills

Alongside the nine numbered stages, Doti ships **unnumbered utility skills** — invoked by name (e.g. `/doti-constitution`), they run outside the numbered cycle (or anytime within it) and never reorder `/01`–`/09`. They are single-sourced in `.doti/core/skills.json` and rendered into the agent skills like the numbered stages.

**`/doti-constitution` — the project constitution.** The constitution is the project's own rule set, in two layers. **§1 (inherited doti invariants)** is *codified* and only cited, never re-declared — the deterministic gates, library-first/pure-core, thin CLI + `CliResult`, GitVersion versioning, Sentrux complexity, cross-platform and hygiene/SAST rules, and the cycle itself. **§2 (project declarations)** is the only operator-authored content: *domain principles*, *tech stack* (beyond the .NET baseline), *coding style*, *security & compliance*, and *performance*. `/03-doti-plan` and `/04-doti-arch-review` re-read **§2 fresh** every run via `hx doti constitution`, so the agent always designs and reviews against the current rules rather than a stale snapshot. Author or amend §2 with `/doti-constitution` (it initializes a missing constitution from the template with the project name auto-derived, and writes `.doti/memory/constitution.md`); amendments are tracked by the Doti cycle + git history — there is no SemVer doc-version line or sync-impact report. A repo generated by `hx new` gets its **own** constitution initialized from the template, not speckit-doti's.

The remaining utility skills:

| Skill | Purpose |
| --- | --- |
| `/doti-auto` | Drive the numbered cycle **hands-off** to a target (`--until <stage>`, default the local release), stopping only at operator-decision points (a clarify ambiguity, an arch-review BLOCKER, an unrecoverable gate failure, the publish boundary). Orchestration over the enforced stages — never a bypass (it never weakens a gate, skips a stamp, or pushes a release tag); for a release train, invoke it per member with `--until drift-review`. |
| `/doti-bug` | Run a bug fix as an **enforced mini-cycle** — assess (read-only) → fix (bound to the assessment) → test (an honest pass requires evidence) — recorded under `.doti/bugs/`. Runs anytime, outside the feature cycle. |
| `/doti-amend` | Amend an already-stamped cycle stage after an approved artifact change and **reconcile** cycle state via the recovery plan (`hx doti cycle refresh-plan` / `refresh --apply-safe`): re-bind the safe-to-reinterpret stamps, re-run the stages whose content genuinely changed. Never reorders `/01`–`/09`. |
| `/doti-drift-fix` | Patch a drift `/08-doti-drift-review` surfaced by correcting the **code** — never the spec, which is the source of truth — then reconcile cycle state. |
| `/doti-converge` | Brownfield / drift reconciliation: find the spec↔tasks coverage gap (`hx doti converge`), assess each uncovered `FR`/`SC` against the codebase, and append the genuinely-unbuilt work as new tasks. |
| `/doti-upgrade` | Upgrade the installed `hx` tool **and** reconcile this repo's `.doti` assets in one action — updates the tool via its channel (`dotnet tool update -g Heurex.SpeckitDoti`) and runs `hx doti install --repo .`, preserving operator-modified managed files. |

---

## Proofs, gates, and recovery

The main branch now includes the 007–016 work: source-free installed `hx`, tiered Doti install, review recovery, deterministic change context, advisory local semantic drift candidates, the project constitution stage, gate & affected-test visibility (`gate run --stream`), the `/doti-auto` hands-off cycle driver, the ArchUnit/Sentrux structural-offender detail (which files/types caused a structural failure, with Sentrux scoped to production code), and cross-platform tool provisioning (per-RID fetch + executable bit).

Key capabilities:

- **Source-free workflow surface** - installed `hx` exposes `gate run`, `architecture test`, `sentrux verify/check`, `hygiene scan`, `security scan`, `version calculate`, `doti cycle`, `doti constitution`, `doti render-skills`, `doti install`, `doti check-version`, `doti scan`, `doti update`, `doti update-all`, `doti payload check`, `doti install-hooks`, and `impact plan`.
- **Always-fresh constitution** - `hx doti constitution` emits the project's §1/§2 constitution; `plan` and `arch-review` evaluate against fresh §2, with delivery code-enforced and evaluation agent-judged (the deterministic gate is unchanged).
- **Tiered adoption** - install Doti into different repo shapes without pretending every repo is the Heurex scaffold.
- **Task-hash completion** - checked tasks must carry canonical `doti-task-hash` markers; stale or missing hashes fail the gate.
- **Review recovery** - `hx doti cycle refresh-plan` and `refresh --apply-safe` show exactly which stale proofs can be safely reinterpreted and which stages must rerun.
- **Change context** - `hx doti review-context` and `hx impact plan --for change-context` turn changed files into review and gate data.
- **Release-train handling** - multiple completed, unreleased feature cycles can be carried into one release, with drift detection between them.
- **Advisory semantic drift candidates** - `hx doti drift-candidates` can suggest likely missing docs/tests/help updates using local models only; it is never release proof.

---

## Install tiers

`hx doti install --repo <path>` installs or repairs Doti workflow assets in an explicit target repo. It preserves operator changes, refuses unsafe version drift, and does not default to the current directory.

`hx doti install` / `update` / `update-all` are **self-contained**: run one command from any directory and the target repo's managed Doti assets are reconciled to the **running tool's bundled payload** (the payload that ships beside the executable — no `cd` to a source checkout, fail-closed if no version-stamped payload resolves). The reconcile **prunes** rendered-skill dirs a payload version renamed away (rendered skills are not operator content; operator-customized policy assets like `skills.json` and the constitution are always preserved), surfaces preserved customizations as `.new` merge-helpers (only when they genuinely differ), and — in a Git repo — makes a **single sanctioned commit of exactly the touched managed-asset paths** (never `git add -A`, so your unrelated work is untouched). `--no-commit` leaves the reconcile in the working tree; a non-Git target reconciles and skips the commit. Shipped in `031-doti-update-self-contained`.

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
| `hx doti install --repo <path> [--no-commit]` | Install, repair, migrate, or update Doti workflow assets from the bundled payload; prunes renamed-away skill dirs; auto-commits exactly the touched paths (opt out with `--no-commit`). |
| `hx doti check-version --repo <path>` | Report a repo's recorded Doti version + its relation to the installed tool (current/outdated/ahead). |
| `hx doti scan --root <dir>` | Discover every Doti-enabled repo under a tree and table each one's version + relation. |
| `hx doti update --repo <path> [--force] [--dry-run] [--no-commit]` | Update one repo's managed assets to the running tool's bundled payload and report the before→after version (reconciled in a git worktree; orphaned skill dirs pruned; customizations preserved unless `--force`; auto-commits the touched paths unless `--no-commit`). |
| `hx doti update-all --root <dir> [--force] [--dry-run] [--no-commit]` | Batch-update every Doti repo under a root, fail-soft, with an updated/already-current/failed summary; same bundled-source + prune + auto-commit behavior per repo. |
| `hx doti cycle status/check/stamp` | Report, enforce, or stamp stage proofs. |
| `hx doti cycle refresh-plan` | Show stale proof recovery steps without mutating anything. |
| `hx doti cycle refresh --apply-safe` | Rebind safe-to-reinterpret **and** content-equal stale proofs (a content change, review stage, or code-bound stage still earns a re-run). |
| `hx doti cycle review-rebind --target <stage> --attest no-impact [--reason]` | After reading the surfaced upstream diff, record an agent's reviewed **no-impact** verdict on an upstream-changed stage (recorded + decaying); a bare `stamp` of such a stage refuses. |
| `hx doti task-hash stamp` | Stamp canonical hashes for completed tasks. |
| `hx doti review-context` | Emit deterministic review context for the current change set. |
| `hx doti constitution` | Emit the project constitution (§2 declarations) for fresh plan and arch-review context. |
| `hx doti drift-candidates` | Run advisory local semantic drift search. |
| `hx doti bug assess/fix/test` | Run the enforced bug mini-cycle. |
| `hx doti converge` | Compare feature prose and tasks for requirement coverage. |
| `hx gate run --profile normal` | Run the deterministic gate ladder and emit a `GateProof`; add `--stream` for a live per-step trace (scope, per-step timing, change summary). |
| `hx architecture test` | Run ArchUnitNET architecture rule families. |
| `hx sentrux verify/check` | Verify and run Sentrux boundary analysis; a violation surfaces the offending file/function (production code only — the `test/` tree is excluded). |
| `hx hygiene scan` | Run public-release hygiene checks. |
| `hx security scan` | Run package-vulnerability and analyzer-backed security checks. |
| `hx tools fetch` | Fetch and SHA-256-verify pinned tool binaries on demand. |
| `hx release --minor --repo <path>` | Validate release intent, create/verify the local tag, and produce channel proof. |
| `hx describe --json` | Self-describe the CLI surface for agents. |

> The doti repo version-lifecycle commands (`check-version` / `scan` / `update` / `update-all`) and the `version --repo` payload-relation fix shipped in `022-doti-repo-version-lifecycle`.

> The development cycle reviews the design before the work that depends on it: `arch-review` runs at `04` (immediately after `plan`), then `tasks` (`05`) and `analyze` (`06`) — shipped in `026-arch-review-after-plan`.

> Stamp reconciliation is codified, not manual: re-running the one genuinely-changed stage auto-rebinds its content-unchanged dependents (a content change, a review stage, or a code-bound stage always earns a real re-run — never a rubber-stamp), and `hx doti update` prunes skill dirs a payload renamed away — shipped in `027-stamp-reconcile-orphan-prune`.

> `hx doti install` / `update` / `update-all` are self-contained — one command from any directory reconciles from the tool's bundled payload, prunes renamed-away orphans, and makes a single sanctioned commit of exactly the touched paths (`--no-commit` opts out; non-git skips it). The bug mini-cycle (`/doti-bug`) requires a reproduce → root-cause → validate RCA and a root-cause fix, and forbids a bandaid/symptom patch or a bandaid-vs-root choice — shipped in `031-doti-update-self-contained`.

> Reconciliation is also agent-gated and self-describing: when an upstream changes, the engine surfaces the diff and the agent records an explicit re-author or `cycle review-rebind --attest no-impact` verdict (recorded + decaying) — a bare `stamp` of an upstream-changed stage refuses, closing the agent-rubber-stamp hole the engine alone could not. And the workflow's next-step commands are generated from one code model (`DotiActionModel`), never a hand-maintained list that can drift — the §1 *self-describing automation* invariant. Shipped in `028-agent-gated-reconcile`.

> `hx doti update` is worktree-safe and never silently swallows a failed self-commit: the reconcile's temp worktree is disposed **before** the commit (no shared-`.git` lock race), a failed commit surfaces as `ok:false`/non-zero (not buried under "Updated"), leaked `hx-doti-worktree-*` temps are pruned ownership-aware (a concurrent run's live worktree is never killed), and the vendored structural tools (`tools/{sentrux,gitleaks,gitversion}`) are reconciled + parity-checked so a doti version bump can no longer leave a repo on a stale Sentrux with a silently-broken gate — shipped in `032-doti-update-worktree-leak`.

> A confirmed bug-fix-only mini-cycle (a passing `/doti-bug`, no numbered feature cycle and so no `.doti/cycle-state.json`) is now releasable as a **patch** — the release train tolerates the missing cycle-state so the bug-release bridge runs, with no fabricated feature cycle and no feature stamp — shipped in `033-bug-only-release-path`.

> A bug-only release train can now **commit** the release-documentation fix its own `release-documentation` gate demands, through a new sanctioned **`hx doti bug release-docs`** — fail-closed gated on a confirmed, fix-bound, test-passed bug member (it refuses before any git mutation otherwise), staging **exactly** `README.md`+`CHANGELOG.md` (never `git add -A`) with the insurance-hook sentinel + the `032` lock-retry, so an operator never hand-wrangles a `DOTI_SANCTIONED_COMMIT=1` bypass. `033` opened the bug-only release *path* but left this last release-doc commit with no coded home — no workflow-transition or release path committed it — shipped in `034-bug-only-release-doc-commit`.

> The `hx doti update` / release / sanctioned-commit subsystem was hardened against a whole **class** of edge bugs an adversarial Opus review surfaced: a sanctioned commit now commits an **explicit pathspec** (never the whole index, so a file the operator already staged is never swept in) and **skips** any single unstageable candidate (a gitignored path, an untracked orphan) instead of failing the commit; `doti install`'s failed commit no longer swallows to exit-0; and the `release-documentation` gate matches a member slug as a bounded token, not a substring. Both commit paths share one `SanctionedGitCommit` so they can't drift — shipped in `035-commit-update-release-hardening`.

> A fast-follow to the 035 hardening (found by **verifying** a real consumer update, not shipping blind): the sanctioned-commit pathspec is now **directory-aware**, so an `hx doti update` that touches a whole managed subtree (`.doti/core`, …) commits its files cleanly instead of leaving some staged — while still never sweeping an operator's out-of-tree pre-staged work. Shipped in `036-reconcile-dir-candidate-commit`.

> `hx release` now correctly releases a **non-hx scaffolded product** (the whole point of the `.doti/release.json` vendored-release path): the source-free install smoke runs the target's *own* declared executable — resolving whichever launcher `dotnet tool install` emits (an apphost `.exe` or a framework-dependent `.cmd`) — instead of a hardcoded `hx.exe`, and a bug-only release no longer self-excludes its own bug after tagging. Proven end-to-end against a real generated repo. Shipped in `037-release-non-hx-product`.

> `hx doti cycle check --stage release` is now **bug-only-aware**: on a repo with no `.doti/cycle-state.json` (a bug-only release train) it delegates readiness to the same bug-aware release train the release path already trusts, instead of demanding the eight feature-cycle stamps a bug-only repo never had — a false blocker that previously tempted an agent to *fabricate* a feature cycle. A valid bug train passes; an empty one still fails closed; feature-stage checks are untouched. And an adversarial verification of the fix caught a coupled write-path hole — `cycle stamp --stage release` shares that chokepoint — so a `RefuseBugOnlyReleaseStamp` guard now makes a release *stamp* on a bug-only repo fail closed too: **no `cycle-state.json` is ever fabricated**, on either the read or the stamp path. Proven end-to-end on a real generated repo. Shipped in `038-bug-only-release-cycle-check`.

---

## Distribution and release model

speckit-doti ships as the `hx` CLI — you install the tool, not this source tree. Two parts, nothing else:

- **NuGet .NET global tool** - `dotnet tool install --global Heurex.SpeckitDoti`; update with `dotnet tool update --global Heurex.SpeckitDoti`.
- **Tool binaries fetched on demand** - Gitleaks, Sentrux, and GitVersion are resolved from pinned manifests and SHA-256-verified into a shared per-user store the first time they're needed (never bundled in the package).

`hx release` owns local release proof and tag creation. Pushing the `v*` tag is what triggers publishing workflows for NuGet and the Store.

---

## Customizing Doti

Doti is generated from repo-owned source assets. Edit the source, then render and check drift.

| Area | Source |
| --- | --- |
| Workflow stages and agent skill text | `.doti/core/skills.json` and `.doti/core/templates/` |
| Stage order and produced artifacts | `.doti/core/workflows/doti/workflow.yml` |
| Repo principles (the constitution) | `.doti/memory/constitution.md` |
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
