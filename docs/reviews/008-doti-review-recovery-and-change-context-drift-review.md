# Drift Review — 008 Doti Review Recovery and Change Context

**Stage:** `/08-doti-drift-review` · **Feature:** `008-doti-review-recovery-and-change-context`
**Cycle base:** `6db075f` (the `analyze:` commit; the whole 41-task implementation + the arch-review record are the working-tree diff).
**Gate:** `gate run --profile normal` → **Pass** (14/14 steps; full gate — broad inputs `.gitignore`/`.sentruxignore`/`Directory.Packages.props` forced the full suite, 11 test projects).

## Scope & triage

107 changed paths (37 added, 19 deleted, 51 modified). The change touches production code (`Hx.Cycle.Core`, `Hx.Impact.Core`, `Hx.Sentrux.Core`, `Hx.Gate.Core`), contracts (`Hx.Tooling.Contracts`), the new ML stack (`Hx.Embedding.Core`, `Hx.Semantic.Core`), CLI (`Hx.Runner.Cli`, `Hx.Semantic.Cli`, `Hx.Scaffold.Cli`), Doti prose (templates/skills), and config (`errorcodes`, `workflow.yml`, `.sentruxignore`, `Directory.Packages.props`). **All three axes applicable** (Axis 1 spec↔code runs in full — this is a code change, not docs-only). The 19 deletions are all `.doti/templates/**` committed twins removed for single-source materialization (FR-016) — expected, not symbol drift.

Two clean-context sub-agents ran Axis 1 and Axis 2 in parallel; Axis 3 ran inline. Every finding below was independently re-verified against the code before action.

## Axis 1 — spec ↔ code

`hx doti converge` → **all 64 spec requirements covered by a task** (0 uncovered). BLOCKERs BL-1/2/3/5 and the additive-contract / CLI-thinness / docs-only-scope / FR-040–042 / FR-020 invariants were verified faithfully implemented. Two genuine enforcement gaps found and **fixed in-cycle (code, never the spec)**:

- **[HIGH · FR-031 / SC-015 / M-2 / T026] Rebaseline gate accepted a STALE arch-review record.** `SentruxRebaselinePolicy.ArchReviewFresh` decided freshness via stage-freshness only. `arch-review` is a `review` stage with no `diff` prerequisite, so `RequiresChangeSetIdentity("arch-review")` is `false` and `FreshnessEvaluator` never compares its `ChangeSetId` — leaving the laundering window the design closes: stamp arch-review → edit code (the regression) → arch-review still reports `Fresh` → rebaseline authorized. **Fix:** `ArchReviewFresh` now also requires the arch-review proof's stamped `ChangeSetId == ChangeSetIdentity.Of(repo, baseRef, "HEAD")` (the current diff), via a new pure `SentruxRebaselinePolicy.ArchReviewChangeSetFresh(state, currentId)`. Tests added in `test/Hx.Cycle.Tests/SentruxBandCycleTests.cs` (stale identity → refused; missing record → fail-closed). `tools/Hx.Cycle.Core/SentruxRebaselinePolicy.cs`.
- **[LOW · FR-039 / BL-4 / T037] BGE-M3 external weight sidecar `model.onnx.data` was not hash-verified before load.** `ModelLocator` verified `model.onnx` + tokenizer but not the external `*.onnx.data` ONNX Runtime loads at session creation — a poisoned sidecar bypassed the graph hash. **Fix:** `ModelLocator.BgeM3Model` now hash-verifies a present `model.onnx.data` fail-closed (a present-but-unpinned sidecar throws). Tests in `test/Hx.Embedding.Tests/ModelLocatorSidecarTests.cs`. `tools/Hx.Embedding.Core/ModelLocator.cs`.

**Not drift (spec satisfied):** FR-029/SC-016 source scoping is enforced by `.sentruxignore` (the native tool reads it; SC-016 verified — prose not scanned) and the docs-only `GateScope` skip. The `SentruxSourceScope` / `SentruxPolicy.ConfiguredAsCode` code-scope filter is unwired (no production caller) — redundant scaffolding, not an enforcement gap, since `.sentruxignore` is the single scope authority. Logged for cleanup (a quality smell, not an axis inconsistency) rather than churned at the final gate.

## Axis 2 — code ↔ docs

This feature's new operator surfaces (`hx doti cycle refresh`/`refresh-plan`, `review-context`, `drift-candidates`, the `llmModelRoot` config field) were undocumented in the agent-facing command reference, and `hx impact plan --for change-context` was unmentioned — a code change with no doc change. **Fixed at the source + re-rendered:**

- `.doti/core/templates/agent-context-template.md` — added entries for `cycle refresh-plan`/`refresh --apply-safe` (review recovery), `review-context`, the advisory offline `drift-candidates` finder (with the `llmModelRoot` ⇒ `HEUREX_LLM_ROOT` precedence), and the `--for change-context` audience on `impact plan`.
- `.doti/profiles/dotnet-cli/profile.json` (`commandAvailabilityFootnote`) — the cycle-substrate summary now names recovery + change/review context, so every rendered skill stays truthful.

Verified clean: the 5 new error codes (VAL0028–VAL0032) are in `ErrorCodes.g.cs` in parity with the registry; no doc describes `.doti/templates/` as a committed/editable source (all references are runtime/materialized); the renamed `SentruxRegression.Evaluate` three-state signature survives in no stale doc; `CLAUDE.md`/`AGENTS.md` stay thin (7 lines).

## Axis 3 — source ↔ installed/rendered

`doti render-skills --check` → **pass (0 drifted)**; `doti payload check` → **pass (86 managed files)**. `CLAUDE.md`/`AGENTS.md` thin; no hand-edited installed skills.

## Disposition

All applicable axes clean on the final diff. Three drift findings fixed in-cycle (2 code, 1 docs-source + re-render); the gate is green over the post-fix change set. One non-blocking cleanup (dead `SentruxSourceScope`) logged for a follow-up. Stamping `drift-review`.
