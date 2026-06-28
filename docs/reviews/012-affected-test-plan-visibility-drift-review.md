# Drift Review — Feature 012: Gate & Affected-Test Visibility

**Stage:** `/08-doti-drift-review`. **Date:** 2026-06-28. **Implement commit:** `f38cf98`.

## Axis 1 — spec ↔ code (PASS)
- `gate run --profile normal`: **green** over the full implement change set (code change → full suite); the new `GateTrace` rendered (mode=full, code scope) during the run, exercising the feature on itself.
- **The load-bearing invariant (M1 / FR-011 / 008 FR-020) is proven:** `GateTrace`/`ChangeSummary`/`AffectedTestInventory` live ONLY on the `GateRunResult` envelope, never on `AffectedTestProof.Plan`. The new ArchUnit assertion (`*ProofHasher ↛ the visibility trace records`, +load guard) and `ProofHashBoundaryTests` (plan/test-scope/executed hashes **byte-identical** with vs without a fully-populated trace) both pass — review context never enters a deterministic proof.
- Implementation matches the plan: additive contracts (nullable/defaulted — `ContractAdditivityTests` deserializes a pre-012 proof), capture/projection in `*.Core` (`ChangeSummaryProjector`, `AffectedTestInventoryProjector`, `GateTraceProjector`), render-only in the kernel. Two-tier honored (basic every gate; classes+inventory only at implement-stage code — resolved in the runner from `CycleStateStore`, engine stays stage-agnostic). Test totals never build-all (`MetadataReader` over already-built selected assemblies; honest `unknown` otherwise). Tests: Gate 13, Impact 47, Kernel 44, Architecture 15 (+ all regressions) green.

## Axis 2 — code ↔ docs (PASS)
- `.doti/agent-context.md` (+ its template) updated to document `--stream`, the human trace, the two-tier summary, and the proof-hash-boundary statement; `hx describe` carries `--stream`. No stale "gate output is summary-only" claim remains.

## Axis 3 — source ↔ installed (PASS)
- `doti render-skills --check` + `doti payload check` clean (the gate's skill-drift + payload-parity steps passed); the agent-context was re-rendered from source, no hand-edited installed asset.

## Note — release train
012 completes to drift-review as the **first member of the 012+013+014 release train**; it is finalized as completed-unreleased when 013 starts. No release here.

## Verdict
**No open drift.** Gate green, the proof-hash boundary proven, contracts additive, docs consistent, parity clean. Ready to carry into the train (start `/01` for 013).
