# Plan — 015 Release accuracy

**Spec:** [docs/specs/015-release-accuracy.md](../specs/015-release-accuracy.md). **Stage:** `/03-doti-plan`.

## Summary

Two release-accuracy fixes for v0.12.1, both docs/CI-config only (no `*.Core` code, no contract, no proof/rule change):
1. **README** gains an *Unnumbered utility skills* subsection documenting all seven by-name skills, with the **constitution** given a fuller treatment.
2. **`release.yml`** fetches the host-RID vendored structural tools from their pinned fork releases before the `hx new` smoke, so the publish gate passes cross-platform.

## Existing-architecture assessment (verified)

- **README** ([README.md](../../README.md)) — the *Doti workflow* section has the numbered-stage table (lines ~130–140) followed by two paragraphs: `/doti-constitution` (one line) and `/doti-auto`. The other five utility skills (`doti-bug`, `doti-amend`, `doti-drift-fix`, `doti-upgrade`, `converge`) are absent. The README is a **human overview**; the skills are single-sourced in `.doti/core/skills.json` and rendered — the README names + summarizes them, it is not a second source of truth.
- **Skills** ([.doti/core/skills.json](../../.doti/core/skills.json)) — the seven unnumbered entries carry the canonical descriptions the README summarizes (constitution's §1/§2 layers; bug's assess→fix→test; amend's reconcile; drift-fix's fix-code-not-spec; converge's coverage-gap; upgrade's two update planes).
- **Release CI** ([.github/workflows/release.yml](../../.github/workflows/release.yml)) — `pack-and-smoke` (ubuntu-latest) packs the global tool then runs `hx new` whose first-smoke verifies the vendored tools; it has **no `hx tools fetch` step**, and the binaries are gitignored/never bundled → fail-closed on linux. `ci.yml` is green only because it skips the gate/smoke entirely.
- **Tool provisioning** — `hx tools fetch --tool <all|sentrux|...> --rid <host>` reads `tools/*/*.version.json` (which declare win-x64 + linux-x64 + osx-arm64 assets from the `heurexai/*` fork releases, hash-pinned) and writes the host-RID binary into `<repo>/tools/<tool>/bin/<rid>/`, hash-verified, fail-closed. `hx new`'s `sourceRepoRoot` (the installed payload) is what `ToolVendor`/`StoreProvisioner` read; the generated repo's first-smoke resolves via `SentruxToolPathResolver` → the shared tool store or in-repo. **Verified locally:** the linux Sentrux fetch from the fork succeeds + hash-verifies.

## Design

**WI-1 — README (Decision):** replace the two stand-alone paragraphs after the numbered-stage table with one *Unnumbered utility skills* subsection: a lead-in (these run outside/anytime in the cycle, never reorder `/01`–`/09`, single-sourced), then a fuller **constitution** explanation (its own short block — the §1 inherited/codified vs §2 operator-authored split; `plan`/`arch-review` re-read §2 fresh via `hx doti constitution`; authored/amended with `/doti-constitution`, tracked by the cycle + git history), then a compact table of the remaining six (`doti-auto`, `doti-bug`, `doti-amend`, `doti-drift-fix`, `converge`, `doti-upgrade`) — name + one-line purpose each. *Rationale:* a table keeps it a scannable overview (FR-003) without duplicating the rendered skill bodies; the constitution gets prose because it is the first-class operator artifact the operator called out.

**WI-2 — release.yml (Decision):** in `pack-and-smoke`, after `dotnet tool install` + `export PATH`, add a step that derives the installed payload root from `hx version --json` (the `prerequisites.manifestPath` minus `/.doti/core/prerequisites.json`) and runs `hx tools fetch --repo "$payload" --tool all --json` (host RID = the runner's). `hx new` then vendors + store-populates from that payload and the first-smoke verifies. *Rationale:* fetch into the exact `sourceRepoRoot` the installed `hx new` reads, so the existing store-populate path resolves the binaries; `--tool all` covers Sentrux + Gitleaks + GitVersion (all declare the host RID); hash-verified + fail-closed preserves the pinning.

**Alternatives rejected:**
- *Duplicate the full rendered skill text into the README.* REJECTED — breaks single-source (FR-003); the README is an overview that links by name.
- *Run the release smoke on windows-latest instead of fetching.* REJECTED — it would mask the real gap (the tools are simply unfetched) and leaves Linux/macOS publishers broken; fetching from the fork is the correct, platform-agnostic fix the operator asked for.
- *Make `hx new` self-provision the tools (product change).* DEFERRED — more robust + fixes end-users, but it is a scaffold-behavior change deserving its own cycle; out of scope here (noted as a follow-up).

## Architecture delta (enforced)

- No ArchUnit/Sentrux/contract/proof surface changes. The only deterministic surface touched is `release.yml` (CI config); `README.md` is a human overview. The CI fetch uses the existing hash-pinned `hx tools fetch` (fail-closed) — it strengthens the published artifact's gate, never weakens it.

## Constitution Check

- §1 (inherited invariants): **PASS** — no gate weakened, nothing downgraded; the CI change makes the existing gate actually run on the runner platform. §2: **PASS** — docs/CI only.

## Risk

- **Low overall.** WI-1 is prose. WI-2's residual risk is the CI store-populate timing (can't be fully exercised locally) — mitigated by deriving the exact payload root + `--tool all`; observed on the v0.12.1 tag push, with one CI iteration possible.

## Next

`/04-doti-tasks` — README-doc task + release.yml provisioning task + verification.
