# Plan: Unified CLI help and doti cycle lockdown

> Spec: `docs/specs/unified-help-and-cycle-lockdown.md`. Finish the workflow through commit, but do not run `/doti-release`, create a release tag, or publish a release. Produce a locally testable latest `hx.exe` artifact after the non-release cycle proof is green.

## Technical Context

The work spans three existing scaffold surfaces:

1. **Human CLI help** in `Hx.Cli.Kernel`, currently rich only for `Hx.Scaffold.Cli` root help through `CliApp.Invoke` and `CliRenderer.WriteHelp`. `Hx.Runner.Cli` and `Hx.Impact.Cli` still call `rootCommand.Parse(args).Invoke()` directly, and nested help falls through to System.CommandLine default help.
2. **Affected-test proof** in `Hx.Gate.Core`, `Hx.Impact.Core`, `Hx.Cycle.Core`, and `Hx.Tooling.Contracts`. The gate already runs the affected planner and selected tests, and `cycle commit` already checks a fresh passing persisted `GateProof`, but the persisted proof lacks a canonical affected-plan/test-execution identity.
3. **Sanctioned workflow evidence** in `Hx.Cycle.Core`. Stage proofs are diff-bound today, but review/diff stages can be stamped with no stage-specific payload beyond the changed diff identity.

Stack and constraints: .NET source only, existing `System.CommandLine` parser, Spectre.Console for rich human rendering, `CliResult` for machine output, existing ArchUnitNET/Sentrux boundaries, no shell runners, and no release action. JSON contracts must remain additive or explicitly versioned; existing `describe --json` and `--json` command output must stay stable.

## Constitution Check (gate)

PASS before design:

- **Deterministic Ownership** — help behavior and proof validation will be source-controlled .NET code, not agent convention.
- **Bootstrap Honesty** — new proof/help checks are advisory until implemented and tested; the plan marks them as planned.
- **Template Boundary** — generated template CLIs must inherit the shared kernel behavior, but static template layout remains template-owned.
- **Public Hygiene** — no secrets, local binary mirrors, or release assets are committed. Local `hx.exe` is a test artifact only.
- **Cross-Platform Rule** — no PowerShell/Bash runner is added; all logic remains .NET/dotnet-hosted.
- **Codified Cycle** — the work strengthens `gate run`, `cycle check`, and `cycle commit`; commit remains through `doti cycle commit`.
- **Engineering Discipline** — implementation must prove nested rich/plain help, affected-proof recomputation, and bypass rejection with tests before claiming completion.
- **Channel Independence** — help rendering stays in `Hx.Cli.Kernel`; gate/cycle validation stays in core libraries; CLI entry projects only wire.

No violation, so Complexity Tracking is empty.

## Research (resolve unknowns)

- **Decision:** implement help mode as `auto|rich|plain`, exposed through a shared `--help-mode` option and an `HX_HELP_MODE` environment override; honor `NO_COLOR` as plain/no-ANSI in auto.
- **Rationale:** the spec requires both explicit and automatic plain fallback. A global option lets every CLI expose the same behavior without per-command switches, and an environment override helps users whose terminals cannot render ANSI.
- **Alternatives rejected:** only `NO_COLOR` (not explicit enough for users who want rich forced); only a CLI flag (does not help nested/tool-driven invocations); replacing System.CommandLine parsing (unnecessary).

- **Decision:** derive a reusable `CommandHelpModel` from `System.CommandLine.Command`/`Option` metadata and render it through shared rich and plain renderers.
- **Rationale:** automatic inheritance requires command-model traversal, not hand-authored tables. It also lets root, group, and leaf commands share the same data.
- **Alternatives rejected:** per-command help methods (drifts quickly); relying on default System.CommandLine help (does not meet the style/ANSI fallback requirement).

- **Decision:** add an `AffectedTestProof` contract and attach it to `GateProof` as an optional/additive field, with canonical hashes over the affected plan, selected test set, and executed test targets.
- **Rationale:** `cycle commit` can recompute against the current change set and reject stale/manual/narrowed proofs without trusting human-readable messages.
- **Alternatives rejected:** storing only extra `GateEvidence` messages (not machine-verifiable); accepting TRX/direct `dotnet test` output (violates sanctioned proof requirement).

- **Decision:** add structured proof validation in `Hx.Cycle.Core` instead of in CLI command bodies.
- **Rationale:** `cycle commit` and future channels must share the same enforcement. This preserves thin CLI.
- **Alternatives rejected:** validation inside `RunnerCommands.CycleCommit` (CLI logic); relying on pre-commit hook (hook is local insurance, not an enforcement boundary).

