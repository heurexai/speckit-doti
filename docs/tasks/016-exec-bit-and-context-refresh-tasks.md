# Tasks — 016 Tool-fetch executable bit + agent-context refresh

**Plan:** [docs/plans/016-exec-bit-and-context-refresh-plan.md](../plans/016-exec-bit-and-context-refresh-plan.md). **Stage:** `/04-doti-tasks`.

## Phase 1 — Tool-fetch executable bit (WI-1)

- [x] T001 [test] `ExecutableFileMode.EnsureExecutable`: on non-Windows it sets the owner/group/other execute bits on a written file (assert via `File.GetUnixFileMode`); on Windows it is a no-op that never throws — `test/Hx.Runner.Tests/` — [covers FR-001, FR-003, SC-002] <!-- doti-task-hash: 8555a0d63ab2ac53b9a271b39493b5eca593bd7d7e4927f69e87170780b32af7 -->
- [x] T002 Add `ExecutableFileMode.EnsureExecutable(string path)` in `tools/Hx.Runner.Core/Tools/` (`!OperatingSystem.IsWindows()` guard → `File.SetUnixFileMode` 0755) and call it after the `File.Move` in `ToolFetcher.WriteExecutable` (`ToolFetcher.Download.cs`) and in `StorePopulator` (`StorePopulator.cs`); only the resolved executable, not grammars/config — `tools/Hx.Runner.Core/Tools/` — [covers FR-001, FR-002, FR-003] <!-- doti-task-hash: 479ed81890504dd134ddc20f257958390e599a0f0638623ce212173719855948 -->

## Phase 2 — Agent-context refresh (WI-2)

- [x] T003 Refresh `.doti/core/templates/agent-context-template.md` to describe `doti-auto` (hands-off cycle driver, `--until`), the 014 structural-engine offender detail (ArchUnit violating types + rule description; Sentrux offender file/function/line/value with honest unknowns; rendered in the standalone commands + the gate ladder, on the trace envelope so the proof is byte-unchanged), Sentrux's production-only `.sentruxignore` scope (`test/` excluded), and the tool-fetch exec-bit guarantee — `.doti/core/templates/agent-context-template.md` — [covers FR-004] <!-- doti-task-hash: f17e26c564d6955c10e55747f79d048d3d3f63ea2fe5b5377f4e88ade7d63ce9 -->
- [x] T004 Refresh the entrypoint capability note (`rootMaturityNote` in `.doti/profiles/dotnet-cli/profile.json`) for the same capabilities, ADDING to the dense block (preserve the existing family ids the architecture-guidance test expects); do NOT hand-edit the rendered `CLAUDE.md`/`AGENTS.md` — `.doti/profiles/dotnet-cli/profile.json` — [covers FR-005] <!-- doti-task-hash: 51b31f66ba7c9b776e4e5493da9a99b41916cab2b3648e89f99f4ad252c1d36c -->
- [x] T005 Re-render with `doti render-skills` (updates `.doti/agent-context.md`, `CLAUDE.md`, `AGENTS.md`, materializes `.doti/templates`); rebuild Release so the build-bundled payload matches the edited sources — [covers FR-005] <!-- doti-task-hash: 06b2d4dc6eed2a69e107f3498caa8bee0277af5711aa78fca72eea78ece2feb0 -->

## Phase 3 — Verify

- [x] T006 `doti render-skills --check` + `doti payload check` clean; `gate run --profile normal` green over the change set; stamp implement on green. The Linux CI smoke pass is observed on the v0.12.2 tag push (not locally reproducible) — [covers SC-004, SC-005, SC-001] <!-- doti-task-hash: 27d54ee5cb4ded434b513f230e368d86f429aae796faa9923dbec31b7920778a -->

## Coverage

- FR-001 → T001, T002 | FR-002 → T002 | FR-003 → T001, T002 | FR-004 → T003 | FR-005 → T004, T005 | SC-001 → T002, T006 | SC-002 → T001 | SC-003 → T003, T004 | SC-004/005 → T005, T006.
