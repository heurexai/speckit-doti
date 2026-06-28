# Arch Review — 016 Tool-fetch executable bit + agent-context refresh

**Stage:** `/06-doti-arch-review`. **Change under review:** [spec](../specs/016-exec-bit-and-context-refresh.md) / [plan](../plans/016-exec-bit-and-context-refresh-plan.md) / [tasks](../tasks/016-exec-bit-and-context-refresh-tasks.md). Changed files: `tools/Hx.Runner.Core/Tools/{ExecutableFileMode.cs (new), ToolFetcher.Download.cs, StorePopulator.cs}`, a test, `.doti/core/templates/agent-context-template.md`, `.doti/profiles/dotnet-cli/profile.json`, + the re-rendered `.doti/agent-context.md`/`CLAUDE.md`/`AGENTS.md`.

## Triage

A small **production `*.Core`** change (the exec-bit helper) + **Doti-prose** (the context refresh). Applicable code lenses: correctness/edge-case, security, modularity, testability, fit-with-architecture. Not applicable: data-contract (no contract change), blast-radius-of-templates (no generated-code template), generated-code lens. The prose half gets the docs-accuracy lens.

## Lens findings

### Correctness / edge-case (WI-1) — no blocker

- **F1 (HIGH → mitigated):** the bug is exactly the missing exec bit; the fix sets it at BOTH write points (fetch + store) — the store path is the one the v0.12.1 CI actually ran. Covered by T002. Evidence: the v0.12.1 CI log (fetched + hash-verified, then "Permission denied").
- **F2 (MEDIUM → mitigated):** `File.SetUnixFileMode` throws on Windows → the helper guards with `OperatingSystem.IsWindows()` and no-ops (T001 asserts the Windows no-op + the non-Windows mode). FR-003.
- **F3 (LOW):** only the resolved **executable** must get `+x`, not grammars/config — those are written by different paths and are out of `EnsureExecutable`'s call sites (called only at the two executable-write `File.Move`s). FR-003.

### Security (WI-1) — no blocker

- **F4 (LOW):** setting `0755` on an already-hash-verified, manifest-pinned binary does not widen trust — the bytes are unchanged (no re-fetch, no re-hash); the mode only makes the verified binary runnable. No new attack surface; the fetch/verify/pinning path is untouched.

### Modularity / testability (WI-1) — no blocker

- **F5 (PASS):** one shared single-responsibility helper, two call sites, separately unit-testable (T001). Stays in `Hx.Runner.Core/Tools` (layer-clean), within the function-size limit.

### Docs-accuracy / single-source (WI-2) — no blocker

- **F6 (MEDIUM → mitigated):** the refresh must edit the **sources** (`.doti/core/templates/agent-context-template.md`, `.doti/profiles/dotnet-cli/profile.json` `rootMaturityNote`) and re-render — never hand-edit `CLAUDE.md`/`AGENTS.md`/`.doti/agent-context.md` (T004/T005); `render-skills --check` + `payload check` confirm. FR-005.
- **F7 (MEDIUM → mitigated):** the dense `rootMaturityNote` feeds the architecture-guidance check (it asserts the `cliSurfaceConfinement`/`cliDelegation` family ids appear) — the refresh must only ADD capability text, never drop those ids (the 009 README-class miss). T004 explicitly preserves them; the gate's architecture step catches a drop.

## Verdict

**No open BLOCKER in any applicable lens.** WI-1 is a guarded, hash-neutral, two-site exec-bit fix with a Windows-no-op test; WI-2 is a source-only prose refresh that re-renders cleanly and preserves the family ids the architecture guidance expects. Cleared for `/07-doti-implement`.
