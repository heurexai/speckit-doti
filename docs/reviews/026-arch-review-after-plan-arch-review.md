# 026 — Architecture review: arch-review after plan (cycle stage reorder)

## Triage

Change classes: **Doti prose** (workflow.yml, skills.json, command templates, agent-context/constitution/spec templates, README) + **one production code edit** (`DotiWorkflowRegistry.cs` — a static value table) + **test updates** (number-coupled tests) + **re-render** of installed assets. No new control flow, no new CLI surface, no new dependency, no `scaffold/templates/**` generated code.

Lenses activated: design-soundness, data-contract, blast-radius, simpler-alternative, fit-with-current-architecture, testability. Lenses **not applicable** (no sub-agent spawned): security (no auth/input/secret surface), edge-case/failure-mode beyond those captured below (no new runtime branches), modularity-smells beyond the pre-existing dual-source noted below. Direct review (not parallel sub-agents) is justified by the no-new-logic triage.

## Findings

### F1 — Renumber breaks number-coupled tests (Severity: MEDIUM) — evidence-backed
`grep` confirms the old numbers are asserted in **5 test files**: `test/Hx.Doti.Tests/DotiWorkflowRegistryTests.cs`, `test/Hx.Runner.Tests/DotiWorkflowRegistryTests.cs`, `test/Hx.Impact.Tests/ImpactCommandsTests.cs`, `test/Hx.Cycle.Tests/RefreshTests.cs`, `test/Hx.Sentrux.Tests/SentruxSourceScopeTests.cs` (~10 occurrences of `0[456]-doti-*`). If not updated, `dotnet test` / the gate fail at implement.
**Fix:** expand the implement scope so **T008 explicitly includes these 5 test files** (update assertions to the new ordinals/paths). This is mechanical, not a design change — does not block design approval but MUST be done in implement. The file list is enumerated here (this record is what implement reads); the tasks doc is left unedited to avoid staling its bound proof.

### F2 — Dual-source stage model (Severity: MEDIUM, pre-existing)
The order/number/chain lives in BOTH `workflow.yml` (backward prereqs, cycle enforcement) and `DotiWorkflowRegistry.cs` (forward chain + ordinals → SkillId, rendering). They can silently diverge. This change does not create the duplication but must not worsen it.
**Fix (in scope):** T003 adds/relies on a consistency test asserting the registry's `(ordinal, command, order)` matches `workflow.yml`. **Fix (deferred, out of scope):** unifying to a single source is a separate feature (recorded in plan Risks) — correctly not bundled here.

### F3 — Renamed skill directories (Severity: LOW)
`04/05/06-doti-*` rename the installed skill dirs; old slash-commands stop resolving (intended). Re-render (T009) regenerates `.claude/`+`.agents/`; the SC-004 grep + `render-skills --check` (T010) catch any orphan or stale reference. No hard-coded skill *file path* outside the render pipeline was found that would break silently.

### F4 — Declaration order is semantically load-bearing (Severity: LOW) — evidence-backed
`StageModel.TransitivePrereqStages` returns prereqs in **workflow.yml declaration order** (`Stages.Where(...)`), so reordering must move the stage *entries*, not only edit `prereqs:`. T001 reorders entries — correct. Verify at T010 that `cycle check` reports the new order.

### F5 — In-flight cycle under old order (Severity: LOW)
A cycle mid-flight when this ships may read an unsatisfied prereq under the new graph (re-run the out-of-order stage). Stage IDs and `cycle-state.json` schema are unchanged (FR-008), so no proof is orphaned. Documented in spec edge cases. Cycle 026 itself runs under the old order — expected.

## Lens verdicts

- **Design-soundness:** PASS — the fail-closed safety property is preserved and, in fact, strengthened in intent (design reviewed before tasks). New prereqs (`arch-review←plan`, `tasks←arch-review`) are exactly the inversion required; T003 + the SC-002 negative stamp test guard correctness.
- **Data-contract:** PASS — `workflow.yml` stays schemaVersion 2; `cycle-state.json` schema, stage IDs, and `produces` patterns unchanged (FR-008).
- **Blast-radius:** bounded — three sources + ~5 tests + re-render; all enumerated, all proof-gated (grep, render-check, test, gate). F1 is the one scope addition.
- **Simpler-alternative:** the in-place edit is the simplest correct option; the cleaner single-source refactor is rightly deferred (would enlarge blast radius and risk the reorder).
- **Fit-with-current-architecture:** PASS — fits the declarative-model + single-edit-then-render pattern; the `*.Core`/`*.Cli` boundary is untouched.
- **Testability:** PASS — every FR/SC has a command-backed check (cycle check, negative stamp, render/payload parity, grep, gate); T003 adds the missing consistency guard.

## Decision

**APPROVED — no BLOCKER.** Two MEDIUM findings (F1, F2) are folded into the implement scope: F1 expands T008 to include the 5 test files; F2 is mitigated by T003's consistency test (the deeper dedup is a deliberate, recorded follow-up). Proceed to implement.
