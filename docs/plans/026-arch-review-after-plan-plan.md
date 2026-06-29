# 026 — Plan: Arch-review after plan (cycle stage reorder)

## Summary

Reposition `arch-review` from stage 6 (after `analyze`) to stage 4 (immediately after `plan`), shifting `tasks`→5 and `analyze`→6, so the design is reviewed before tasks/analyze depend on it. The stage model is *declarative and duplicated across three coordinated sources* — the cycle-enforcement YAML, a code-level rendering registry, and the prose that references stage numbers. The design edits all three in place (no new abstraction), re-renders the installed assets, and proves the new order via the existing fail-closed gates.

## Technical Context

The 9-stage cycle's order/number/prerequisites live in **three** places that must stay mutually consistent:

1. **`.doti/core/workflows/doti/workflow.yml`** (schemaVersion 2) — the **backward prerequisite chain** + per-stage `command`, loaded by `Hx.Cycle.Core/StageModel`. Drives `doti cycle stamp`/`check` fail-closed enforcement. Today: `tasks.prereqs=[plan]`, `analyze.prereqs=[tasks]`, `arch-review.prereqs=[analyze]`, `implement.prereqs=[arch-review]`.
2. **`tools/Hx.Doti.Core/Workflow/DotiWorkflowRegistry.cs`** — the **rendering identity**: each `Stage(ordinal, stageId, commandName, …)` yields `SkillId = $"{ordinal:D2}-{commandName}"` (so the `06-doti-arch-review` file number comes from the *ordinal here*), plus the **forward `NextStageIds`** and the **`nextStep`** prose. Today ordinals: tasks=4, analyze=5, arch-review=6.
3. **Prose cross-references** to `/0N-doti-*`: `.doti/core/skills.json` (`nextStage` + in-body descriptions), `.doti/core/templates/commands/*.md` (the skill bodies — `doti-implement`/`doti-plan`/`doti-analyze`/`doti-arch-review`/`doti-tasks`/`doti-constitution`/`doti-specify`), and `.doti/core/templates/{agent-context,constitution,spec}-template.md`, plus `README.md`.

Downstream: the installed `.claude/`+`.agents/` skills, `.doti/agent-context.md`, `CLAUDE.md`/`AGENTS.md`, and `.doti/workflows/doti/workflow.yml` are **rendered** from (1)–(3) and must be regenerated. Other code mentions of the numbers (`ImpactCommands.cs`, `SentruxOptimizationTracker.cs`, `SentruxRebaselinePolicy.cs`, `AffectedPlan.cs`, generated `ErrorCodes.g.cs`) are to be triaged in implement (expected: comments/examples or generated, not functional ordering).

## Constitution Check (gate)

- **§1 inherited invariants** — *Codified Cycle*: this changes the cycle's order but keeps it fully codified, fail-closed, and proof-bound (no stage removed, no enforcement weakened) — PASS. *Deterministic Ownership*: stage IDs, `produces` patterns, and `cycle-state.json` schema are unchanged, so existing proofs stay interpretable and new stamps bind as before — PASS. *Template Boundary*: the edited `.doti/**` prose is Doti workflow prose (no-code), not `scaffold/templates/**` generated code — PASS. *Engineering Discipline / Channel Independence*: no gate downgraded, no version policy touched — PASS.
- **§2 project declarations** — re-read fresh at implement via `hx doti constitution`; a workflow-ordering change touches no domain/tech-stack declaration. Expected PASS (re-evaluated post-design).

## Research (resolve unknowns)

- **Decision:** Edit all three stage-model sources in place to the new order; keep the two structural sources (workflow.yml backward chain + registry forward chain/ordinals) mutually consistent, and update every prose reference.
  - **Rationale:** the sources are already declarative; the simplest correct change is a coordinated data/prose edit plus the registry's ordinal/chain/`nextStep` strings. It satisfies all FRs, fits the existing single-edit-then-render pattern, and preserves every fail-closed proof.
  - **Alternatives rejected:** (a) *Unify the duplicated stage model into one source first* — architecturally cleaner (kills the workflow.yml↔registry duplication) but a materially larger refactor with its own blast radius; bundling it here violates the one-concern rule and risks the reorder. Recorded as a follow-up (see Risks). (b) *Renumber only the rendered/installed files* — would desync source↔installed and the registry ordinal, failing parity and re-rendering back to the old numbers; rejected as non-durable.

