# Tasks: Gate & Affected-Test Visibility

Plan: `docs/plans/012-affected-test-plan-visibility-plan.md`. Spec: `docs/specs/012-affected-test-plan-visibility.md`. **Priority mode = workflow/tooling: safety-first** — preserve the fail-closed proof + the 008 FR-020 boundary before ergonomics. Phases sequential; T015 is the final gate. Test-first for code.

## Phase 0 — Additive contracts (foundational) — Checkpoint: `gate run` green

- [ ] T001 [test] Contract additions are additive (schema unchanged): `GateStep.DurationMs` (nullable), `ChangeSummary`, `AffectedTestInventory`, `GateTrace`, `GateRunResult.Trace` (nullable) — a pre-feature JSON still deserializes; `GateProof`/`AffectedTestProof` hashes are byte-unchanged by the new trace records — `test/Hx.Tooling.Tests/` (or `test/Hx.Gate.Tests/`) — [covers FR-009, FR-011, FR-017]
- [ ] T002 Add the records/fields to `Hx.Tooling.Contracts`: `GateStep += long? DurationMs`; `ChangeSummary(Source, Test, Docs, Other, LinesAdded, LinesRemoved, Files, ClassesTouched, ClassesIncluded)`; `AffectedTestInventory(SelectedProjects, TotalProjects, SelectedCases?, TotalCases?, SelectedClasses?, TotalClasses?, UnknownReason?)`; `GateTrace(Scope, Change, Tests?, Steps, TotalMs, EffectiveMode)`; `GateRunResult += GateTrace? Trace` — `tools/Hx.Tooling.Contracts/` — [covers FR-007, FR-019]

## Phase 1 — Capture (change summary, test inventory, timing) — Checkpoint: `gate run` green

- [ ] T003 [test] `ChangeSummaryProjector`: files-by-category (source/test/docs/other), lines ± from `--numstat`, classes-touched from changed `.cs` (lexer-aware top-level types; brace-in-string/comment safe); deterministic ordering; capped; injected git runner — `test/Hx.Impact.Tests/ChangeSummaryTests.cs` — [covers FR-020, FR-013, FR-018]
- [ ] T004 `ChangeSummaryProjector` in `Hx.Impact.Core` (reuses `ChangeSetContext` for files/categories; a `git diff --numstat` reader; a C# top-level-type scanner modeled on the 009 `CSharpMemberChunker` — zero Roslyn) — `tools/Hx.Impact.Core/ChangeDetection/ChangeSummaryProjector.cs` — [covers FR-020]
- [ ] T005 [test] `AffectedTestInventory`: selected/total **test projects** from the project graph; selected class/case from the built (selected) assemblies; repo-wide class/case `total` marked `unknown — not enumerated` when not cheaply available (NEVER build all test projects); docs-only → no inventory — `test/Hx.Impact.Tests/AffectedTestInventoryTests.cs` — [covers FR-003, FR-004, FR-005]
- [ ] T006 `AffectedTestInventory` computation in `Hx.Impact.Core` (project totals from the graph; selected class/case via enumerating the already-built selected test assemblies; honest unknowns) — `tools/Hx.Impact.Core/Planning/` — [covers FR-002, FR-003, FR-004]
- [ ] T007 Per-step timing: `GateRunner.Run` wraps each step in a `Stopwatch` and populates `GateStep.DurationMs`; total elapsed retained — `tools/Hx.Gate.Core/GateRunner.cs` — [covers FR-019]

## Phase 2 — Trace projector + kernel rendering — Checkpoint: `gate run` green

- [ ] T008 [test] `GateTraceProjector`: assembles `GateTrace` from `GateProof` + `ChangeSetContext` + `AffectedPlan` + durations + the injected `implement-stage code` flag; **two-tier** — basic change summary always, classes+inventory only when implement-stage AND code (FR-021); docs-only → scope + basic only; deterministic — `test/Hx.Gate.Tests/GateTraceTests.cs` — [covers FR-007, FR-008, FR-014, FR-020, FR-021]
- [ ] T009 `GateTraceProjector` in `Hx.Gate.Core`; wire it into `RunnerCommands.GateRun` (build the trace, set `GateRunResult.Trace`); the implement-stage flag resolved from `CycleStateStore` in the runner (engine stays stage-agnostic) — `tools/Hx.Gate.Core/GateTraceProjector.cs`, `tools/Hx.Runner.Cli/RunnerCommands.Gates.cs` — [covers FR-007, FR-009, FR-021]
- [ ] T010 [test] Kernel render: `OnEvent` colors the bar by `CliEvent.Status` (skip = muted+reason, fail = red+reason); `WriteGateSummary` renders scope line + basic change summary + (detailed line when present) + per-step ladder (icon·name·duration·reason) + total elapsed; all bounded/capped (no dump); human == JSON trace — `test/Hx.Cli.Kernel.Tests/` — [covers FR-014, FR-015, FR-016, FR-018, FR-019, SC-008..013]
- [ ] T011 Kernel render in `Hx.Cli.Kernel/CliRenderer` (`OnEvent` outcome-aware; `WriteGateSummary(GateTrace)` invoked when `result.Data is GateRunResult { Trace: {} }`) — `tools/Hx.Cli.Kernel/CliRenderer.cs` — [covers FR-014, FR-015, FR-016]
- [ ] T012 `gate run --stream` (codex WI-3): NDJSON `CliEvent`s before `restore-build-test` + the final trace in the envelope; `describe` entry adds `--stream`; thin wiring (events already exist) — `tools/Hx.Runner.Cli/RunnerCommandFactory.Gates.cs`, `RunnerCommands.Gates.cs` — [covers FR-007, FR-008, FR-009]

## Phase 3 — Boundary proof + verification — Checkpoint: `gate run` green

- [ ] T013 [test] **008 FR-020 boundary:** ArchUnit — `*ProofHasher` MUST NOT depend on `GateTrace`/`ChangeSummary`/`AffectedTestInventory` (review context never a proof-hash input); and a test that the persisted gate-proof hash is byte-identical with vs without the trace populated — `test/Hx.Architecture.Tests/ArchitectureTests.cs`, `test/Hx.Gate.Tests/` — [covers FR-011, SC-007]
- [ ] T014 Code↔docs: the agent context + the `gate run` help/`describe` reflect `--stream` and the human trace; no stale claim that gate output is summary-only — `.doti/core/templates/agent-context-template.md` → re-render — [covers code↔docs]
- [ ] T015 Run `gate run --profile normal` green; the proof/validation is unchanged (visibility-only); `render-skills --check` + `payload check` clean; the new ArchUnit boundary (T013) passes — [verifies FR-011, SC-006, SC-007]
