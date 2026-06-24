# Tasks: Local release output directory

## Tasks

- [x] T001 - Add the local release result contract - `tools/Hx.Tooling.Contracts/LocalReleaseResult.cs` - covers FR-013, FR-015, FR-016, FR-017, FR-027, FR-032, FR-037.
- [x] T002 - Add deterministic release-root resolution and validation - `src/Hx.Scaffold.Core/Release/LocalReleaseRootResolver.cs` - covers FR-001, FR-002, FR-003, FR-004, FR-005, FR-006, FR-029, FR-030, FR-031, FR-033, FR-034, FR-035, FR-036, FR-038.
- [x] T003 - Add command-backed local release packaging, checksum verification, identity metadata, version/latest publication, unsafe-path rejection, and opt-in environment persistence - `src/Hx.Scaffold.Core/Release/LocalReleaseService.cs` - covers FR-007 through FR-022 and FR-026 through FR-028.
- [x] T004 - Wire `hx release` and option diagnostics into the shared CLI envelope - `tools/Hx.Scaffold.Cli/ScaffoldCommandFactory.cs`, `tools/Hx.Scaffold.Cli/ScaffoldCommands.Release.cs` - covers FR-023, FR-024, FR-027, FR-029, FR-030, FR-038.
- [x] T005 - Add focused tests for release-root precedence, default environment lookup, named environment lookup, invalid names, and `--save-release-root` without `--release-root` - `test/Hx.Scaffold.Tests/LocalReleaseRootTests.cs` - covers SC-003, SC-013, SC-014, SC-015, SC-018.
- [x] T006 - Update source workflow guidance and generated agent assets for command-backed release behavior - `doti/core/templates/commands/doti-release.md`, `doti/core/templates/agent-context-template.md`, `doti/core/skills.json`, rendered `.agents`, `.claude`, `.doti/agent-context.md`, root entrypoints - covers FR-025.
- [x] T007 - Update README CLI reference and local release-store guidance - `README.md` - covers FR-023, FR-024, FR-025.
- [x] T008 - Run focused command proof: build, `Hx.Scaffold.Tests` release-root tests, `hx describe --json`, and local `hx release --release-root <temp> --json` / `--save-release-root` smokes - covers SC-001, SC-002, SC-005, SC-010, SC-012, SC-016, SC-017.
- [x] T009 - Run doti renderer drift check and normal/release gates before sanctioned commit - covers SC-006, SC-008, SC-009, SC-011.

## Dependencies

T001 blocks T003 and T004. T002 blocks T003 and T005. T003 and T004 block command proof. T006 and T007 block drift review and final release verification.

## Gate Notes

Manual file copying is not proof. Acceptance depends on `hx release` producing the machine-readable `LocalReleaseResult` and verified version/latest directories.
