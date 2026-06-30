# 027 — Architecture review: codified stamp reconciliation + orphan-prune

## Triage

Change class: `*.Core` logic in `Hx.Cycle.Core` (freshness/stamp/refresh/recovery) + `Hx.Doti.Core` (install reconcile/parity). No CLI surface, no new error code, no stage-model reorder, no `scaffold/templates/**`. Additive/nullable proof fields. This review was substantially pre-run by the RCA workflow's adversarial pass; the findings below are its verdict folded into the design.

Lenses run: design-soundness, data-contract, edge-case/failure-mode, security/determinism, blast-radius, simpler-alternative, testability. Not applicable: CLI/output-shape (no surface), generated-template (no scaffold change).

## Findings

### F1 — Safety boundary of auto-rebind (Severity: BLOCKER, RESOLVED in design)
The adversarial pass proved that auto-rebinding any `PrereqArtifactChanged` would **rubber-stamp a downstream artifact to a changed upstream it was never re-derived against**, inverting "fix the artifact, never the proof". Resolution (now in the design, FR-001/003): auto-rebind ONLY when (a) own artifact canonical-unchanged, (b) every producing upstream `Fresh`, (c) every shared prereq path byte-identical — i.e. a pure edge/reorder, never a content value change. The operator's spec-edit case stays `RerunRequired`. **Resolved — no longer a blocker because the design adopts the narrow invariant.**

### F2 — Review-kind stages must never auto-rebind (Severity: HIGH, RESOLVED)
A review verdict (arch-review/analyze/drift-review) is a judgment over its inputs; a changed input invalidates it. The classifier is a pure `StaleReason`-only map, so the carve-out is codified at the `CycleRecoveryPlanner` gate (which has the StageModel): a review-kind dependent is downgraded to `RerunRequired` on `ReBindContentEqual` (FR-003). A test asserts analyze/arch-review stay `RerunRequired` on any upstream change.

### F3 — Change-set-bound stages stay RerunRequired (Severity: HIGH, RESOLVED)
`ChangeSetDiffers` keeps mapping to `RerunRequired` (FR-002), and the `requireChangeSetIdentity` arm fires before the prereq arm, so a code edit during implement is never reclassified — the 021/026 fix is locked by a regression test (SC-001/W1 scenario 4).

### F4 — Cascade re-entrancy/termination (Severity: MEDIUM, addressed)
The on-stamp auto-cascade (FR-006) must not recurse (nested Stamp→Refresh→Stamp). Addressed by an ambient suppress-flag, closure-bounding, prereq-first ordering, and per-pass progress (a step that doesn't become Fresh is left, never retried); a cascade failure never fails the primary stamp.

### F5 — Orphan-prune source + safety (Severity: MEDIUM, addressed)
Prune candidates come from an ON-DISK scan of agent SkillsRoots (not only the prior manifest, which is empty on pre-category repos like agentx — the chicken-and-egg the adversarial pass flagged), and deletion is gated by the existing clean-baseline-hash guard (operator-edited orphans preserved/blocked). `payload check` gains surplus-dir detection (FR-008/009).

### F6 — Data-contract / migration (Severity: LOW)
All new proof/state fields are additive + nullable (the established `PrerequisiteArtifactHashes`-null → `MissingBinding` → `SafeReinterpret` precedent), so existing proofs never wedge and no schema-version bump is forced (FR-010).

## Lens verdicts

- **Design-soundness:** PASS — reconciliation is a strict projection over the same `Check` freshness; refresh, the on-stamp cascade, and the transition rebase all call one content-equality-gated path; fail-closed preserved.
- **Data-contract:** PASS — additive/nullable; gate-proof digest byte-unchanged.
- **Failure-mode:** PASS — re-entrancy guard + termination + cascade-failure isolation; the BOTH-reorder-and-content-change case fails the precondition → `RerunRequired`.
- **Security/determinism:** PASS — no new hashing path; `ChangeSetDiffers` untouched; orphan-prune clean-only.
- **Blast-radius / simpler-alternative / testability:** bounded to the 9 enumerated sites + the test matrix; the narrow invariant is the simplest correct option (the broad auto-rebind was rejected as unsafe); every FR/SC is command-backed by a test.

## Decision

**APPROVED — no open BLOCKER.** The one BLOCKER (F1) is resolved by the design's narrow, content-equality-gated invariant; F2/F3 codified in the engine (not prose); F4/F5/F6 addressed. Proceed to tasks.
