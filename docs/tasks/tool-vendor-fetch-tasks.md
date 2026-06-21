# Tasks: Deterministic vendored-tool fetch

> Plan: `docs/plans/tool-vendor-fetch-plan.md`.

- `T001` (FR-006) — Append four codes to `errorcodes/registry.json` (`validation/tool-asset-unavailable`, `integrity/tool-archive-hash-mismatch`, `integrity/tool-executable-hash-mismatch`, `internal/tool-download-failed`); run `errorcodes render` to regenerate `tools/Hx.Cli.Kernel/ErrorCodes.g.cs`.
- `T002` (FR-001, FR-002) — Add `ToolFetcher` (`tools/Hx.Runner.Core/Tools/ToolFetcher.cs`) + a minimal manifest DTO: `Fetch(manifestPath, rid, fetchBytes)` downloads, verifies `archiveSha256` (when set) → unzips `executableName`, else uses raw bytes; verifies `executableSha256`; writes `executablePath`. Fail-closed (the four codes). `FetchAll` enumerates the tool manifests. Add `ToolFetchResult` to `Hx.Tooling.Contracts`.
- `T003` (FR-005, FR-007) — `RunnerCommands.ToolsFetch` + a `tools fetch` command in `Hx.Runner.Cli/Program.cs` (`--repo`, `--rid` default host, `--tool`, `--json`); host download uses `HttpClient` (mirror `GitleaksUpdateChecker`); returns `CliResult`. Unknown RID → asset-unavailable (clean, no throw).
- `T004` (FR-003, FR-004) — Add `tools/gitversion` to `ToolVendor.ToolDirectories`; have `ScaffoldNewRunner` call `ToolFetcher.FetchAll` (best-effort, fetch-if-missing) after vendoring so generated repos provision missing binaries incl. `gitversion`. Offline `new` must still complete.
- `T005` (SC-003, SC-004) — `test/Hx.Runner.Tests`: fixture-based verify/extract with an injected byte source (match installs; archive-hash mismatch fails; exe-hash mismatch fails; unknown RID → asset-unavailable). No network.
- `T006` (SC-004) — `dotnet build` + `dotnet test` green; `errorcodes check` passes (codes frozen).
- `T007` (SC-001, SC-002) — Verify behavior: `tools fetch` on a binary-less win-x64 checkout installs all three verified executables; `new` yields a working `gitversion`. (Manual/network verification — recorded, not a CI gate.)
- `T008` (docs) — Update `README.md` (CLI-reference table: add `tools fetch`; refresh the **Status** note now that vendored tools self-provision) and `doti/core/templates/agent-context-template.md` (add `tools fetch` to *Current Command Availability* + note `gitversion` is now provisioned), then `doti render-skills` so `.doti/agent-context.md` matches (no drift).

## Coverage

| Requirement | Task(s) |
| --- | --- |
| FR-001, FR-002 | T002 |
| FR-003, FR-004 | T004 |
| FR-005, FR-007 | T003 |
| FR-006 | T001 |
| SC-001, SC-002 | T007 |
| SC-003 | T002, T005 |
| SC-004 | T005, T006 |
| docs (README + agent-context) | T008 |