- **Decision:** model sanctioned surfaces as a source-controlled JSON policy under `.doti/workflows/doti/` and load it from core checks.
- **Rationale:** a machine-readable policy is reviewable, can drive diagnostics/tests, and can also generate documentation later.
- **Alternatives rejected:** documentation-only policy (not enforceable); hard-coded command strings only in C# (harder to audit and localize).

## Design

### 1. Unified help model and renderer

Likely files:

- `tools/Hx.Cli.Kernel/CliApp.cs`
- `tools/Hx.Cli.Kernel/CliRenderer.cs`
- new `tools/Hx.Cli.Kernel/Help/CommandHelpModel.cs`
- new `tools/Hx.Cli.Kernel/Help/HelpMode.cs`
- new `tools/Hx.Cli.Kernel/Help/HelpRenderer.cs`
- `tools/Hx.Scaffold.Cli/Program.cs`
- `tools/Hx.Runner.Cli/Program.cs`
- `tools/Hx.Impact.Cli/Program.cs`
- generated template CLI kernel/program files under `scaffold/templates/dotnet-cli`
- tests in `test/Hx.Cli.Kernel.Tests` and template golden tests

Approach:

- Add one `CliApp.Invoke(root, meta, args, banner, tagline)` path for every scaffold CLI.
- Teach `CliApp.Invoke` to detect help anywhere in the command path (`--help`, `-h`, `-?`, and `help`) and resolve the target command path before invoking default help.
- Render root, group, and leaf help through a shared command-help model.
- Render rich help through Spectre tables/panels using the existing palette.
- Render plain help with no ANSI, no Spectre markup, and equivalent command/option/argument/default content.
- Keep `--json` and `describe --json` unchanged; help mode affects human help only.

Architecture delta: no new project/layer. `Hx.Cli.Kernel` is already in the core layer. No `rules/architecture.json` or `.sentrux/rules.toml` change is required unless implementation reveals direct console writes outside the kernel.

### 2. Affected-test proof binding

Likely files:

- `tools/Hx.Tooling.Contracts/GateProof.cs`
- new `tools/Hx.Tooling.Contracts/AffectedTestProof.cs`
- `tools/Hx.Gate.Core/GateRunner.cs`
- `tools/Hx.Cycle.Core/GateProofStore.cs`
- `tools/Hx.Cycle.Core/CycleService.cs`
- new `tools/Hx.Cycle.Core/GateProofValidator.cs`
- `tools/Hx.Impact.Core/Planning/AffectedTestPlanner.cs` if the proof needs changed-path classification exported
- tests in `test/Hx.Runner.Tests` and `test/Hx.Impact.Tests`

Approach:

- During `gate run`, compute a canonical `AffectedTestProof` containing:
  - schema version, lane, base ref, current change-set id;
  - affected-plan outcome and reasons;
  - canonical affected-plan hash;
  - canonical selected-test-set hash;
  - changed-path classifications where available;
  - selected/executed test projects and command scope;
  - full-suite flag and full-suite reason when escalated or release;
  - per-target result status sufficient for `cycle commit` to verify the gate path, without storing large logs.
- Persist the proof inside `GateProof` or `PersistedGateProof` in an additive way.
- In `cycle commit`, recompute the current affected plan using the stored base ref and current `HEAD`, recompute the canonical hashes, and refuse if:
  - affected proof is absent;
  - proof lane is wrong for the stored lane;
  - plan hash or selected-test-set hash differs;
  - proof claims affected/no-tests/full-gate but recomputation reaches a different outcome;
  - proof says selected tests ran but execution records are missing or mismatched.
- Treat direct `dotnet test` artifacts as diagnostics only; the validator accepts only gate-minted proof.

Architecture delta: no new project/layer. Contracts remain lowest layer; gate/cycle/impact stay in core.

### 3. Sanctioned-surface policy and stage-proof tightening

Likely files:

- new `.doti/workflows/doti/sanctioned-surfaces.json`
- `tools/Hx.Cycle.Core/StageModel.cs`
- `tools/Hx.Cycle.Core/CycleService.cs`
- `tools/Hx.Tooling.Contracts/CycleState.cs`
- `doti/core/templates/agent-context-template.md`
- `doti/core/skills.json`
- rendered `.doti/agent-context.md`, `.agents/skills/doti-*`, `.claude/skills/doti-*`
- tests in `test/Hx.Runner.Tests`

Approach:

- Define the proof-producing surface for generation, affected planning, gate execution, security scan, version bump, Sentrux baseline/check, skill rendering, drift review, and commit.
- Extend stage proof payloads so review/diff stages cannot satisfy commit prerequisites with only a bare stage id.
- Make later-stage stamping fail closed unless transitive prerequisites are fresh. The first `specify` stamp remains the cycle bootstrap.
- Add diagnostics/next actions for common bypasses:
  - generated skill drift;
  - unexpected Sentrux baseline edit;
  - direct/manual version tag drift;
  - missing doti cycle trailer in commit history where relevant;
  - manually edited or malformed gate/cycle proof.

