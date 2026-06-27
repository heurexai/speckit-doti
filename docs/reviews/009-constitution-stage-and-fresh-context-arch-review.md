# Architecture Review — Feature 009: Constitution Stage and Always-Fresh Context

**Stage:** `/06-doti-arch-review` (multi-lens design review before implementation). **Date:** 2026-06-28.
**Artifacts:** [spec](../specs/009-constitution-stage-and-fresh-context.md) · [plan](../plans/009-constitution-stage-and-fresh-context-plan.md) · [tasks](../tasks/009-constitution-stage-and-fresh-context-tasks.md). **Diff context:** design review (pre-implementation); the change set so far is docs-only, so lenses review the *planned* design.

## Triage — change shape and activated lenses
009 spans three code-bearing surfaces and one prose surface:
- **Runtime code** (`Hx.Doti.Core`: `ConstitutionService`, `ConstitutionInitializer`, `ProjectNameResolver`; `Hx.Embedding.Core`: `CSharpMemberChunker`; `Hx.Runner.Cli`: the `doti constitution` command + the review-context composition) → code lenses ON.
- **Scaffold/install code** (`DotiInstaller`, the payload glob in `Hx.Scaffold.Cli.csproj`) → blast-radius + data-contract ON.
- **Doti prose** (the `doti-constitution` skill + command template, the constitution template + this repo's constitution, plan/arch-review template edits) → clarity/consistency lens ON; code lenses N/A.
- **ML calibration** (`Thresholds`, the .NET gold set) → testability + design-soundness ON.

Lenses **not applicable** (no-op): persistence/transaction (no DB), concurrency (no shared mutable state), network/security-surface (the finder is local/CPU, models hash-verified, no new I/O) — security exits *not applicable* beyond the model-integrity invariant already enforced by 008's `ModelManifestValidator`.

## Findings

### BLOCKER: 0 · HIGH: 0
The design is sound and fits the established patterns (thin-CLI→`*.Core`, single-sourced rendered prose, managed-asset preservation, the Embedding zero-dep and Gate/Cycle↛Semantic boundaries). No finding blocks `/07-implement`. The MEDIUMs below are design refinements to honour during implementation.

### MEDIUM

- **M1 — §2 emit must be VERBATIM to guarantee byte-identity (data-contract; SC-003).** `ConstitutionService` projects §2, and SC-003 requires the injected content be **byte-identical** to on-disk. *Risk:* if the service re-renders §2 from a parsed model, whitespace/heading normalization breaks byte-identity, and a freely operator-edited constitution defeats a brittle parser. **Fix:** emit a **verbatim substring** of the file — key extraction on a stable, documented §2 anchor (a fixed `## §2 — Project declarations` heading the template + this repo's constitution both carry) and slice from that heading to EOF (or the next top-level marker); `--section full` emits the whole file verbatim. The §2 anchor becomes part of the template contract (T002) so the parser and the authored files cannot diverge. *Evidence:* spec SC-003, T006/T010.
- **M2 — `CSharpMemberChunker` must be lexer-aware, not naive brace-counting (design-soundness; FR-013).** A pure brace-depth split miscounts on `{`/`}` inside string literals, char literals, comments, interpolated/verbatim/raw strings → merged or split chunks that degrade embedding quality. **Fix:** a minimal hand-rolled scanner that skips string/char/comment spans before counting braces (still zero new deps — keeps the `Hx.Embedding.Core` boundary, T020/T026). The finder is recall-favouring/advisory, so perfection isn't required, but gross mis-chunking would. Roslyn stays the documented escalation only if the lexer proves insufficient. *Evidence:* T019/T020.
- **M3 — calibration must be presence-gated, thresholds committed as data (testability; SC-007).** T022/T023 run Qwen3 + BGE-M3 over the gold set. Inference is environment-dependent (models at `D:\LLM-Models`, absent in CI) and not bit-deterministic. **Fix:** mirror 008's embedding-test pattern — the calibration **test skips when models are absent** (so the gate stays green in CI), the chosen thresholds are recorded as **committed constants** in `Thresholds` + the calibration doc, and the gate never re-runs inference. The "≥1 .NET drift the general thresholds miss" assertion runs only in the model-present lane. *Evidence:* SC-007, the 008 presence-gated embedding tests.
- **M4 — payload exclusion must reclassify, not just delete (blast-radius; FR-009).** Excluding `.doti/memory/constitution.md` from the `_HxDoti` glob removes it from the generated payload manifest. *Risk:* `DotiPayloadParityChecker`/`ManagedAssetScanner` may still expect it (a parity miss), and a generated repo's `doti payload check` must treat its *initialized* constitution as a per-repo artifact, not a verbatim-parity asset. **Fix:** reclassify constitution.md as an **initialized managed asset** (the `cycle-state.json` precedent), so parity ignores its content while still asserting the *template* + skill shipped. Land T017 `[test]` before T018. *Evidence:* `Hx.Scaffold.Cli.csproj:93`, `DotiInstaller.StaticDotiSubdirectories`, T017/T018.

### LOW

- **L1 — title-fill vs preservation interplay (edge-case).** `ConstitutionInitializer` fills the title when initializing; on `doti install` re-run it preserves operator content verbatim (incl. any `{PROJECT_NAME}` an operator left unfilled). Correct (operator owns §2 + the file once it exists) — just assert idempotence + non-resurrection in T015.
- **L2 — injection: review-context composition vs two direct calls (simpler-alternative).** The plan composes §2 into `review-context` for arch-review (stronger codification — the agent can't fetch arch-review context without §2) and calls `hx doti constitution` directly for plan. A uniformly-direct alternative was considered; the chosen split maximises codification where the carrier already exists. Sound — recorded as a deliberate, not accidental, asymmetry.
- **L3 — composition site (fit-with-architecture).** The §2 composition belongs in the **runner** (`RunnerCommands.Doti.ReviewContext`), never in `Hx.Cycle.Core`, to avoid a `Cycle.Core → Doti.Core` edge. T010/T026 pin this with an ArchUnit assertion — keep that assertion in the test, not just the prose.

## Rule/Boundary deltas to enforce (carried into implement)
- ArchUnit: `ConstitutionService`/`ConstitutionInitializer`/`ProjectNameResolver` resolve within `Hx.Doti.Core` (cliDelegation/cliSurfaceConfinement already cover `*Service`/`*Resolver`); `CSharpMemberChunker` keeps `Hx.Embedding.Core` free of `Hx.*` except Contracts; **Gate/Cycle ↛ Semantic** (008 FR-020) and **Cycle.Core ↛ Doti.Core** unbroken. No `rules/architecture.json` family addition required; no `.sentrux/rules.toml` layer change (constitution + templates are Sentrux source-excluded prose, 008 FR-029).

## Verdict
**Design is sound, modular, and pattern-consistent. 0 BLOCKER / 0 HIGH.** Four MEDIUM refinements (verbatim §2 emit, lexer-aware chunker, presence-gated calibration, payload reclassification) are to be honoured during `/07-implement` — they sharpen correctness without changing the architecture. Cleared for implementation.
