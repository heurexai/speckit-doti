# Architecture Review — Feature 010: Single-Source the Constitution

**Stage:** `/06-doti-arch-review`. **Date:** 2026-06-28.
**Artifacts:** [spec](../specs/010-single-source-constitution.md) · [plan](../plans/010-single-source-constitution-plan.md) · [tasks](../tasks/010-single-source-constitution-tasks.md).

## Triage
Change footprint: a **Doti-asset removal** (`.doti/core/memory/constitution.md`), a **build/config glob edit** (`Hx.Scaffold.Cli.csproj`), and one **test**. No production `*.Core` code, no CLI surface, no contract, no security surface, no dependency/layering change. Code lenses (edge-case, data-contract, security, blast-radius, modularity, fit-with-architecture) **exit not-applicable — no runtime code changes**. The applicable lenses are design-soundness + testability.

## Findings

### BLOCKER 0 · HIGH 0
- **design-soundness:** PASS. The change removes a verified-vestigial file (non-shipped post-009, read by no code, byte-identical to the kept copy). The csproj edit only removes a dead exclusion segment. Sound and minimal.
- **testability:** PASS. T001 pins the single-source invariant (twin absent + active present + no placeholders) before the deletion. `doti payload check` + `gate run` are the deterministic proofs (SC-002).

### LOW
- **L1 — absence assertion.** T001 asserts `.doti/core/memory/constitution.md` does not exist. Acceptable: it is the machine-checkable form of the single-source invariant; a future re-introduction of the twin would fail it.
- **L2 — empty `core/memory/` dir.** After removal the source `.doti/core/memory/` is empty; git does not track empty dirs, so it disappears. The parity checker iterates the dir (empty → no comparison) — confirmed safe in the plan's Risks.

## Rule/Boundary deltas
None. No `rules/architecture.json` family change, no `.sentrux/rules.toml` change (the constitution is Sentrux-excluded prose).

## Verdict
**0 BLOCKER / 0 HIGH.** A clean, minimal asset-hygiene change. Cleared for `/07-implement`.
