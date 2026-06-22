# Tasks: Unified CLI help and doti cycle lockdown

> Plan: `docs/plans/unified-help-and-cycle-lockdown-plan.md`.

## Tasks

- [x] `T001` (FR-001, FR-002, FR-003, FR-004, FR-005, FR-006, FR-008, SC-001, SC-002, SC-003, SC-004) — Add kernel help-rendering tests in `test/Hx.Cli.Kernel.Tests`: root help, nested group help, leaf help, `help <path>`, `<path> --help`, rich mode uses the shared style, plain mode emits no ANSI, and a test-only nested command appears without custom renderer code.
- [x] `T002` (FR-009, FR-010, FR-011, FR-012, FR-013, FR-014, SC-005, SC-006, SC-007) — Add gate/cycle proof tests in `test/Hx.Runner.Tests`: missing affected proof rejected, stale plan hash rejected, stale selected-test-set hash rejected, no-tests-required recomputation mismatch rejected, full-gate escalation proof accepted only when full test execution is recorded, and direct test transcript evidence is ignored.
- [x] `T003` (FR-015, FR-016, FR-017, FR-018, FR-019, FR-020, FR-021, FR-022, SC-008, SC-009) — Add cycle lockdown tests in `test/Hx.Runner.Tests`: out-of-order later-stage stamp fails, prerequisite proof hashes are validated, generated-skill drift is reported, unexpected Sentrux baseline edit is reported, and diagnostics include next actions.
- [x] `T004` (FR-001, FR-002, FR-003, FR-004, FR-005, FR-006, FR-008) — Implement `HelpMode`, `CommandHelpModel`, and shared rich/plain help renderers in `tools/Hx.Cli.Kernel/Help/*`; derive command/subcommand/option/argument/default content from `System.CommandLine`.
- [x] `T005` (FR-001, FR-004, FR-005, FR-007, SC-010) — Update `tools/Hx.Cli.Kernel/CliApp.cs` to intercept root/group/leaf help for every command path, support `auto|rich|plain` via shared option/environment/no-color behavior, and preserve `--json`/`describe --json`.
- [x] `T006` (FR-001, FR-002, FR-003, SC-001, SC-002) — Route `tools/Hx.Runner.Cli/Program.cs` and `tools/Hx.Impact.Cli/Program.cs` through `CliApp.Invoke`; keep `tools/Hx.Scaffold.Cli/Program.cs` on the same shared entry path.
- [x] `T007` (FR-001, FR-002, FR-003, SC-004) — Update generated template CLI/kernel assets under `scaffold/templates/dotnet-cli` so generated repos inherit unified rich/plain nested help; refresh template golden tests.
- [x] `T008` (FR-009, FR-010, FR-012, FR-013) — Add `AffectedTestProof` contracts in `tools/Hx.Tooling.Contracts` with canonical plan/selected/execution hashes and minimal per-target execution records.
- [x] `T009` (FR-009, FR-010, FR-012, FR-013) — Update `tools/Hx.Gate.Core/GateRunner.cs` to mint `AffectedTestProof` during `gate run`, including affected, no-tests-required, full-gate escalation, and release/full-suite paths.
- [x] `T010` (FR-011, FR-014, FR-018, SC-005, SC-006, SC-007) — Add `GateProofValidator` in `tools/Hx.Cycle.Core` and wire `CycleService.Commit` to reject missing, malformed, stale, lane-mismatched, manually edited, or recomputation-mismatched proofs.
- [x] `T011` (FR-015, FR-016, FR-017, FR-019, FR-020, FR-021, FR-022, SC-008, SC-009) — Extend cycle stage proofs with prerequisite-proof hashes; stamp/check reject out-of-order, stale, or hash-mismatched stage progression.
- [x] `T012` (FR-020, FR-021) — Update single-sourced Doti guidance in `doti/core/skills.json` and `doti/core/templates/agent-context-template.md` so agents treat direct `dotnet test` and other bypass commands as diagnostic/advisory only; re-render installed `.agents`, `.claude`, and `.doti` files.
- [x] `T013` (SC-001, SC-002, SC-003, SC-010) — Verify help behavior with command-backed probes: `hx-scaffold --help`, `hx-scaffold new --help`, `hx-runner doti cycle --help`, `hx-runner doti cycle stamp --help`, `hx-impact plan --help`, and the same in plain mode.
- [x] `T014` (SC-005, SC-006, SC-007, SC-008, SC-009, SC-010) — Run focused tests: `test/Hx.Cli.Kernel.Tests`, `test/Hx.Runner.Tests`, `test/Hx.Impact.Tests`, and template golden tests.
- [x] `T015` (SC-010) — Run command-backed checks: `dotnet restore`, `dotnet build`, `dotnet test`, `doti render-skills --check`, `architecture test`, and `gate run --profile normal`; treat failures as blocking.
- [x] `T016` (local-test artifact, no release) — Discover the existing packaging command and produce a local latest `hx.exe` artifact for operator testing; do not run `/doti-release`, do not tag, do not push, and do not publish a release.
- [ ] `T017` (cycle) — Stamp `implement`, run `/doti-drift-review`, stamp `drift-review`, persist a fresh gate proof, stage the scoped files, and commit through `doti cycle commit`; stop before `release`.

## Dependencies

- `T001` blocks `T004`-`T007`.
- `T002` blocks `T008`-`T010`.
- `T003` blocks `T011`.
- `T004` blocks `T005`; `T005` blocks `T006`; `T006` blocks `T013`.
- `T008` blocks `T009`; `T009` blocks `T010`.
- `T011` and `T012` depend on the proof-validation design in `T010`.
- `T014` depends on implementation tasks `T004`-`T012`.
- `T015` depends on `T014`.
- `T016` depends on a green non-release gate from `T015`.
- `T017` depends on `T015` and scoped staging.

## Coverage

| Requirement | Task(s) |
| --- | --- |
| FR-001 | T001, T004, T005, T006, T007 |
| FR-002 | T001, T004, T006, T007 |
| FR-003 | T001, T004, T006, T007 |
| FR-004 | T001, T004, T005 |
| FR-005 | T001, T004, T005 |
| FR-006 | T001, T004 |
| FR-007 | T005 |
| FR-008 | T001, T004, T005 |
| FR-009 | T002, T008, T009 |
| FR-010 | T002, T008, T009 |
| FR-011 | T002, T010 |
| FR-012 | T002, T008, T009 |
| FR-013 | T002, T008, T009 |
| FR-014 | T002, T010 |
| FR-015 | T003, T011 |
| FR-016 | T003, T011 |
| FR-017 | T003, T011 |
| FR-018 | T003, T010 |
| FR-019 | T003, T011 |
| FR-020 | T003, T011, T012 |
| FR-021 | T003, T011, T012 |
| FR-022 | T003, T011 |
| SC-001 | T001, T013 |
| SC-002 | T001, T013 |
| SC-003 | T001, T013 |
| SC-004 | T001, T007 |
| SC-005 | T002, T010, T014 |
| SC-006 | T002, T010, T014 |
| SC-007 | T002, T010, T014 |
| SC-008 | T003, T011, T014 |
| SC-009 | T003, T011, T014 |
| SC-010 | T005, T013, T014, T015 |

## Gate Notes

- Manual review is not deterministic gate proof.
- Unified nested help, affected-test proof binding, prerequisite proof hashes, and generated guidance have landed; the normal gate proof must be fresh before commit.
- Build and Doti CLI execution may require elevated process/cache access in this environment; any such use is operational, not a new workflow surface.
- Release is explicitly out of scope for this cycle run. The local `hx.exe` artifact is for operator testing only.
