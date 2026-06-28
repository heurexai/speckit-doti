# Drift Review — Feature 014: Structural-Engine Violation Detail

**Stage:** `/08-doti-drift-review`. **Date:** 2026-06-28. Scoped from the implement change set: additive render-only contracts (`ArchitectureViolation`, `SentruxViolation`, `StructuralStepViolations` + the three host records), ArchUnit capture (`ArchitectureTestRunner` + the repo & template arch tests via a shared `ArchitectureViolationMarker`), Sentrux capture (`SentruxOutputParser`/`SentruxChecker`), gate-trace + ladder render (`GateRunner`, `GateTraceProjector`, `StructuralViolationProjector`, `CliRenderer`), and the new tests. A **code** diff — all three axes apply.

## Axis 1 — spec ↔ code (PASS)

Each FR has a real enforcing mechanism, matching the approved plan/arch-review design:

- **FR-001/FR-002** (ArchUnit violating types + rule description, additive `ArchitectureTestCase`): the repo + template arch tests now evaluate via `rule.Evaluate(Arch)` and emit `EvaluationResult.Description` + failing-object names through a shared `ArchitectureViolationMarker.Format`; `ArchitectureTestRunner.ParseTrx` parses the failing `ErrorInfo/Message` into `ArchitectureViolation`s. Outcome-neutral — the repo's 15 architecture tests still pass (verified), so the change surfaces detail without altering pass/fail (FR-007).
- **FR-003** (Sentrux structured offender, additive `RuleViolationDetails`): `SentruxOutputParser` now yields a structured `SentruxViolation` per object alongside the unchanged `RuleViolations` string flatten; `SentruxChecker.Build` sets `RuleViolationDetails`.
- **FR-004** (detail in the 012 ladder): `GateRunner` hands offenders **out-of-band** to `GateTraceProjector` via an `onStructuralViolations` callback — the `GateStep`/`GateProof` it builds is unchanged (summary evidence only) — and the projector sets `GateTrace.StructuralViolations`; `CliRenderer` renders them under failing structural ladder steps + a standalone offender panel.
- **FR-005** (unknown-with-reason): fail-closed everywhere — an unparseable ArchUnit failure → one `ArchitectureViolation` with `UnknownReason`; a Sentrux summary-style/string-only violation → `SentruxViolation` with null location + `UnknownReason`; never empty/zero/fabricated. (Verified in the capture tests.)
- **FR-006** (deterministic order + "+N more"): `CapOffenders` caps the displayed set (≤5, then "+N more"), deterministically ordered; the full set stays in `--json`.
- **FR-007 / SC-006 — the load-bearing invariant (PROVEN):** offender detail lives ONLY on `GateRunResult.Trace` + the standalone result contracts; **no `*ProofHasher`, no hashed proof contract (`GateProof`/`GateStep`/`GateEvidence`/`AffectedTestProof`/`TaskCompletionProof`), and `CommitPreparation` are in the diff** (verified by `git status`). `ProofHashBoundaryTests` proves `DigestOf(PersistedGateProof)` is byte-identical with null vs a fully-populated `StructuralViolations`, replicating `CycleService.CommitPreparation.DigestOf`; the 012 ArchUnit boundary assertion + load-guard are extended to the three new records. The architecture/Sentrux outcomes are unchanged.

Matches the plan: additive on the envelope, capture in `*.Core`, render in the kernel/CLI; no rule-family or Sentrux-policy change; the rejected alternatives (detail on `Evidence`; replacing `RuleViolations`) were not taken.

## Axis 2 — code ↔ docs (PASS)

- **No stale claim exists.** A repo-wide grep finds no doc asserting the structural steps are offender-free / "summary-only"; the agent-context describes the gate ladder generically (the new offenders are additive output, not a contradicted claim). `hx`/command surface is unchanged (no new flags), so `--help`/`describe` need no update.
- **Release-facing notes deferred to the aggregated `/09`** (consistent with how 012 added none at implement): the richer structural output will be noted in the CHANGELOG/README against the train's release version, where the release-doc proof is evaluated — not fragmented across three implements. This is a deliberate deferral of *release notes*, not an open code↔docs drift (the changed code describes itself; nothing documents the old behavior).

## Axis 3 — source ↔ installed (PASS)

- 014 changes no `.doti/core` skill/template/payload source; `doti render-skills --check` + `doti payload check` stay clean (the gate's skill-drift + doti-payload steps confirm). No hand-edited installed asset.

## Gate

`gate run --profile normal` green over the full change set; the architecture-test (15/15) and sentrux-check outcomes are unchanged by the visibility detail and the persisted proof digest is provably unmoved (visibility-only). All regressions green (Gate, Sentrux, Runner, Kernel, Architecture, Templates incl. the generated-template arch tests).

## Operator-directed Sentrux scope correction (in this change set — spec Clarifications / T014)

The implement change set also includes an operator-directed Sentrux scope correction (recorded as the source-of-truth in the spec's Clarifications + Sentrux And Hygiene Impact, plan Addendum, arch-review FR-031 classification, task T014):

- **`.sentruxignore` excludes the repo's `test/` tree** so the structural barometer measures production architecture only. A 014 *test* file (`test/Hx.Gate.Tests/ProofHashBoundaryTests.cs`) had crossed the god-file threshold and dropped the whole-graph signal −14 — a false positive (test fixtures legitimately have many methods per file).
- **Verified non-regression:** with tests excluded the production-only god-file set is unchanged (5 files, all production) and `complexFunctions` unchanged (1); 014's production code added **none**. The production-only signal rises to 6483.
- **Authorized baseline raise (FR-031):** `.sentrux/baseline.json` raised once to the production-only signal via `hx sentrux baseline --authorize-rebaseline` (operator intent + change-set-fresh arch-review + the `functionality-driven-growth` classification). A stricter production floor — never a lowering, never a removed baseline, no rule/limit relaxed. spec↔code is consistent (the spec was amended to record this decision; the code matches).

## Note — release train

014 completes to drift-review as the **third and final member of the 012+013+014 release train**. The train (012 + 013 + 014) is now ready for the aggregated `/09-doti-release` — an explicit operator step. No release here.

## Verdict

**No open drift** in any applicable axis. The load-bearing proof-hash boundary is proven; capture is fail-closed; render is bounded + deterministic; outcomes + proof unchanged. Ready to carry into the aggregated release.
