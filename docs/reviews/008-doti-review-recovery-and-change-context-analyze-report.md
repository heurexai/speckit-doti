# Analyze Report — Feature 008: Doti Review Recovery and Change Context

**Stage:** `/05-doti-analyze` (cross-artifact consistency backstop). **Date:** 2026-06-27.
**Artifacts reviewed:** [spec](../specs/008-doti-review-recovery-and-change-context.md) · [plan](../plans/008-doti-review-recovery-and-change-context-plan.md) · [tasks](../tasks/008-doti-review-recovery-and-change-context-tasks.md).

## Coverage
`hx doti converge`: **64/64** spec requirements (FR-001–042 + SC-001–022) covered by ≥1 task. No FR/SC numbering gaps. Verified by command, not assertion.

## Cross-artifact consistency — load-bearing verifications (all PASS)
A clean-context adversarial pass spot-checked every consistency claim against the real code.

- **Sentrux baseline "never removed" (FR-031):** PASS — no task/plan/spec contains baseline-removal language; T026 guards `SentruxBaselineRunner.Save` with `SentruxRebaselinePolicy.Authorize`; the gate path never calls `Save`.
- **Engine = Qwen3-GGUF primary + BGE-M3-ONNX fallback (FR-039):** PASS in the binding artifacts — plan 5a + table and T035/T036/T038/T039 all state Qwen3/GGUF primary, BGE-M3/ONNX fallback, active engine reported. polaris-core confirmed to carry both engines behind `IEmbedder`/`SemanticEngineFactory`.
- **Templates removal vs parity (FR-014/015 vs FR-017):** PASS — T032 + plan 4a test that with **source `.doti/templates` absent**, the *materialized* payload matches `.doti/core/templates` byte-for-byte; T034 closes the silently-skipped hole at `DotiPayloadParityChecker.cs:45`. Validates the materialized copy, not the committed twin.
- **Reproduced-bug fixes tasked (FR-038, FR-004):** PASS — FR-038 → T012 `[test]` (one-stamp clean start AND a stray untracked file still blocks) + T013 (impl); FR-004 → T014 with SC-021.
- **FR-033/034/035 regression-not-rebuild:** PASS — T027 is `[test]` pinning the verified-existing release-train behavior; only FR-036 (T028/T029) and FR-037 (T030/T031) are new build.
- **Deterministic-surface honesty:** PASS — `refresh-plan`, `refresh`, `review-context`/`change-context`, `drift-candidates`, the utility skills are listed under "Planned/advisory surfaces (do NOT report as passing gates until implemented)." None presented as implemented.
- **Dependency ordering:** PASS — Phase 0 (`ChangeSetContext`/`StaleReason`/`FeatureArtifactScope`) precedes all consumers; T014 paired with T009 (T009 < T014); every `[test]` precedes its impl; no task depends on a higher-numbered task.
- **Plan→task completeness:** PASS — every named `*.Core` type, the three new projects, the `workflow.yml`/template/skill changes, and all new ArchUnit rules have a covering task. No orphan task inventing out-of-plan scope.
- **Cited code locations exist:** `ImpactChangeCollector`, `StageModel.ResolveProduces`, `GateProofStatus` field, `CliNextAction`, `HxLocalConfiguration`, `FreshnessEvaluator` stale arms, `.sentruxignore` — all present; the new `Hx.Embedding.*`/`Hx.Semantic.*` names do not collide in speckit-doti.

## Findings

**CRITICAL: 0 · HIGH: 0** → implement is not blocked.

**MEDIUM (non-blocking; the binding plan/tasks are already correct):**
- **M1 — FR-021 prose is engine-agnostic.** FR-021 (trust boundary: "local/private only … on CPU") deliberately does not name the engine; FR-039 carries the Qwen3-GGUF/BGE-M3-ONNX specifics. Correct separation of concerns — no change required.
- **M2 — superseded historical reference.** The `/02` clarification entry mentions "`Hx.Semantic.Core` / `Hx.Discriminate.Core`"; the embedding engines are in `Hx.Semantic.Core` (`Hx.Discriminate.Core` is polaris-core's NLI discriminator — a real source lib, but unrelated to embedding). The current Assumptions + plan + T036 correctly cite `Hx.Semantic.Core` only. The `/02` mention is a superseded record.

**LOW:**
- **L1 — line-number imprecision.** Plan cites `sentrux.json:7` for `signalToleranceBand`; actual `rules/sentrux.json:6`. Value (100) and meaning correct.
- **L2 — analyze self-binding bootstrap.** For 008's own cycle, this analyze report is the first; the `produces`-binding for `analyze` lands via T014 in 008's build (forward-consistent).

## Verdict
**Internally consistent, fully covered, correctly ordered, honest about planned-vs-implemented surfaces. 0 CRITICAL / 0 HIGH — implement is not blocked.** Ready for `/06-doti-arch-review`.
