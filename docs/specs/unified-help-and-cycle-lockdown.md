# Spec: Unified CLI help and doti cycle lockdown

> WHAT and WHY only. This feature fixes two trust breaks in the current scaffold experience: human help is only partly upgraded to the rich console style, and the doti cycle proof is not explicit enough about the affected-test evidence and sanctioned command surfaces that make a commit trustworthy.

## Goal

Every scaffold CLI must present a coherent human help experience, and every doti cycle commit must be backed by tamper-evident, command-produced proof that the correct gate path ran for the current change set.

The immediate user pain is visible in the CLI: rich root help and `new` progress exist, but subcommand help such as `new --help`, nested runner menus, and the impact planner still fall back to bland default output. The system needs one modular help design so every command and submenu automatically inherits the same table style, colors, wording density, and plain-output fallback.

The cycle pain is about trust: agents can run `dotnet test` directly, stamp stages, or point at ad hoc transcripts. The workflow must make those actions advisory only. The commit chokepoint must accept only proof produced by the sanctioned doti/gate commands, bound to the current change set, the affected-test plan, and the exact test execution scope.

## Current Review Findings

- `Hx.Scaffold.Cli` calls the branded help entry point for root-level help, but `Hx.Runner.Cli` and `Hx.Impact.Cli` still invoke `System.CommandLine` directly.
- The current branded help interception is root-only. Per-subcommand help such as `new --help`, nested groups such as `doti cycle --help`, and leaf commands such as `doti cycle stamp --help` are still owned by the default help renderer.
- The current rich renderer has a single root command table, but no reusable command-help model for nested command groups, options, arguments, aliases, defaults, examples, or inherited/global options.
- `gate run` already owns affected-test execution inside the gate, and `cycle commit` already requires a fresh passing persisted `GateProof` whose change-set identity matches the current diff.
- The persisted gate proof is currently thin: it records step names and human-readable evidence messages, but not a canonical affected-plan hash, selected-test-set hash, per-test command hash, or per-target execution identity.
- `doti cycle stamp` records diff-bound stage proofs, but review/diff stages can be stamped without a stage-specific proof payload that demonstrates the expected tool or review actually ran.
- The pre-commit hook is correctly described as insurance, not a hard security boundary: a local user can bypass hooks or set the sentinel. The workflow therefore needs command-backed detection and refusal at `cycle check` / `cycle commit`, not reliance on hook presence.

## Scope

**Included**
- A single human-help rendering contract shared by `Hx.Scaffold.Cli`, `Hx.Runner.Cli`, `Hx.Impact.Cli`, and generated template CLIs.
- Consistent rich help for root menus, nested command groups, and leaf commands, including options and arguments.
- A plain vanilla help mode for terminals, logs, screen readers, CI consoles, or users that cannot render ANSI reliably.
- Automatic inheritance: adding a command or option to a CLI makes it appear in the same rich/plain help style without hand-building menu tables per command.
- Cycle hardening for affected-test proof: the gate proof must identify the affected plan, selected test projects, exact test command scope, and execution result for the current change set.
- Cycle hardening for sanctioned-tool use: direct `dotnet test`, direct `git commit`, manual proof edits, manual version tags, direct baseline changes, and hand-edited generated skills/docs are advisory or suspicious unless reconciled by the expected doti command surface.
- Clear agent instructions that only sanctioned command outputs are commit-qualifying proof; ad hoc shell transcripts are diagnostic evidence only.

**Excluded**
- Replacing `describe --json` or the `CliResult` envelope. Machine output stays JSON-first and byte-stable.
- Removing `System.CommandLine` as the parser. This feature concerns the human help surface and proof policy, not argument parsing.
- Treating local hooks as an unbypassable security boundary. Hooks remain insurance; command-backed checks own enforcement.
- Preventing a malicious OS user from modifying local files. The target is deterministic, tamper-evident workflow enforcement for agents and honest contributors, with bypasses detected or refused by the sanctioned tools.
- Changing the release lane's full-suite requirement. Release still runs the full suite.

## Functional Requirements

### A. Unified human help

- `FR-001`: Every scaffold CLI MUST route human help for root commands, command groups, nested subcommands, and leaf commands through one shared help renderer.
- `FR-002`: Rich help MUST use one shared style contract for tables, borders, headings, command names, option names, descriptions, diagnostics, and muted guidance across every menu and submenu.
- `FR-003`: The help renderer MUST derive its content from the command model so new commands, nested subcommands, options, arguments, aliases, defaults, and inherited/global options appear automatically without per-command table code.
- `FR-004`: Users MUST be able to force plain vanilla help through a documented CLI/environment control, and plain help MUST contain no ANSI escape sequences or Spectre markup.
- `FR-005`: Help rendering MUST auto-select a compatible mode when output is redirected, ANSI is unavailable, or a standard no-color signal is present.
- `FR-006`: Rich and plain help MUST expose equivalent command, option, argument, default, and description content so plain mode is not a reduced or second-class help path.
- `FR-007`: The existing `--json` machine-output contract and `describe --json` capability model MUST remain unchanged by human help rendering.
- `FR-008`: Human help MUST remain readable for deeply nested commands such as `doti cycle stamp --help`, including parent command context and the full invocation path.

