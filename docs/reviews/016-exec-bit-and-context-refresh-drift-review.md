# Drift Review — Feature 016: Tool-fetch executable bit + agent-context refresh

**Stage:** `/08-doti-drift-review`. **Date:** 2026-06-28. Change set: the exec-bit `*.Core` fix (`ExecutableFileMode.cs` + the two call sites + a test) and the agent-context refresh (template + profile source, re-rendered).

## Axis 1 — spec ↔ code (PASS)

- **FR-001/FR-002/FR-003** (exec bit, both write points, Windows-guarded, executable-only): new `ExecutableFileMode.EnsureExecutable` ([ExecutableFileMode.cs](../../tools/Hx.Runner.Core/Tools/ExecutableFileMode.cs)) sets `0755` on non-Windows (no-op on Windows via `OperatingSystem.IsWindows()`); called immediately after the `File.Move` in `ToolFetcher.WriteExecutable` and `StorePopulator` — the only two executable-write points. The bytes (already hash-verified) are untouched — only the file mode changes, so no re-hash and no fetch/verify-logic change. `ExecutableFileModeTests` passes (asserts the bits on non-Windows; the Windows no-op never throws).
- **FR-004** (agent-context describes the latest): `.doti/core/templates/agent-context-template.md` now covers `doti-auto` (+ the other unnumbered utility skills, replacing the stale "legacy unnumbered names" line), the 014 ArchUnit violating-type / Sentrux offender detail (in the architecture-test + sentrux + `--stream` trace descriptions, render-only on the envelope so the proof is byte-unchanged), Sentrux's production-only `.sentruxignore` `test/` scope, and the tool-fetch exec-bit.
- **FR-005** (entrypoint note refreshed from source, re-rendered, no hand-edit): the `selfHostingStatus.rootMaturityNote` in `.doti/profiles/dotnet-cli/profile.json` (the CLAUDE.md/AGENTS.md source) gained the same capabilities; `doti render-skills` re-rendered `.doti/agent-context.md` + `CLAUDE.md` + `AGENTS.md` from source (all three now name `doti-auto`, the offenders, and the production scope) — no rendered file was hand-edited.

Matches the plan: a guarded, hash-neutral two-site exec-bit fix + a source-only prose refresh; the rejected alternatives (resolve-time chmod; hand-edit the rendered files; stamp the source repo) were not taken.

## Axis 2 — code ↔ docs (PASS)

- The `+x` code change is self-described (the two call-site comments + the new exec-bit sentence in the agent-context tool-fetch description). The context refresh *is* the doc update for 013/014/015's already-shipped features (this cycle closes the accumulated agent-context drift). No symbol was removed/renamed.

## Axis 3 — source ↔ installed (PASS)

- `doti render-skills --check` — **no drift** (rendered entrypoints/agent-context match the edited source). `doti payload check` — **93 managed files match** after the Release rebuild (the build-bundled payload now equals the edited source). The change is template/profile-source only; no hand-edited rendered/installed asset.

## Gate

`gate run --profile normal` green over the change set; build 0/0; regressions green. No gate, rule, limit, manifest, hash, or proof changed (the exec-bit sets only the file mode; the context refresh is prose).

## Note — the one CI-observed item

WI-1's effect on Linux (the fetched/stored binary now runs) is observable on the v0.12.2 tag: the release CI smoke should pass `sentrux-verify` + `hygiene` (both failed on v0.12.1 with "Permission denied") and the NuGet publish proceeds. The fix is locally proven on Windows (the no-op) + by the Unix-mode test; the Linux end-to-end is the confirm-at-CI item.

## Verdict

**No open drift** in any applicable axis. The exec-bit fix is guarded, hash-neutral, and two-site; the agent-context is refreshed from source and re-renders cleanly. Ready for `/09-doti-release` (v0.12.2).
