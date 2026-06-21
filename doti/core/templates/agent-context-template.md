# scaffold-dotnet Agent Context

This repository is a self-hosting, deterministic .NET scaffold driven by the command-backed doti workflow.

Use local Doti workflow skills to guide work, but do not report planned commands as passing gates until the corresponding .NET command exists.

## Current Command Availability

Implemented deterministic scaffold commands:

- `dotnet restore .\scaffold-dotnet.slnx`
- `dotnet build .\scaffold-dotnet.slnx -c Release --no-restore /m:1`
- `dotnet test .\scaffold-dotnet.slnx -c Release --no-build /m:1`
- `dotnet run --project tools/Hx.Scaffold.Cli -- profile`
- `dotnet run --project tools/Hx.Scaffold.Cli -- new --name <Name> --output <path> --company <Company> --agents codex,claude --json` (the single generation front door: invoke the template via subprocess `dotnet new`, finish the repo — vendor Gitleaks/Sentrux + the runner/impact source + install Doti — run the first smoke, and emit a `ScaffoldProof`; fully green on win-x64, other RIDs fail closed)
- `dotnet run --project tools/Hx.Runner.Cli -- bootstrap-proof`
- `dotnet run --project tools/Hx.Runner.Cli -- platform probe`
- `dotnet run --project tools/Hx.Impact.Cli -- bootstrap-plan`
- `dotnet run --project tools/Hx.Runner.Cli -- hygiene scan --repo . --scope changed --source staged --json`
- `dotnet run --project tools/Hx.Runner.Cli -- hygiene scan --repo . --scope changed --base <ref> --head <ref> --json`
- `dotnet run --project tools/Hx.Runner.Cli -- hygiene scan --repo . --scope all --json`
- `dotnet run --project tools/Hx.Runner.Cli -- hygiene gitleaks verify --repo . --json`
- `dotnet run --project tools/Hx.Runner.Cli -- hygiene gitleaks update-check --repo . --json` (explicit, network-enabled)
- `dotnet run --project tools/Hx.Runner.Cli -- hygiene gitleaks render-config --repo .` (regenerate tools/gitleaks/config/gitleaks.toml from rules/hygiene.json)

Hygiene secret scanning runs the vendored Gitleaks (v8.30.1, win-x64; other RIDs fail closed); scaffold-specific hygiene checks run on every host.

- `dotnet run --project tools/Hx.Runner.Cli -- sentrux verify --repo . --json`
- `dotnet run --project tools/Hx.Runner.Cli -- sentrux baseline --repo . --json`
- `dotnet run --project tools/Hx.Runner.Cli -- sentrux check --repo . --json`

Sentrux is vendored for win-x64 (v0.5.10, Heurex fork — adds `.sentruxignore` exclusions); verify/check pass. Other RIDs are pending.

