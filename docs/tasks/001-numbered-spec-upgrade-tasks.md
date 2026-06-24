# Tasks: Numbered spec upgrade

> Plan: `docs/plans/001-numbered-spec-upgrade-plan.md`.

## Tasks

- [x] `T001` (FR-001, FR-002, FR-003, SC-001) - Add numbered slug validation in `tools/Hx.Cycle.Core/CycleFeatureSlug.cs` and wire first-stage stamp validation in `tools/Hx.Cycle.Core/CycleService.Stamp.cs`.
- [x] `T002` (FR-002, FR-003) - Update `tools/Hx.Runner.Cli/RunnerCommands.DotiCycle.cs` and `tools/Hx.Runner.Cli/RunnerCommandFactory.Cycle.cs` so invalid slugs return structured usage diagnostics and `describe --json` exposes numbered `--feature` guidance.
- [x] `T003` (FR-007, SC-004) - Update `src/Hx.Scaffold.Core/Update/ScaffoldUpdateService.Mutation.cs` so `hx update` reports the v0.5 legacy spec follow-up without renaming project docs.
- [x] `T004` (FR-001, FR-005, FR-006, FR-008, FR-009) - Update source workflow/spec/agent templates and README with numbered slug and legacy-upgrade rules.
- [x] `T005` (FR-008, SC-005) - Re-render installed `.agents`, `.claude`, `.doti/agent-context.md`, `AGENTS.md`, and `CLAUDE.md` from source.
- [x] `T006` (FR-002, FR-003, SC-001) - Add runner tests for unnumbered first-stage slug rejection.
- [x] `T007` (FR-004, SC-002) - Add runner test proving an open unimplemented legacy spec can be renamed and re-stamped with a numbered slug.
- [x] `T008` (FR-005, FR-006, SC-003) - Add runner test proving a completed legacy unnumbered spec remains while the next spec uses a numbered slug.
- [x] `T009` (FR-007, SC-004) - Add scaffold update test asserting the follow-up mentions open unimplemented specs and completed legacy preservation.
- [x] `T010` (SC-005, SC-006) - Run Doti renderer checks, focused tests, normal gate, release gate, and `git diff --check`.
- [x] `T011` (cycle recovery) - Stamp the retrofitted doti cycle stages from `specify` through `drift-review`, stage the scoped files, and commit through `doti cycle commit`.

## Dependencies

- `T001` blocks `T002` and `T006`.
- `T003` blocks `T009`.
- `T004` blocks `T005`.
- `T006`, `T007`, `T008`, and `T009` block `T010`.
- `T010` blocks `T011`.

## Coverage

| Requirement | Task(s) |
| --- | --- |
| FR-001 | T001, T004 |
| FR-002 | T001, T002, T006 |
| FR-003 | T001, T002, T006 |
| FR-004 | T007 |
| FR-005 | T004, T008 |
| FR-006 | T004, T008 |
| FR-007 | T003, T009 |
| FR-008 | T004, T005 |
| FR-009 | T004 |
| SC-001 | T006, T010 |
| SC-002 | T007, T010 |
| SC-003 | T008, T010 |
| SC-004 | T009, T010 |
| SC-005 | T005, T010 |
| SC-006 | T010 |

## Gate Notes

- Focused tests passed before this retrofit: runner, scaffold, and Doti renderer tests.
- Normal and release gates passed before this retrofit, but adding these docs changes the diff identity. The gate proof must be refreshed before `doti cycle commit`.
- The release lane passing is not a sanctioned release because the current feature still needs a Doti cycle commit.
