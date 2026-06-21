# Plan: Deterministic vendored-tool fetch

> Spec: `docs/specs/tool-vendor-fetch.md`. Adds a `ToolFetcher` core + a `tools fetch` command + error codes, and wires fetch-if-missing into `new`.

## Technical Context

A new `ToolFetcher` in `Hx.Runner.Core` downloads a tool's asset per its pinned manifest, verifies SHA-256, and installs the executable; a thin `tools fetch` command in `Hx.Runner.Cli` drives it. Network I/O mirrors `GitleaksUpdateChecker` (`HttpClient`, short timeout, graceful failure). The three manifests share the asset shape `{rid, downloadUrl, archiveSha256?, executablePath, executableSha256, executableName}`, so a single minimal DTO parses all three (System.Text.Json ignores the extra `grammars`/`config` fields). **Constraint:** the download is injected (`Func<Uri, byte[]>`) so the verify/extract logic is unit-testable with fixtures and **no network in tests**.

## Constitution Check (gate)

PASS (before and after design):

- **Deterministic Ownership** — provisions strictly from pinned `downloadUrl` + SHA-256; fail-closed on mismatch; no unverified binary is ever written.
- **Bootstrap Honesty** — a real, command-backed capability; `describe` and the gate reflect it.
- **Template Boundary** — `new` gains fetch-if-missing for the generated repo; no template-content change beyond carrying the `gitversion` manifest.
- **Public Hygiene** — fetched binaries are gitignored; manifests are pinned; no secrets.
- **Cross-Platform** — only `win-x64` assets exist; other RIDs are reported (asset-unavailable), never crash. The fetcher logic is RID-agnostic.
- **Codified Cycle / Engineering Discipline** — through the cycle; premises verified against the manifests + existing download code.

No violation → Complexity Tracking empty.

## Research (resolve unknowns)

- **Archive formats.** Decision: handle raw-exe (sentrux: `archiveSha256` null, the download *is* the exe) and `.zip` (gitleaks + gitversion: `archiveSha256` set → extract `executableName`). Rationale: confirmed from the three manifests. Alternatives rejected: tar.gz handling (not used by any `win-x64` asset).
- **Sentrux grammar.** Decision: out of scope — the `grammars[]` entry has a SHA-256 but **no `downloadUrl`**, so it cannot be fetched; it remains a copy/provision step. Noted as a follow-up (add a grammar `downloadUrl` later). This feature fetches the three executables.
- **Testability.** Decision: inject the byte-fetch as a delegate so the deterministic verify/extract path is unit-tested without network.

## Design

1. `tools/Hx.Runner.Core/Tools/ToolFetcher.cs` (new): `FetchAll(repoRoot, rid, fetchBytes)` enumerates the tool manifests; `Fetch(manifestPath, rid, fetchBytes)` reads the asset for `rid`, downloads, verifies `archiveSha256` (when set) then extracts `executableName` from the zip (else uses the raw bytes), verifies `executableSha256`, and writes `executablePath`. Returns a per-tool result (Fetched / Skipped(no asset for RID) / Failed(code)).
2. `tools/Hx.Tooling.Contracts/ToolFetchResult.cs` (new): JSON proof (per-tool outcome + reason).
3. `tools/Hx.Runner.Cli`: a `tools` command group + `fetch` subcommand (`--repo`, `--rid` default host, `--tool all|gitleaks|sentrux|gitversion`, `--json`) → `RunnerCommands.ToolsFetch` → `CliResult`.
4. `errorcodes/registry.json`: append the four codes below; run `errorcodes render` (regenerates `ErrorCodes.g.cs`) and `errorcodes check`.
5. `src/Hx.Scaffold.Core`: add `tools/gitversion` to `ToolVendor.ToolDirectories` (so the manifest travels into generated repos), and have `ScaffoldNewRunner` call `ToolFetcher.FetchAll` (best-effort, fetch-if-missing) after vendoring so a generated repo provisions any missing binary incl. `gitversion`. Offline `new` still completes (the smoke already tolerates a blocked tool step).
6. Tests in `test/Hx.Runner.Tests`: fixture-based verify/extract (matching hash installs; archive/exe mismatch fails closed; unknown RID → asset-unavailable) with an injected byte source — no network.
7. Docs: update `README.md` (CLI-reference table + the **Status** note now that tools self-provision) and `doti/core/templates/agent-context-template.md` (add `tools fetch` to *Current Command Availability*); re-render with `doti render-skills` so `.doti/agent-context.md` matches.

**Architecture delta:** none. New types live in existing namespaces (`Hx.Runner.Core`, `Hx.Tooling.Contracts`, `Hx.Runner.Cli`, `Hx.Scaffold.Core`); `Hx.Runner.Core` already does network I/O (`GitleaksUpdateChecker`), so `rules/architecture.json` + `.sentrux/rules.toml` are unchanged.

## CLI surface & error contract

- **Command:** `tools fetch --repo . [--rid <rid>] [--tool all|gitleaks|sentrux|gitversion] [--json]` — returns the `CliResult` envelope; appears in `describe`.
- **Error codes** (append-only in `errorcodes/registry.json`, then `errorcodes render` + `errorcodes check`):
  - `validation` / `tool-asset-unavailable` — no manifest asset for the host RID.
  - `integrity` / `tool-archive-hash-mismatch` — downloaded archive SHA-256 ≠ manifest.
  - `integrity` / `tool-executable-hash-mismatch` — executable SHA-256 ≠ manifest.
  - `internal` / `tool-download-failed` — download or extraction failed (network/IO).
- **Exit class:** Integrity on a hash mismatch; Validation on asset-unavailable / overall failure; Success when all requested tools are present + verified.
- **Envelope:** `CliResult`, JSON-first; thin command body (builds the result, `CliHost` renders + sets the exit code).

## Command Availability

| Area | Command | Status |
| --- | --- | --- |
| Tool fetch | `tools fetch --repo . [--rid] [--tool] --json` | NEW (this feature) |
| Error codes | `errorcodes render` + `errorcodes check` | implemented (exercised here to register the new codes) |
| Build / Test | `dotnet build/test scaffold-dotnet.slnx -c Release` | implemented (the PR's CI gate) |

## Risks

- Network unavailable at fetch time → the command reports `tool-download-failed` (fail-closed) and `new` degrades to today's behavior (blocked tool step); not a regression.
- The sentrux grammar still needs separate provisioning (no `downloadUrl`) → noted; sentrux `check` (not `verify`) may still require it.
- Adding error codes is append-only — `errorcodes check` freezes them; reordering/removing a shipped code would fail the gate (so only append).