- `dotnet run --project tools/Hx.Runner.Cli -- doti render-skills --repo . --agents codex,claude --json` (render installed skills + agent context from one source)
- `dotnet run --project tools/Hx.Runner.Cli -- doti render-skills --repo . --agents codex,claude --check --json` (drift check; fail closed)
- `dotnet run --project tools/Hx.Runner.Cli -- doti install --repo <path> --agents codex,claude --json` (install Doti assets — `doti/` source + rendered skills + repo metadata — into a target repo)
- `dotnet run --project tools/Hx.Runner.Cli -- architecture test --repo <path> --json` (run a generated repo's ArchUnitNET rule families; per-`[Fact]` proof + the nine families from `rules/architecture.json`; fully green on win-x64)
- `dotnet run --project tools/Hx.Runner.Cli -- gate run --repo <path> --profile <auto|advisory|normal|release> --json` (aggregate the deterministic ladder — hygiene, Gitleaks/Sentrux verify, affected-test planning, build/test, architecture, skill-drift, Sentrux check, version — into one fail-closed `GateProof`; never creates a Sentrux baseline; `doti-implement`/`doti-drift-review` call it as the blocking gate)
- `dotnet run --project tools/Hx.Impact.Cli -- plan --repo . [--base <ref> --head <ref>] --json` (deterministic affected-test planner — git diff + `.csproj`/`.slnx` reverse-dependency closure → covering test projects; fail-closed escalation to full-gate on broad/shared/unresolved changes. The gate's normal/advisory lane runs only the selected tests + new tests; release runs the full suite)
- `dotnet run --project tools/Hx.Runner.Cli -- version calculate --repo . --json` (GitVersion-backed version; fail-closed if the vendored GitVersion binary is absent for the host RID)
- `dotnet run --project tools/Hx.Runner.Cli -- version bump --major|--minor --repo . --json` (the sole bump surface — records an annotated git tag; agents never bump by hand)
- `dotnet run --project tools/Hx.Runner.Cli -- tools fetch --repo . [--rid <rid>] [--tool all|gitleaks|sentrux|gitversion] --json` (deterministic, hash-verified provisioning of the vendored tool binaries — gitleaks, sentrux, **and gitversion** (now a provisioned tool) — from their pinned `tools/*/*.version.json` manifests: download → verify `archiveSha256` (when set, unzip `executableName`, else the raw download is the exe) → verify `executableSha256` → write `executablePath`; fail-closed on mismatch (integrity/`tool-*-hash-mismatch`); a RID with no asset is reported cleanly (validation/`tool-asset-unavailable`), never thrown. Fetch-if-missing; `new` runs it best-effort after vendoring so generated repos carry a working gitversion. The offline gate verifies the already-present binaries — this fetch is a provisioning step, not part of the gate)
- `dotnet run --project tools/Hx.Runner.Cli -- security scan --repo . --json` (package-vulnerability SCA via `dotnet list package --vulnerable` + the build-integrated analyzer SAST status; enforced at release, advisory in dev. The .NET analyzers (CA3xxx/CA5xxx) + Gitleaks are the SAST gate; the build is the SAST enforcement point)
- `dotnet run --project tools/Hx.Runner.Cli -- doti cycle stamp --stage <id> --feature <slug> --repo . --json` + `doti cycle status --repo . --json` (cycle-state substrate — record diff-bound `CycleStageProof`s into `.doti/cycle-state.json` (gitignored) + report per-stage FRESH/STALE freshness; engine `Hx.Cycle.Core`. **Non-enforcing** — `status` exits 0 even when STALE; the fail-closed chokepoint is `cycle check`)
- `dotnet run --project tools/Hx.Runner.Cli -- doti cycle check --stage <id> --repo . --json` + `doti cycle commit --message <m> --repo . --json` + `doti question check --file <path> --repo . --json` + `doti install-hooks --repo . --json` (**enforcing** chokepoints — `cycle check` fails closed unless every transitive prerequisite is stamped + fresh; `cycle commit` is the **sole sanctioned commit path** (refuses unless a fresh drift-review + task-hash, a fresh passing persisted gate proof, and a clean staged scope, then commits); `question check` validates an operator question against the protocol (Layers B+C); `install-hooks` installs the untracked, logic-free insurance pre-commit hook that redirects bare `git commit`s. `gate run` persists a change-set-bound proof for `cycle commit` to verify)
- `dotnet run --project tools/<Tool>.Cli -- describe --json` (every scaffold CLI — Impact/Runner/Scaffold: the machine-readable capability model — command/option tree + exit classes + the error-code catalog, so an agent learns the whole tool in one call)
- `dotnet run --project tools/Hx.Runner.Cli -- errorcodes render --repo . --json` + `errorcodes check --repo . --json` (regenerate the structured error-code constants in `tools/Hx.Cli.Kernel/ErrorCodes.g.cs` from `errorcodes/registry.json`; `check` is the fail-closed append-only stability gate against `errorcodes/shipped.json`)
- `dotnet run --project tools/Hx.Runner.Cli -- gate run --repo . --profile <auto|advisory|normal|release> --stream --json` (stream NDJSON ladder-phase events live, then the final envelope)

**Agent-first CLI output.** Every scaffold CLI (Impact/Runner/Scaffold) renders the single `CliResult` envelope (kernel `Hx.Cli.Kernel` + contracts in `Hx.Tooling.Contracts`): JSON when piped or with `--json`, a readable summary on a TTY. Rings: status/identity/diagnostics/direction/result (+ effects, progress). Diagnostics carry a stable structured code `<PREFIX><NNNN>` from `errorcodes/registry.json` (frozen append-only by `errorcodes/shipped.json`); exit codes are a small fixed set (Success 0, Usage 2, Validation 3, Integrity 4, Internal 70); the envelope schema is published at `schemas/cli-envelope.schema.json`. Command bodies are thin — they build a `CliResult` and let `CliHost` render it + set the exit code (no direct `Console` writes).

Installed Codex/Claude skills and `.doti/agent-context.md` are rendered from `doti/core/skills.json` plus the profile availability footnote; hand-edits are drift. Skill drift is command-backed and wired into the `gate run` blocking gate.

- `dotnet pack scaffold/Hx.Scaffold.Templates.csproj` then `dotnet new install <nupkg>` and `dotnet new hx-dotnet-cli -n <Name>` (the `dotnet new` template pack: library + CLI + tests + ArchUnitNET architecture tests)

The `hx-dotnet-cli` template content lives under `scaffold/templates/dotnet-cli` (excluded from the scaffold repo's Sentrux graph via `.sentruxignore`). It is verified by golden + round-trip tests in `test/Hx.Templates.Tests` (the round-trip is gated behind `HX_TEMPLATE_ROUNDTRIP=1`; its nested `dotnet` calls disable persistent build servers and MSBuild node reuse — `--disable-build-servers`, `MSBUILDDISABLENODEREUSE=1`, `DOTNET_CLI_USE_MSBUILD_SERVER=0` — so build-server grandchildren cannot inherit and hold the test's redirected output pipe open and hang it; do not remove). `Hx.Scaffold.Cli new` is the single supported entry point: it vendors the runner/impact **source** + Gitleaks/Sentrux (with manifests) and installs the Doti assets into the generated repo, then runs the first smoke and emits a `ScaffoldProof`. Generated repos are self-hosting — they carry `doti/` plus rendered `.doti/`/`.agents/`/`.claude/` and run the same gates as the scaffold. Direct `dotnet new` use is bootstrap-only. The ArchUnitNET rule families are a command-backed gate: `architecture test` runs them with a per-family proof; the runner stays ArchUnitNET-free (rules live in the template's test project).

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

- Use `/doti-<name>` command naming.
- **Self-orient and self-correct via the CLI's machine contract.** Before driving a scaffold CLI, run `describe --json` to learn its command/option tree, exit classes, and error-code catalog — don't guess a command or flag. On any non-Success `CliResult`, act on the diagnostics `code` (`<PREFIX><NNNN>`), `hint`, and `nextActions` rather than guessing or blind-retrying.
- Treat manual review as advisory, not proof.
- Asking the operator a question (any stage, Claude and Codex): use the shared **Operator-Question Protocol** (the "Asking the operator a question" section below); `/doti-clarify` adds the stage-specifics (one blocking question at a time, folded into the spec's `## Clarifications`).
- Installed Codex/Claude skills, `.doti/agent-context.md`, and the root `AGENTS.md`/`CLAUDE.md` are rendered by `doti render-skills` from `doti/core/skills.json` + the profile footnote/maturity note; edit the source and re-render — never hand-edit installed files (they are drift).
- Do not add shell-specific runners.
- README and generated docs must describe only implemented behavior as implemented.

{operatorQuestionProtocol}

## Engineering discipline

These apply to all work in this repo, for every agent (Claude and Codex):

- **Root-cause, don't patch symptoms.** On any failure or surprise, do a real RCA — read the code/output, reproduce it, find the underlying cause — before changing anything.
- **Validate assumptions.** Verify every premise by reading the code, running the command, or observing the output. Prove claims (reproduce/RCA); never assert or assume.
- **No shortcuts.** Do not silence a check, hard-code around a problem, stub past a failure, or declare done without proof. If the correct/idiomatic solution is harder, do that one.
- **95% confidence.** If you are not ≥95% confident a solution and its code are correct, keep finding better approaches and validating until you are. Prove it works (build/test/run).
- **If truly blocked, say so.** When confidence is unreachable or you are genuinely blocked (missing access, an operator decision, an unverifiable premise), surface it with what you tried and what is needed — do not fabricate or guess.
- **Report honestly.** State what was validated and how, what remains, and any scope refinement or deferral with its rationale. Never overclaim.