Architecture delta: no new project/layer. If policy loading creates new types, they live in `Hx.Cycle.Core` or contracts.

### 4. Local latest `hx.exe` for testing, no release

Likely files/commands:

- packaging and installer files under `packaging/`
- existing publish/package commands discovered from the current repo before execution

Approach:

- After implementation and normal gate proof, build/package the latest local `hx.exe` artifact for the user to test.
- Do not run `/doti-release`, do not create a release tag, do not push a release, and do not publish GitHub Release assets.

## CLI surface & error contract

Changed operation: human help for all scaffold CLIs.

- **Error codes:** none expected for successful help rendering; malformed help-mode values should use existing usage diagnostics unless plan implementation proves a new stable code is needed.
- **Exit class:** Success for help output; Usage for invalid help-mode values.
- **`describe` entry:** unchanged for existing commands except the shared help-mode option, if surfaced in the command model.
- **Envelope:** unchanged for command execution. Help rendering is human output and does not replace `CliResult`/`--json`.
- **Channel boundary:** behavior lives in `Hx.Cli.Kernel`; CLI projects only call `CliApp.Invoke`.

Changed operation: `gate run` / `doti cycle commit` proof validation.

- **Error codes:** likely existing `Validation_Failed` for stale/missing proof; existing `Integrity_VerificationFailed` for malformed/manual proof. Add registry codes only if implementation needs more specific diagnostics.
- **Exit class:** `gate run` remains Success/Validation by aggregate proof. `doti cycle commit` remains blocked Validation when proof is missing/stale/invalid.
- **`describe` entry:** no new command; option/behavior documentation may update existing command descriptions.
- **Envelope:** validation failures remain structured `CliResult` diagnostics with next actions.
- **Channel boundary:** proof minting in `Hx.Gate.Core`; proof validation in `Hx.Cycle.Core`; CLI command bodies stay wiring-only.

## Command Availability

| Area | Command | Status |
| --- | --- | --- |
| Restore | `dotnet restore .\scaffold-dotnet.slnx` | implemented |
| Build | `dotnet build .\scaffold-dotnet.slnx -c Release --no-restore /m:1` | implemented; must be re-run and RCA'd if local no-diagnostic failure persists |
| Test | `dotnet test .\scaffold-dotnet.slnx -c Release --no-build /m:1` | implemented |
| Describe | `dotnet run --project tools/<Tool>.Cli -- describe --json` | implemented; must remain stable |
| Help | `dotnet run --project tools/<Tool>.Cli -- <path> --help` | implemented today via mixed/default paths; unified nested rich/plain behavior is planned |
| Gate | `dotnet run --project tools/Hx.Runner.Cli -- gate run --repo . --profile normal --json` | implemented; affected-proof binding planned |
| Impact plan | `dotnet run --project tools/Hx.Impact.Cli -- plan --repo . --base <ref> --head <ref> --json` | implemented; proof hash/export additions planned |
| Cycle check/commit | `dotnet run --project tools/Hx.Runner.Cli -- doti cycle check/commit ... --json` | implemented; stricter proof validation planned |
| Skill drift | `dotnet run --project tools/Hx.Runner.Cli -- doti render-skills --repo . --agents codex,claude --check --json` | implemented |
| Local hx.exe | packaging/publish command | implemented or discovered from packaging files before use; local artifact only, not release |

## Constitution Check (after design)

PASS after design:

- The design keeps human output in the kernel and validation in core.
- It strengthens deterministic ownership instead of adding advisory-only process.
- It does not introduce shell runners, release actions, committed binaries, or generated-file hand edits.
- It preserves the machine `CliResult`/`describe` contract.
- It keeps release excluded and uses only local artifact creation for `hx.exe` testing.

## Complexity Tracking

No justified constitution violations.

## Risks

- **System.CommandLine help integration risk:** target-command resolution for nested paths must be tested with root, group, leaf, `help <path>`, and `<path> --help` forms.
- **Contract compatibility risk:** proof fields must be additive/versioned and covered by serialization tests.
- **False rejection risk:** `cycle commit` recomputation must use the same base ref/change-set identity as gate persistence, or valid proofs will appear stale.
- **Over-tightening stage stamps:** later-stage stamp prerequisites must not prevent legitimate first-cycle bootstrap; tests need first-stage and out-of-order cases.
- **Local build blocker:** prior local `dotnet run`/`dotnet build` attempts failed before useful diagnostics. The implementation lane must RCA this before claiming gate proof.
- **Scope control:** do not run release. Local `hx.exe` packaging is test-prep only.