- **Decision:** Run cycle 026 itself under the *current* (old) order; the new order takes effect for cycles started after this ships.
  - **Rationale:** the installed workflow.yml enforces the old prereq chain during this cycle; arch-review (06) is stamped after analyze (05) here. Self-hosting bootstrap reality — unavoidable and correct.
  - **Alternatives rejected:** trying to run this cycle in the new order — the fail-closed prereq check would refuse `arch-review` before `analyze`; cannot and should not be bypassed.

## Design

**Selection rule:** simplest correct, in-place edit of the declarative sources; no new types or abstraction (right-sized). Files changed:

- `.doti/core/workflows/doti/workflow.yml` — reorder stage entries to `specify, clarify, plan, arch-review, tasks, analyze, implement, drift-review, release`; set `command:` to `04-doti-arch-review` / `05-doti-tasks` / `06-doti-analyze`; set `prereqs:` to `arch-review:[plan]`, `tasks:[arch-review]`, `analyze:[tasks]`, `implement:[analyze]`.
- `tools/Hx.Doti.Core/Workflow/DotiWorkflowRegistry.cs` — reorder + renumber the `Stage(...)` table (arch-review ordinal 4, tasks 5, analyze 6); fix `NextStageIds` forward chain (`plan→[arch-review]`, `arch-review→[tasks]`, `tasks→[analyze]`, `analyze→[implement]`); rewrite the `nextStep` strings to the new `/0N-doti-*` targets.
- `.doti/core/skills.json` — reorder entries; fix every `nextStage`; fix in-body `/0N-doti-*` cross-references; reword the arch-review description so it no longer says "after analyze"/"before implementation" but "after plan, before tasks/analyze" and corrects "hard prereq of /07".
- `.doti/core/templates/commands/*.md` + `templates/{agent-context,constitution,spec}-template.md` + `README.md` — update every `/0N-doti-*` reference and the arch-review positioning prose.
- Re-render: `doti render-skills` (regenerates `.claude/`+`.agents/` skills, agent-context, entrypoints) + ensure `.doti/workflows/doti/workflow.yml` installed copy matches source.

**Patterns assessed:** the change lives in `*.Core` data + Doti prose; the `*.Cli`/`*.Core` boundary is untouched (no command parse/delegate/render change). No `rules/architecture.json` family or `.sentrux/rules.toml` boundary changes — no new layer/dependency. `scaffold/templates/**` generated code is not involved.

**Architecture delta:** none structural. No ArchUnitNET family added/changed; no Sentrux boundary changed. The `DotiWorkflowRegistry` edit is a static-table value change (no new control flow). No new namespaces/projects/layers.

## CLI surface & error contract

No CLI command added or changed; no new error codes; no `describe` surface delta. (Stage commands are renamed by *number*, but that is the rendered SkillId, not a new CLI verb — `hx` itself exposes `doti cycle …`, unchanged.)

## Command Availability

| Area | Command | Status |
| --- | --- | --- |
| Build | `dotnet build .\scaffold-dotnet.slnx -c Release --no-restore` | implemented |
| Test | `dotnet test .\scaffold-dotnet.slnx -c Release --no-build` | implemented |
| Cycle prereq | `hx doti cycle check` | implemented |
| Render parity | `hx doti render-skills --check` | implemented |
| Payload parity | `hx doti payload check --repo .` | implemented |
| Gate | `hx gate run --repo . --profile normal --json` | implemented |

## Complexity Tracking

No constitution violation; table intentionally empty.

## Risks

- **Source desync (primary risk):** if `workflow.yml` and `DotiWorkflowRegistry.cs` are updated inconsistently, the cycle could enforce one order while rendering another. Mitigation: in implement, search for any existing test asserting workflow.yml↔registry consistency and rely on it; if none exists, add an assertion test that the registry ordinals/commands match the workflow.yml stage order. `doti cycle check` + `render-skills --check` + the test suite are the proof.
- **Stale reference miss:** a `/06-doti-arch-review`-class reference left in a doc/comment. Mitigation: `SC-004` repo-wide grep is a hard acceptance check; the CHANGELOG history is the only allowed exception.
- **Duplicated stage model (pre-existing debt, deliberately not fixed here):** the three-source duplication is a design smell; unifying it is recorded as a separate follow-up rather than risked inside this reorder.
- **In-flight cycles under the old order** read an unsatisfied prereq under the new graph — expected and documented in the spec edge cases (re-run the now-out-of-order stage); not a regression.
