# Spec: Agent-first CLI self-description in the doti workflow

> WHAT and WHY only. The doti workflow ships an agent-first CLI (the `CliResult` envelope, a `describe` capability model, and a frozen structured error-code catalog), but the workflow does not yet *direct* an agent to use them. This feature encodes that usage so a doti-driven agent self-orients and self-corrects, and so new work is designed to be self-describing.

## Goal

Make a doti-driven agent **fully and automatically use the scaffold's rich error-coding and help system** — `describe` (the machine-readable capability model) and the structured diagnostics (`<PREFIX><NNNN>` code + `hint` + `nextActions` on the `CliResult` envelope) — so that:

1. the agent learns a CLI's surface before driving it (self-orienting), and self-corrects from structured diagnostics instead of guessing or blind-retrying; and
2. new commands/operations are **designed** to be self-describing — their error contract and `describe` surface are declared at plan time and registered at implement time.

Today the agent context *documents* these capabilities but never instructs the agent to use them, and the plan stage has no error/CLI-surface contract — so the system is under-used.

## Scope

**Included**
- A cross-cutting **usage directive** in the agent context (loaded by every stage): call `describe` before driving a scaffold CLI; act on the envelope's `code`/`hint`/`nextActions` on any non-Success result.
- A **plan-stage design requirement**: for any new command/operation, declare its error contract (emitted error codes, exit class, `describe` entry, envelope conformance) — encoded in the plan template and the `doti-plan` command.
- An **implement-stage execution step**: register the declared codes in `errorcodes/registry.json`, run the `errorcodes check` stability gate, and confirm `describe` reflects the new surface — encoded in the `doti-implement` command.
- Re-rendering the installed skills + agent context from source so consumers receive the change.

**Excluded**
- Any change to the CLI kernel, the `describe` implementation, the error-code registry mechanism, or the envelope schema (they already exist and work).
- Adding new CLI commands or error codes as part of this feature.
- Changing gate behavior or adding new gates.

## Functional Requirements

- `FR-001`: The agent context MUST direct agents to run `describe --json` to learn a scaffold CLI's command/option tree, exit classes, and error-code catalog **before** driving that CLI.
- `FR-002`: The agent context MUST direct agents, on any non-Success `CliResult`, to act on the diagnostics `code` (`<PREFIX><NNNN>`), `hint`, and `nextActions` rather than guessing or blindly retrying.
- `FR-003`: The plan template MUST contain a section requiring, for any new command/operation, a declared **error contract**: the structured error codes it emits, its exit class, its `describe` entry, and that it returns the `CliResult` envelope. The section is omitted only when the feature adds no CLI surface.
- `FR-004`: The `doti-plan` command MUST instruct the planner to produce that error-contract section.
- `FR-005`: The `doti-implement` command MUST require, when a plan declared new error codes, registering them in `errorcodes/registry.json`, running `errorcodes check` (the append-only stability gate), and confirming `describe` reflects the new surface.
- `FR-006`: The change MUST be single-sourced in `doti/core` and the installed skills + agent context MUST be re-rendered so no drift remains.

## Success Criteria

- `SC-001`: A plan produced by `doti-plan` for a feature that adds a CLI command contains a populated "CLI surface & error contract" section (observable in the plan document).
- `SC-002`: After the change, `doti render-skills --check` reports no skill/agent-context drift.
- `SC-003`: The directive and plan requirement reach a consumer repo on its next `doti install`/render, because the source lives only in `doti/core` (single-sourced).
- `SC-004`: Existing gates remain green — the change introduces no failing build, test, or architecture gate.

## Deterministic Surfaces

- Source (edited): `doti/core/templates/agent-context-template.md`, `doti/core/templates/plan-template.md`, `doti/core/templates/commands/doti-plan.md`, `doti/core/templates/commands/doti-implement.md`.
- Rendered (regenerated): `.doti/agent-context.md`, `.claude/skills/doti-*`, `.agents/skills/doti-*`.
- Command-backed: `doti render-skills [--check]` (drift gate); `errorcodes check` (the existing append-only stability gate the implement step invokes).
- The `describe` and `errorcodes` commands referenced are **already implemented** — this feature only directs their use; nothing here is planned-but-absent.

## Architecture Impact

None. The change is template/markdown content under `doti/core` plus their rendered outputs. No projects, namespaces, layers, or `rules/architecture.json` families are added or moved; `.sentrux/rules.toml` is unchanged.

## Sentrux And Hygiene Impact

None expected. Markdown/template content only; no secrets, no new untracked binaries, no public-hygiene risk. `doti/` is in the Sentrux scan graph but contains no code edges.

## Assumptions

- Consumer repos are scaffolded .NET projects that carry the agent-first CLI kernel, so `describe` and the error-code catalog exist there for the directive to apply.
- The directive is guidance (a behavioral nudge); it is not — and cannot be — a deterministic gate. The plan-section requirement *is* checkable in review/analyze.
- "Help system" here means the agent-first machine contract (`describe` + structured diagnostics), not a human `--help` text.

## Acceptance

- **Command-backed today:** `doti render-skills --check` (no drift); build + test on CI (no regression).
- **Advisory:** whether an agent actually consults `describe` / acts on diagnostics is a behavioral expectation, surfaced and reviewed (`doti-analyze`/`doti-arch-review`), not deterministically proven.

## Clarifications

### 2026-06-20
- No blocking ambiguities. The three-part split (usage in the agent context; design requirement in the plan; execution in implement) was settled with the operator before specify; assumptions are recorded above. No `[NEEDS CLARIFICATION]` markers remain.
