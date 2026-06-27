# scaffold-dotnet Agent Context

This repository is a self-hosting, deterministic .NET scaffold driven by the command-backed doti workflow.

Use local Doti workflow skills to guide work, but do not report planned commands as passing gates until the corresponding .NET command exists.

## Current Command Availability

Implemented deterministic scaffold commands:

- `dotnet restore .\scaffold-dotnet.slnx`
- `dotnet build .\scaffold-dotnet.slnx -c Release --no-restore /m:1`
- `dotnet test .\scaffold-dotnet.slnx -c Release --no-build /m:1`
- `hx profile`
- `hx new --name <Name> --output <path> --company <Company> --agents codex,claude --json` (the single generation front door: runs trusted prerequisite/directory preflight first, invokes the template via subprocess `dotnet new`, finishes the repo — vendor Gitleaks/Sentrux + the runner/impact source + install Doti, write the scaffold version stamp + canonical managed-asset hash baseline + prerequisite policy + `.doti/release.json` release-target manifest — run the first smoke, auto-arm the Doti insurance hook for the Git repo, and emit a `ScaffoldProof`; fully green on win-x64, other RIDs fail closed)
- `hx version --repo <path> --json` (repo-aware version report: running hx identity, target repo scaffold version stamp when present, release-asset identity when known, exact managed workflow-template vs skill/generated-instruction modification categories using the canonical hash baseline with source-format/canonicalizer/conflict-policy metadata, and read-only prerequisite health when available)
- `hx release --repo <path> [--major|--minor|--patch] [--rid <rid>] --json` (command-backed local release output: operational `hx` commands require an executable-adjacent `hx.config.json`; release reads that local Microsoft Configuration file before inspecting the target repo, then reads `.doti/release.json`, calculates the GitVersion-backed release version, validates the requested release intent, creates or verifies the local annotated release tag, publishes the manifest-declared target product rather than assuming `tools/Hx.Scaffold.Cli` exists in the target repo, packs it as a framework-dependent .NET global tool (`dotnet pack`) with a source-free install smoke, records the Microsoft Store MSIX channel proof plus payload/package checks and config source in `LocalReleaseResult` and `release.identity.json`, and when `localReleaseOutput.enabled` is true copies the verified set to `<localReleaseOutput.directory>/<packageName>/<version>` plus `<localReleaseOutput.directory>/<packageName>/latest`; if local output is disabled it reports the skipped-copy reason, `hx release` does not push tags, and manual agent file copying is not release proof)
- `hx prereq check --for <new|version|generated-validation> [--repo <path>] [--output <path>] --json` + `prereq install --for <new> [--repo <path>] [--output <path>] --confirm-plan <digest> --json` (trusted manifest-backed .NET SDK/Git/directory preflight; reports manifest identity and exact diagnostics; Windows automatic install is winget-only, package/source data is release-defined, and execution requires an exact operator-approved plan digest; non-Windows remains instructions-only)
- `hx bootstrap-proof`
- `hx platform probe`
- `hx impact bootstrap-plan`
- `hx hygiene scan --repo . --scope changed --source staged --json`
- `hx hygiene scan --repo . --scope changed --base <ref> --head <ref> --json`
- `hx hygiene scan --repo . --scope all --json`
- `hx hygiene gitleaks verify --repo . --json`
- `hx hygiene gitleaks update-check --repo . --json` (explicit, network-enabled)
- `hx hygiene gitleaks render-config --repo .` (regenerate tools/gitleaks/config/gitleaks.toml from rules/hygiene.json)

Hygiene secret scanning runs the vendored Gitleaks (v8.30.1, win-x64; other RIDs fail closed); scaffold-specific hygiene checks run on every host.

- `hx sentrux verify --repo . --json`
- `hx sentrux baseline --repo . --json`
- `hx sentrux check --repo . --json`

Sentrux is vendored as the Heurex fork v0.5.11 for declared RIDs with matching C# grammars (win-x64, linux-x64, osx-arm64; binaries are fetched operationally and gitignored); verify/check preserve richer fork diagnostics. Other RIDs remain fail-closed until their assets and grammars are declared.

