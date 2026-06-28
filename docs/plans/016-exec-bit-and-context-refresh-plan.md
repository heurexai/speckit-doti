# Plan — 016 Tool-fetch executable bit + agent-context refresh

**Spec:** [docs/specs/016-exec-bit-and-context-refresh.md](../specs/016-exec-bit-and-context-refresh.md). **Stage:** `/03-doti-plan`.

## Summary

Two fixes for v0.12.2: (1) a tiny `*.Core` correctness fix — fetched/stored tool binaries get the Unix execute bit; (2) a Doti-prose refresh — the agent-context template + the entrypoint capability note describe the current doti (doti-auto, structural offenders, Sentrux production scope, the exec-bit guarantee), then re-render.

## Existing-architecture assessment (verified)

- **Exec-bit gap:** `ToolFetcher.WriteExecutable` ([ToolFetcher.Download.cs:156](../../tools/Hx.Runner.Core/Tools/ToolFetcher.Download.cs)) and `StorePopulator` ([StorePopulator.cs:56](../../tools/Hx.Runner.Core/Tools/StorePopulator.cs)) both `File.WriteAllBytes` → `File.Move` the verified binary into place but never set the Unix exec mode; on Linux/macOS the file lands `0644` → "Permission denied" when run (the v0.12.1 CI failure). No existing `SetUnixFileMode` anywhere in `tools/Hx.Runner.Core/Tools`.
- **Context sources:** `.doti/agent-context.md` is rendered from `.doti/core/templates/agent-context-template.md` (materialized + rendered by `DotiRenderer`); `CLAUDE.md`/`AGENTS.md` are thin entrypoints whose capability paragraph is the **`rootMaturityNote`** read from `.doti/profiles/dotnet-cli/profile.json` ([DotiRenderer.cs:17,32](../../tools/Hx.Doti.Core/DotiRenderer.cs); `RootEntrypointRenderer`). Both are **prose sources** — the template is materialized from `.doti/core`, the profile is a static payload asset; neither is a hand-edited *rendered* file. Both sources are stale at 012 (no doti-auto/offender/structural).
- **Parity:** after editing the sources + re-rendering, the repo's `.doti` changes; `doti payload check` compares the repo payload against the build-bundled payload, so the Release CLI is rebuilt so the bundled payload matches (then `render-skills --check` + `payload check` are clean).

## Design

**WI-1 (Decision):** add one shared helper `ExecutableFileMode.EnsureExecutable(string path)` in `tools/Hx.Runner.Core/Tools/` that, on non-Windows (`!OperatingSystem.IsWindows()`), calls `File.SetUnixFileMode(path, 0755)` (owner rwx, group/other r-x); a no-op on Windows. Call it immediately after the `File.Move` in `ToolFetcher.WriteExecutable` and in `StorePopulator`. *Rationale:* single-responsibility, one source of the mode, both write points covered, Windows-guarded so `SetUnixFileMode` never throws; applies only to the resolved executable (grammars/config are written elsewhere and stay non-exec). No fetch/verify-logic or hash change — the bytes (already hash-verified) are untouched; only the mode is set.

**WI-2 (Decision):** edit the two prose sources to add: `doti-auto` (the hands-off cycle driver, `--until`), the 014 structural-engine offender detail (ArchUnit violating types + rule description; Sentrux offender file/function/line/value, honest unknowns), Sentrux's **production-only** scope (`.sentruxignore` excludes `test/`), and the tool-fetch exec-bit guarantee. Then `doti render-skills` to update `.doti/agent-context.md` + `CLAUDE.md`/`AGENTS.md`, and rebuild Release so the bundled payload matches. *Rationale:* edit the single source (template + profile), never the rendered output (FR-005); keep the additions accurate + concise (the maturity note is a dense single-sourced block — preserve the existing family ids the architecture-guidance checks expect, only adding).

**Alternatives rejected:**
- *Set the exec bit at resolve/run time.* REJECTED — hacky + repeated; setting it once at write is correct + covers every consumer.
- *Hand-edit `CLAUDE.md`/`AGENTS.md`/`.doti/agent-context.md` directly.* REJECTED — they are rendered; `render-skills --check` would flag the drift. Edit the template + profile sources.
- *Stamp the source repo with `scaffold-version.json`.* REJECTED (spec Excluded) — the source repo renders rather than installs; a managed-consumer stamp would misclassify normal source edits as managed drift.

## Architecture delta (enforced)

- WI-1 is a modular `*.Core` helper within `Hx.Runner.Core/Tools` (layer-clean, within the function-size limit); no contract/proof/rule change. WI-2 is prose + re-render. The parity authorities (`render-skills --check`, `payload check`) + the architecture-guidance test (family-id presence in the maturity note) gate the change.

## Constitution Check

- §1 (inherited invariants): **PASS** — no gate weakened; the exec-bit fix makes a hash-verified binary runnable (a correctness fix), nothing downgraded. §2: **PASS**.

## Risk

- **Low.** WI-1 is a guarded one-liner at two sites (testable: assert the mode is set on non-Windows, no-op on Windows). WI-2's residual risk is dropping a family id the architecture-guidance test expects from the maturity note — mitigated by only ADDING to the dense block. The Linux CI smoke pass is observed on v0.12.2 (the one not-locally-reproducible item, same as 015).

## Next

`/04-doti-tasks`.