### B. Affected-test proof hardening

- `FR-009`: `gate run` MUST persist a canonical affected-test proof for the current change set whenever the normal or advisory lane narrows tests through the affected-test planner.
- `FR-010`: The affected-test proof MUST bind the change-set identity, project graph identity, changed-path classification, affected-plan outcome, selected test project list, exact test command scope, and per-target execution result.
- `FR-011`: `cycle commit` MUST recompute the current affected-test plan and reject a gate proof whose affected-plan identity or selected-test-set identity no longer matches the current change set.
- `FR-012`: When the affected planner escalates to full-gate, the proof MUST record the escalation reason and prove the full test suite path was selected for that lane.
- `FR-013`: When the affected planner reports no tests required, the proof MUST record the changed-path classifications and the no-test reason, and `cycle commit` MUST reject the proof if recomputation no longer reaches the same no-test outcome.
- `FR-014`: Direct `dotnet test` output, TRX files, pasted transcripts, or manually supplied test commands MUST NOT be accepted as commit-qualifying proof by `cycle check` or `cycle commit`.

### C. Sanctioned workflow lockdown

- `FR-015`: Each cycle stage that advances the workflow MUST have a stage-appropriate proof payload or artifact hash that demonstrates the expected doti stage output exists and is fresh.
- `FR-016`: `doti cycle stamp` MUST fail closed when stamping a stage whose transitive prerequisites are missing, stale, invalid, or unresolved, except for the first stage in a new cycle.
- `FR-017`: Review and diff stages MUST NOT be satisfiable by a bare stage id alone; they MUST carry evidence of the expected review/check result or be rejected by the commit chokepoint.
- `FR-018`: `doti cycle commit` MUST reject a gate proof that is missing required proof fields, has been manually edited into an unverifiable shape, was minted for a different lane, or references an unsanctioned proof source.
- `FR-019`: The workflow MUST define a sanctioned-surface policy that names the proof-producing command for each concern: generation, affected planning, gate execution, security scan, version bump, Sentrux baseline/check, skill rendering, drift review, and commit.
- `FR-020`: Agent-facing instructions MUST state that bypass commands are diagnostic/advisory only unless their result is re-run or reconciled through the sanctioned doti surface.
- `FR-021`: The gate or cycle checks MUST detect and report common bypass patterns that affect committed output, including direct generated-skill edits without source/render alignment, unexpected Sentrux baseline changes, manual version tags, and commits without the doti cycle trailer.
- `FR-022`: Enforcement failures MUST surface stable structured diagnostics and next actions through the existing `CliResult` envelope.

## Success Criteria

- `SC-001`: Running help for at least one root command, one command group, and one leaf command in each scaffold CLI shows the same rich table style and color vocabulary.
- `SC-002`: `new --help`, `doti cycle --help`, `doti cycle stamp --help`, and `plan --help` no longer fall back to default unstyled System.CommandLine help in rich mode.
- `SC-003`: Plain help mode for the same commands emits zero ANSI escape sequences while preserving the same command/option/argument content.
- `SC-004`: Adding a test-only nested command to a CLI automatically produces styled rich help and ANSI-free plain help without custom help-rendering code for that command.
- `SC-005`: A manually edited `.doti/gate-proof.json` that claims success but lacks the canonical affected-test proof is rejected by `doti cycle commit`.
- `SC-006`: A gate proof whose affected-plan hash or selected-test-set hash differs from a recomputed plan for the current change set is rejected as stale or invalid.
- `SC-007`: A direct `dotnet test` transcript, even when all tests pass, cannot satisfy the gate-proof requirement for `doti cycle commit`.
- `SC-008`: Attempting to stamp a later stage before its prerequisites are fresh fails closed with structured diagnostics.
- `SC-009`: Common bypass scenarios are covered by tests: manual proof edit, direct test transcript, stale affected plan, out-of-order stage stamp, generated-skill drift, and unexpected Sentrux baseline change.
- `SC-010`: Existing machine contracts remain stable: `describe --json`, `--json` command output, and `CliResult` schema conformance do not regress.

## Key Entities

- **Help profile** - the selected human help mode: rich, plain, or auto.
- **Help theme** - the shared human-facing color, border, table, heading, and muted-text contract.
- **Command help model** - a parser-derived description of a command path, subcommands, options, arguments, aliases, defaults, and inherited/global options.
- **Affected-test proof** - canonical evidence that binds a change set to the planner outcome, selected tests, exact execution scope, and result.
- **Sanctioned-surface policy** - a machine-readable or generated-document policy naming which doti command owns proof for each workflow concern.
- **Stage proof payload** - the stage-specific artifact hash or structured evidence that proves a cycle stage was actually completed for the current change set.