- `hx doti render-skills --repo . --agents codex,claude --json` (render installed skills + agent context from one source)
- `hx doti render-skills --repo . --agents codex,claude --check --json` (drift check; fail closed)
- Installed/released repo asset path: `hx doti install --repo <path> --agents codex,claude --json` installs or repairs Doti assets into an explicit target repo; the command never defaults to the current directory, reads the `.doti` payload installed beside `hx`, classifies the target as `installed-new-target`, `installed-empty-target`, `installed-non-empty-non-doti-target`, or `upgraded-existing-doti-repo`, reports installed/preserved/removed/skipped/blocked paths with reasons, and auto-arms or refreshes the Doti insurance hook when the target is a Git repo; non-Doti pre-commit hooks fail hard and are not overwritten. Source/developer-only equivalent: `hx doti install --repo <path> --agents codex,claude --json`.
- `hx doti payload check --repo . --json` (install Doti into a temporary target using the real installer and compare managed `.doti` source assets plus rendered skills/entrypoints back to this repo; fail closed on exact path drift)
- `hx architecture test --repo <path> --json` (run the repo's ArchUnitNET rule families declared in `rules/architecture.json`; speckit-doti currently reports the thin-CLI `cliSurfaceConfinement` and `cliDelegation` families, while Sentrux enforces path/layer/cycle boundaries; fully green on win-x64)
- `hx gate run --repo <path> --profile <auto|advisory|normal|release> --json` (aggregate the deterministic ladder — hygiene, Gitleaks/Sentrux verify, affected-test planning, task-completion, prebuilt test execution, architecture, skill-drift, Doti payload parity, Sentrux check, version — into one fail-closed `GateProof`; persists a canonical affected-test proof with planner/test-scope/execution hashes; never creates a Sentrux baseline; `doti-implement`/`doti-drift-review` call it as the blocking gate after the command-backed build)
- `hx impact plan --repo . [--for <audience>] [--base <ref> --head <ref>] --json` (deterministic affected-test planner — git diff + `.csproj`/`.slnx` reverse-dependency closure → covering test projects; fail-closed escalation to full-gate on broad/shared/unresolved changes. The gate's normal/advisory lane runs only the selected tests + new tests; release runs the full suite. `--for change-context` instead emits the status-rich change set as data (`data.files` with Added/Modified/Deleted/Renamed status + `affectedSourceProjects`) that `/06-arch-review` and `/08-drift-review` consume. Direct `dotnet test` is diagnostic/advisory; only `gate run` mints the persisted affected-test proof that Doti workflow transitions and releases may rely on)
- `hx version calculate --repo . --json` (GitVersion-backed version; fail-closed if the vendored GitVersion binary is absent for the host RID; release tags are created only by `hx release --major|--minor|--patch`)
- `hx tools fetch --repo . [--rid <rid>] [--tool all|gitleaks|sentrux|gitversion] --json` (deterministic, hash-verified provisioning of the vendored tool binaries — gitleaks, sentrux, and gitversion — from their pinned `tools/*/*.version.json` manifests: download → verify `archiveSha256` when present → verify `executableSha256` → write `executablePath`; fail-closed on mismatch (integrity/`tool-*-hash-mismatch`); a RID with no asset is reported cleanly (validation/`tool-asset-unavailable`), never thrown. Fetch-if-missing; `new` runs it best-effort after vendoring so generated repos carry a working gitversion and release tooling. The offline gate verifies the already-present binaries/packages — this fetch is a provisioning step, not part of the gate)
- `hx security scan --repo . --json` (package-vulnerability SCA via `dotnet list package --vulnerable` + the build-integrated analyzer SAST status; enforced at release, advisory in dev. The .NET analyzers (CA3xxx/CA5xxx) + Gitleaks are the SAST gate; the build is the SAST enforcement point)
- `hx doti cycle stamp --stage <id> --feature <NNN-slug> [--release-intent <major|minor|patch>] --repo . --json` + `doti cycle status --repo . --json` (cycle-state substrate — record diff-bound `CycleStageProof`s into `.doti/cycle-state.json` (gitignored) + report per-stage FRESH/STALE/COMPLETED freshness; engine `Hx.Cycle.Core`. `stamp` fails closed when a stage's transitive prerequisites are missing, stale, invalid, when the first-stage feature slug is not numbered (`NNN-short-name`), or when a completed cycle tries to stamp a non-initial stage; when stamping the release stage, `--release-intent` adds the matching GitVersion `+semver:` signal to the automatic drift-review transition commit; `status` remains reporting-only and recovers a completed commit when a pending commit intent plus Git trailers prove the commit succeeded)
- `hx doti task-hash stamp --repo . [--feature <NNN-slug>] --json` (command-backed task-completion hashing: refuses unchecked tasks, writes canonical `doti-task-hash` markers for checked tasks, and uses the same whitespace/EOL-insensitive task hash validator that the `task-completion` gate step runs)
- `hx doti cycle check --stage <id> --repo . --json` + `doti question check --file <path> --repo . --json` + `doti install-hooks --repo . --json` (**enforcing** chokepoints — `cycle check` fails closed unless every transitive prerequisite is stamped + fresh, and reports completed-cycle / ambiguous-recovery verdicts explicitly; `question check` validates an operator question against the protocol (Layers B+C); `install-hooks` repairs/re-arms the untracked, logic-free insurance pre-commit hook that blocks bare `git commit`s and refuses to overwrite non-Doti hooks. Commits are owned by coded Doti workflow transitions and release paths, not by an agent-visible commit command. `gate run` persists a change-set-bound proof for those transition/release paths to verify)
- `hx doti cycle refresh-plan --target <stage> --repo . --json` + `doti cycle refresh --target <stage> --apply-safe --repo . --json` (review recovery — `refresh-plan` is the read-only recovery plan: per transitively-required stage it classifies the existing stamp `SafeReinterpret` (artifact unchanged under canonical hashing → re-bindable to the new change set), `RerunRequired` (artifact changed → re-run that stage), or `NotBound`; `refresh --apply-safe` re-stamps ONLY the `SafeReinterpret` stages and refuses the rest (`cycle-refresh-rerun-required`/`cycle-refresh-not-bound`), so a stale cycle recovers without hand-guessing a stamp)
- `hx doti review-context --base <ref> --repo . --json` (project the change set into arch-review lens applicability — the change categories (`doti-prose`/`generated-template`/`runtime-code`/`docs-only`/`contract`) + which lenses apply vs skip, so `/06` reads triage as data and a docs-only diff has a machine-checkable category set)
- `hx doti drift-candidates --base <ref> [--model-root <dir>] --repo . --json` (advisory semantic drift finder, **never gating** — embeds changed code vs reference prose on a local CPU model (primary Qwen3-Embedding-0.6B via GGUF/LLamaSharp, fallback BGE-M3 via ONNX; pinned + hash-verified, fully offline) and reports semantically-close candidates plus the ACTIVE engine. Skips cleanly when no model is provisioned; an empty candidate list is NOT a clean-bill signal. The model root resolves from `hx.config.json` `llmModelRoot` (wins), else `HEUREX_LLM_ROOT`, else the finder skips)
- `dotnet run --project tools/<Tool>.Cli -- describe --json` (every scaffold CLI — Impact/Runner/Scaffold: the machine-readable capability model — command/option tree + exit classes + the error-code catalog, so an agent learns the whole tool in one call)
- `hx errorcodes render --repo . --json` + `errorcodes check --repo . --json` (regenerate the structured error-code constants in `tools/Hx.Cli.Kernel/ErrorCodes.g.cs` from `errorcodes/registry.json`; `check` is the fail-closed append-only stability gate against `errorcodes/shipped.json`)
- `hx gate run --repo . --profile <auto|advisory|normal|release> --stream --json` (stream NDJSON ladder-phase events live, then the final envelope)

**Agent-first CLI output.** Every scaffold CLI (Impact/Runner/Scaffold) renders the single `CliResult` envelope (kernel `Hx.Cli.Kernel` + contracts in `Hx.Tooling.Contracts`): JSON when piped or with `--json`, a readable summary on a TTY. Human help for root commands, command groups, and leaf commands routes through the shared kernel renderer; use `--help-mode plain`, `--plain-help`, `HX_HELP_MODE=plain`, or `NO_COLOR` for ANSI-free help. Rings: status/identity/diagnostics/direction/result (+ effects, progress). Diagnostics carry a stable structured code `<PREFIX><NNNN>` from `errorcodes/registry.json` (frozen append-only by `errorcodes/shipped.json`); exit codes are a small fixed set (Success 0, Usage 2, Validation 3, Integrity 4, Internal 70); the envelope schema is published at `schemas/cli-envelope.schema.json`. Command bodies are thin — they build a `CliResult` and let `CliHost` render it + set the exit code (no direct `Console` writes).

Installed Codex/Claude skills and `.doti/agent-context.md` are rendered from `.doti/core/skills.json` plus the profile availability footnote; hand-edits are drift. Skill drift is command-backed and wired into the `gate run` blocking gate.

- `dotnet pack scaffold/Hx.Scaffold.Templates.csproj` then `dotnet new install <nupkg>` and `dotnet new hx-dotnet-cli -n <Name>` (the `dotnet new` template pack: library + CLI + tests + ArchUnitNET architecture tests)

The `hx-dotnet-cli` template content lives under `scaffold/templates/dotnet-cli` (excluded from the scaffold repo's Sentrux graph via `.sentruxignore`). It is verified by golden + round-trip tests in `test/Hx.Templates.Tests` (the round-trip is gated behind `HX_TEMPLATE_ROUNDTRIP=1`; its nested `dotnet` calls disable persistent build servers and MSBuild node reuse — `--disable-build-servers`, `MSBUILDDISABLENODEREUSE=1`, `DOTNET_CLI_USE_MSBUILD_SERVER=0` — so build-server grandchildren cannot inherit and hold the test's redirected output pipe open and hang it; do not remove). `Hx.Scaffold.Cli new` is the single supported entry point: it vendors the runner/impact **source** + Gitleaks/Sentrux (with manifests), installs the Doti assets into the generated repo, runs the first smoke, and auto-arms the Doti insurance hook. Generated repos are self-hosting — they carry managed `.doti/` source/payload assets plus rendered `.agents/`/`.claude/` skills and run the same gates as the scaffold. Direct `dotnet new` use is bootstrap-only. The ArchUnitNET rule families are a command-backed gate: `architecture test` runs them with a per-family proof; the runner stays ArchUnitNET-free (rules live in the template's test project).

Builds enforce security ahead of the security-scan gate: NuGet package auditing (`NuGetAudit`, direct + transitive) and the .NET analyzer security rules (`AnalysisModeSecurity=All`, e.g. CA3xxx injection + CA5xxx crypto) are on in both the scaffold repo and the template; with `TreatWarningsAsErrors`, a vulnerable package or insecure-code finding fails the build (fail closed). A build that fails on `NU190x` or a `CA5xxx`/`CA3xxx` diagnostic is a real security finding — root-cause and fix it, do not suppress without justification.

Available advisory checks:

- review public hygiene for local paths and sensitive markers;
- verify source assets and installed command-aware files are aligned;
- verify root `AGENTS.md` and `CLAUDE.md` are thin entrypoints;
- verify no PowerShell or Bash runners are introduced.

Cross-platform mode:

- win-x64 is the active target; other RIDs fail closed.
- Windows and Linux are active targets.
- macOS is advisory and warning-only.
- Linux runtime validation waits for Linux CI or an explicit Linux run.

## Workflow Rules

- Use numbered `/NN-doti-<name>` skill naming for the normal workflow (`/01-doti-specify` through `/09-doti-release`). Legacy unnumbered command/template names may remain as compatibility or source identifiers, but they are not the normal agent-facing workflow order.
- **Self-orient and self-correct via the CLI's machine contract.** Before driving a scaffold CLI, run `describe --json` to learn its command/option tree, exit classes, and error-code catalog — don't guess a command or flag. On any non-Success `CliResult`, act on the diagnostics `code` (`<PREFIX><NNNN>`), `hint`, and `nextActions` rather than guessing or blind-retrying.
- Treat manual review as advisory, not proof.
- Asking the operator a question (any stage, Claude and Codex): use the shared **Operator-Question Protocol** (the "Asking the operator a question" section below); `/doti-clarify` adds the stage-specifics (one blocking question at a time, folded into the spec's `## Clarifications`).
- Installed Codex/Claude skills, `.doti/agent-context.md`, and the root `AGENTS.md`/`CLAUDE.md` are rendered by `doti render-skills` from `.doti/core/skills.json` + the profile footnote/maturity note; edit the source and re-render — never hand-edit installed files (they are drift).
- Upgraded repos may contain pre-numbering v0.5 project specs. Repo asset install/repair is handled by `doti install`; project-owned feature docs are not silently renamed. Leave implemented/completed historical specs unchanged. If an open, unimplemented legacy spec is still unnumbered, rename the matching spec/plan/tasks artifacts to a new `NNN-short-name` slug and re-stamp `specify` with that slug before continuing. All subsequent new specs must use numbered slugs.
- Do not add shell-specific runners.
- README and generated docs must describe only implemented behavior as implemented.

## Asking the operator a question (required format)

Any operator-facing question — at any stage, for Claude or Codex — uses this fixed format, presented immediately before the question:

- **Context** — the full background. Assume the operator has not re-read the material; restate what is unclear and the relevant facts.
- **Why it matters** — the concrete impact AND a brief **concrete example** of how it plays out, and why it needs a decision now. Frame the stakes as QUALITY: for a code change — maintainability, consistency with existing patterns, correctness, testability, and future drift; for a docs/Doti-prose change — clarity, consistency, and accuracy of the documentation. **Never frame the stakes as effort or convenience** — that one option is more work is not why a question matters.
- **Options** — for each option: **Pros**, **Cons**, and **Consequence** — what becomes true downstream if chosen, reasoned through in those same quality terms with a **concrete example** (e.g. "then every new command must re-declare its error codes — a consistency cost paid forever", or "the README would then document two install paths — a reader-clarity cost"). Effort/convenience is NOT a pro, con, or consequence here; whether an option is more work never decides it.
- **Recommendation** — the option you recommend and the reasoning — the quality trade-off you are optimising, never "the easy one"; list it first and label it "(Recommended)".
- **Assumptions** — the assumptions behind the recommendation.
- **Confidence** — High / Medium / Low, with a one-line reason.

Evidence requirement: every question, option, recommendation, and assumption must rest on verified facts. Verify each premise first (read the code, run the tool, observe the output) and cite that evidence; never present options whose premises you have not confirmed. An assumption is allowed only when the fact cannot be obtained from this environment — then label it UNVERIFIED, state why it cannot be verified and what would verify it. Prove claims (reproduce/RCA); never assert them. Do not use "assume" to defer work you could do — an unproven premise is a defect that can ruin the design.

## Engineering discipline

These apply to all work in this repo, for every agent (Claude and Codex):

- **Root-cause, don't patch symptoms.** On any failure or surprise, do a real RCA — read the code/output, reproduce it, find the underlying cause — before changing anything.
- **Validate assumptions.** Verify every premise by reading the code, running the command, or observing the output. Prove claims (reproduce/RCA); never assert or assume.
- **No shortcuts.** Do not silence a check, hard-code around a problem, stub past a failure, or declare done without proof. If the correct/idiomatic solution is harder, do that one.
- **95% confidence.** If you are not ≥95% confident a solution and its code are correct, keep finding better approaches and validating until you are. Prove it works (build/test/run).
- **If truly blocked, say so.** When confidence is unreachable or you are genuinely blocked (missing access, an operator decision, an unverifiable premise), surface it with what you tried and what is needed — do not fabricate or guess.
- **Report honestly.** State what was validated and how, what remains, and any scope refinement or deferral with its rationale. Never overclaim.
