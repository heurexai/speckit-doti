# 016 — Tool-fetch executable bit + doti agent-context refresh

## Goal

A patch (v0.12.2) that makes this repo's doti current in two ways an operator surfaced:

1. **Fix the cross-platform tool-fetch executable bit (the bug).** `hx tools fetch` (and the shared-store populate) download + hash-verify the per-RID tool binaries, but write them **without the Unix executable bit** (`0644`). On Linux/macOS the verified binary then cannot run: the v0.12.1 release CI smoke fetched all three tools successfully yet failed `sentrux-verify` with `"Permission denied"`, and the `hygiene` step failed the same way on Gitleaks. A downloaded executable must be `chmod +x`. (Win-x64 was never affected; this never surfaced until the release CI first ran on Linux.)
2. **Refresh the stale doti agent context.** The repo's agent-facing context — `.doti/agent-context.md` (+ its template) and the `CLAUDE.md`/`AGENTS.md` entrypoints — was last refreshed at cycle **012**. It does not mention `doti-auto` (013), the **structural-engine offender detail** (014), Sentrux's **production-only** scope (014's `.sentruxignore` `test/` exclusion), or this tool-fetch fix. `doti render-skills --check` passes only because the rendered output matches the *template*; the template itself drifted behind the code, so an agent reading "Read `.doti/agent-context.md` first" sees a doti three cycles stale.

## User Scenarios & Testing

**Priority Mode** — workflow / tooling fix: fail-closed safety + deterministic proof before ergonomics. The exec-bit fix is a correctness defect; the context refresh is truth-first documentation accuracy.

### Work Item 1 — Fetched tool binaries are executable on Unix (Priority: P1)

A tool binary fetched/stored by the workflow is runnable on the host OS.

- **Why this priority:** the release CI publish gate (and any Linux/macOS `hx new` / gate run) depends on the fetched Sentrux/Gitleaks binaries being executable; without the bit they fail-closed with "Permission denied" even though the bytes are correct + hash-verified.
- **Independent Test:** on a non-Windows host, fetch a tool binary (`hx tools fetch`) and run it (`<tool> --version`); it executes (no permission error). The store-populated copy is likewise executable.
- **Acceptance Scenarios:**
  1. **Given** a freshly fetched tool binary on Linux/macOS, **When** the workflow runs it, **Then** it executes — the file carries the owner/group/other execute bits.
  2. **Given** a Windows host, **When** a binary is fetched, **Then** the Unix-mode call is a no-op (never throws) and behavior is unchanged.
  3. **Given** the store-populate copies a verified binary into the shared store, **Then** the stored copy is executable too (the path the generated repo resolves + runs).

### Work Item 2 — The doti agent context reflects the latest capabilities (Priority: P1)

The repo's agent-facing context describes the current doti, not a three-cycle-old snapshot.

- **Why this priority:** `CLAUDE.md` directs an agent to read `.doti/agent-context.md` first; if that omits `doti-auto`, the structural-offender detail, and Sentrux's production scope, the agent cannot use or reason about the latest doti — the repo effectively "runs" an older doti from the agent's view.
- **Independent Test:** read `.doti/agent-context.md` + `CLAUDE.md`/`AGENTS.md` and confirm they describe `doti-auto`, the ArchUnit/Sentrux offender detail (014), Sentrux's production-only `.sentruxignore` scope, and the tool-fetch exec-bit guarantee; `doti render-skills --check` + `doti payload check` stay clean after re-render.
- **Acceptance Scenarios:**
  1. **Given** the refreshed context, **When** an agent reads it, **Then** `doti-auto`, the structural-offender detail, and Sentrux's production scope are present and accurate.
  2. **Given** the source change is to the **template/source** (not the rendered output), **When** `doti render-skills` runs, **Then** the rendered `.doti/agent-context.md` + entrypoints update and `--check` is clean (no hand-edited rendered asset).

### Edge Cases

- `File.SetUnixFileMode` throws on Windows — the helper MUST guard with `OperatingSystem.IsWindows()` and no-op there.
- A grammar/config artifact (no execute needed) MUST NOT be marked executable — only the resolved tool **executable** gets the bit.
- The context refresh is template/source-only; it MUST NOT hand-edit a rendered asset (which `render-skills --check` would flag).

## Scope

Included:
- Set the Unix executable bit on a fetched tool binary (`ToolFetcher` write path) and on the shared-store copy (`StorePopulator`), via one shared helper; guarded for Windows.
- Refresh `.doti/core/templates/agent-context-template.md` + the `CLAUDE.md`/`AGENTS.md` entrypoint source to cover `doti-auto`, the structural-offender detail, Sentrux's production scope, and the exec-bit guarantee; re-render.

Excluded:
- No change to the fetch/verify logic, the manifests, the hash pinning, or any gate/rule/proof.
- No new behavior for `hx new` provisioning beyond making the existing fetched binary executable.
- No `scaffold-version.json` install-stamp for the source repo (it renders rather than installs; stamping it as a managed consumer is out of scope and undesired).

## Functional Requirements

- `FR-001`: A tool executable written by the fetch path (`ToolFetcher`) MUST be marked executable (owner/group/other execute) on non-Windows hosts after it is moved into place. `[WI1]`
- `FR-002`: A tool executable copied into the shared store (`StorePopulator`) MUST likewise be marked executable on non-Windows hosts. `[WI1]`
- `FR-003`: The exec-bit operation MUST be guarded for Windows (no `SetUnixFileMode` call there) and MUST apply only to the tool executable, never to grammars/config. `[WI1]`
- `FR-004`: `.doti/agent-context.md` (via its template) MUST describe `doti-auto`, the 014 structural-engine offender detail (ArchUnit violating types + Sentrux offender file/function/line), Sentrux's production-only `.sentruxignore` scope, and the tool-fetch exec-bit guarantee. `[WI2]`
- `FR-005`: The `CLAUDE.md`/`AGENTS.md` entrypoint capability text MUST be refreshed from its source for the same capabilities, and the change MUST be made in the template/source then rendered (no hand-edited rendered asset); `doti render-skills --check` + `doti payload check` stay clean. `[WI2]`

## Success Criteria

- `SC-001`: On Linux/macOS, a fetched + store-populated tool binary runs (no "Permission denied"); the v0.12.2 release CI smoke passes `sentrux-verify` + `hygiene` and the publish proceeds.
- `SC-002`: On Windows, fetch behavior is unchanged (the Unix-mode call no-ops).
- `SC-003`: `.doti/agent-context.md` + `CLAUDE.md`/`AGENTS.md` describe `doti-auto`, the structural-offender detail, and Sentrux's production scope; an agent reading them sees the current doti.
- `SC-004`: `doti render-skills --check` + `doti payload check` clean after the refresh; the change is template/source-only.
- `SC-005`: No gate, rule, limit, manifest, hash, or proof changes (visibility/correctness-only).

## Key Entities

- **Tool executable bit** — the Unix execute mode a fetched/stored tool binary must carry to be runnable (the `+x` the fetch path omitted).
- **Doti agent context** — `.doti/agent-context.md` (+ template) and the `CLAUDE.md`/`AGENTS.md` entrypoints an agent reads to know the current doti.

## Deterministic Surfaces

- `tools/Hx.Runner.Core/Tools/ToolFetcher.Download.cs` (the fetch write), `tools/Hx.Runner.Core/Tools/StorePopulator.cs` (the store write) — gain the exec-bit set.
- `.doti/core/templates/agent-context-template.md` + the entrypoint source — refreshed; re-rendered to `.doti/agent-context.md`, `CLAUDE.md`, `AGENTS.md`.
- `doti render-skills`/`--check`, `doti payload check` — the parity proofs.

## Architecture Impact

- WI-1: a tiny, modular `*.Core` change — one shared `EnsureExecutable` helper called at the two write points; thin, testable, Windows-guarded. No contract, no fetch/verify-logic change.
- WI-2: Doti-prose (template/source) + re-render. No code.
- No gate/rule/Sentrux/proof surface changes.

## Sentrux And Hygiene Impact

- No Sentrux baseline/policy/limit change. The new helper stays within the function-size limit + layer boundaries (`Hx.Runner.Core/Tools`). The agent-context refresh is prose (Sentrux source-excluded).

## Assumptions

- The "Permission denied" failure is the missing exec bit (verified from the v0.12.1 CI log: all three tools fetched + hash-verified, then `sentrux-verify` failed running the binary). Confirmed on the v0.12.2 CI run.
- `File.SetUnixFileMode` is the cross-platform .NET API (no-op-able via the Windows guard); it sets the mode without re-reading the bytes (no re-hash needed).

## Acceptance

- Command-backed today: `hx tools fetch`, `doti render-skills --check`, `doti payload check`, `gate run`.
- Planned by this feature: the exec-bit set + the refreshed context; the Linux CI smoke pass is observed on the v0.12.2 tag.