## Deterministic Surfaces

- Existing command surfaces: `Hx.Scaffold.Cli`, `Hx.Runner.Cli`, `Hx.Impact.Cli`, generated template CLIs, `describe --json`, `--json`, and the `CliResult` envelope.
- Existing help code: `Hx.Cli.Kernel` (`CliApp`, `CliRenderer`) and each CLI `Program.cs`.
- Existing cycle/gate code: `Hx.Cycle.Core` (`CycleService`, `GateProofStore`, `ChangeSetIdentity`, `FreshnessEvaluator`, `PrecommitGuard`), `Hx.Gate.Core` (`GateRunner`), and `Hx.Impact.Core` (`AffectedTestPlanner`, project graph/change collector).
- Existing workflow/config surfaces: `.doti/workflows/doti/workflow.yml`, `.doti/gate-proof.json`, `.doti/cycle-state.json`, `doti/core/skills.json`, rendered `.agents`/`.claude` skills, `rules/architecture.json`, `.sentrux/rules.toml`, and the Sentrux baseline.
- Planned/advisory until implemented: canonical affected-test proof fields, sanctioned-surface policy, proof-source validation, plain/rich help profile selection, and nested help rendering.

## Architecture Impact

- Help rendering remains centralized in the CLI kernel; CLI entry projects stay thin and only wire commands.
- The help model must be reusable by all scaffold CLIs and generated template CLIs without creating command-specific rendering logic.
- Gate/cycle proof validation belongs in core libraries, not CLI command bodies.
- The plan should evaluate whether architecture rules need a new or strengthened family for "human output/help stays in the kernel" and "cycle proof validation stays in core."
- The existing output-confinement and thin-CLI direction remains unchanged: command bodies return `CliResult` or invoke the shared host/renderer; direct console writes outside the approved kernel remain disallowed.

## Sentrux And Hygiene Impact

- No Sentrux baseline should be created by `gate run`; this feature must preserve that invariant.
- If Sentrux baseline changes become a locked-down workflow concern, the expected approval/proof path must be explicit and unexpected baseline edits must fail the relevant cycle/gate check.
- Help snapshots, if added as tests, must be deterministic and must not depend on terminal-specific ANSI capability.
- Proof files remain local/gitignored, but their schema must be strict enough that manual edits are detected as invalid by the commit chokepoint.
- Public hygiene risk is low; no secrets or binaries are expected. Generated/rendered agent files must continue to be source-owned by `doti/core` and drift-checked.

## Assumptions

- Rich help remains the default for capable interactive terminals because that is the user-preferred console experience.
- Plain help is an explicit user-accessibility and compatibility path, not just the behavior when output is piped.
- "Lock down" means deterministic, command-backed, tamper-evident enforcement inside this repo's workflow. It does not mean an untrusted local OS user cannot edit files, disable hooks, or run arbitrary commands.
- Direct `dotnet test` remains useful during debugging, but it cannot be the proof that authorizes a doti cycle commit.
- The exact flag/environment names for selecting help profile can be finalized during `/doti-plan`; the user-facing requirement is that both explicit and auto plain fallback exist.

## Acceptance

- **Command-backed today:** source inspection; `describe --json` contract (when the local build is runnable); existing `gate run`, `doti cycle check`, `doti cycle commit`, `doti render-skills --check`, build/test/architecture gates.
- **Advisory until built:** unified nested help renderer, explicit plain help switch, canonical affected-test proof fields, stricter stage proof payloads, sanctioned-surface policy, and new bypass-detection tests.

## Clarifications

### 2026-06-23
- No blocking clarification markers were left. The brief was specific enough to choose a combined scope: consistent human help across all CLIs/subcommands, plus cycle proof hardening around affected tests and sanctioned command surfaces.
- `/doti-clarify` review asked no operator question because the remaining choices are non-blocking implementation decisions for `/doti-plan`: exact help-mode flag/environment names, the concrete affected-proof schema shape, and whether the sanctioned-surface policy is machine-readable config or generated documentation. The user-facing defaults remain: rich help by default on capable terminals, explicit and automatic plain fallback, direct `dotnet test` treated as diagnostic only, and commit-qualifying proof produced only by sanctioned doti/gate commands.
- Command-backed today: existing `gate run`, affected planning inside the gate, `doti cycle check`, `doti cycle commit`, `doti render-skills --check`, `describe --json`, and the `CliResult` envelope. Advisory until implemented: unified nested help rendering, plain/rich help profile selection, canonical affected-test proof fields, stricter stage proof payloads, sanctioned-surface policy, and bypass-detection tests.
