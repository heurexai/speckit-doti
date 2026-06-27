# Analyze Report — Feature 009: Constitution Stage and Always-Fresh Context

**Stage:** `/05-doti-analyze` (cross-artifact consistency backstop). **Date:** 2026-06-28.
**Artifacts reviewed:** [spec](../specs/009-constitution-stage-and-fresh-context.md) · [plan](../plans/009-constitution-stage-and-fresh-context-plan.md) · [tasks](../tasks/009-constitution-stage-and-fresh-context-tasks.md) · [constitution](../../.doti/memory/constitution.md).

## Coverage
`hx doti converge`: **26/26** declared 009 requirements (FR-001–016 + SC-001–010) covered by ≥1 task — verified by command, not assertion. No FR/SC numbering gaps in 009's own range.

**Converge false positives (FR-020, FR-029):** converge reports two "uncovered" ids, FR-020 and FR-029. **These are not 009 requirements** — 009 declares only FR-001–016. They are the regex matching this spec's **cross-citations of 008's** requirements: `008 FR-020` (the Gate/Cycle ↛ Semantic dependency boundary, Architecture Impact) and `008 FR-029` (Doti-prose is Sentrux source-excluded, Sentrux/Hygiene Impact). Both citations are accurate and load-bearing (the design preserves both 008 invariants); they are deliberately retained rather than contorted to satisfy an advisory regex. Recorded here so the same false positive at `/08-drift-review` is pre-explained.

## Coverage map (each FR/SC → task)
- FR-001 → T009 · FR-002 → T001/T002 · FR-003 → T009 · FR-004 → T001/T002/T014 · FR-005 → T002 · FR-006 → T006/T007/T008 · FR-007 → T010/T012 · FR-008 → T010/T011/T013 · FR-009 → T009/T017/T018 · FR-010 → T015/T016/T018 · FR-011 → T001/T003 · FR-012 → T013 · FR-013 → T019–T023 · FR-014 → T024 · FR-015 → T004/T005/T018 · FR-016 → T006/T010/T012.
- SC-001 → T001 · SC-002 → T015/T017/T018/T028 · SC-003 → T006/T008/T010/T029 · SC-004 → T013 · SC-005 → T003/T017 · SC-006 → T015 · SC-007 → T022/T023 · SC-008 → T001/T002/T014 · SC-009 → T024/T028 · SC-010 → T004/T015.

## Cross-artifact consistency — load-bearing verifications (all PASS)

- **Constitution stays a project artifact, not a per-cycle stamp (FR-003):** PASS — no task adds the constitution to `workflow.yml`; T009 makes `doti-constitution` an unnumbered utility skill (joining `doti-amend`/`doti-drift-fix`), and the cycle engine never stamps it. Confirmed: `workflow.yml` carries only the numbered `/01`–`/09` stages.
- **§2 delivery codified, §2 evaluation agent-judged (FR-007/008/012):** PASS — T011 composes `ConstitutionService` into `hx doti review-context` (code-enforced delivery); T012/T013 make plan/arch-review *evaluate* §2 (agent-judged); no task turns §2 into a fail-closed gate step — the deterministic gate (build/test/ArchUnit/Sentrux/hygiene) is untouched. Honest about advisory-vs-enforced.
- **Verified bug — generated repos inherit this repo's constitution:** PASS — reproduced this session (`Hx.Scaffold.Cli.csproj:93` payload glob ships `.doti/memory/constitution.md`; `DotiInstaller.StaticDotiSubdirectories` reconciles `memory/`; `.doti/memory/constitution.md` ≡ `.doti/core/memory/constitution.md`). T017 `[test]` + T018 (exclude from payload + initialize from template) close it — landed paired.
- **No §1 placeholders / no doc-versioning ritual (FR-004/005):** PASS — T001 `[test]` asserts the template carries no fillable principle for any §1 invariant (versioning/CLI shape/quality-gate) and no SemVer doc-version line; T002 builds the §1 reference block + §2-only placeholders; T014 fixes the stale 7-principle Constitution Check list.
- **Finder stays advisory, code↔docs-only (FR-013/014):** PASS — T021 keeps the never-gating, code↔docs-only contract (NOT wired into `converge`/`analyze`); T024 documents match-type guidance (paraphrase→semantic, IDs→grep, contradictions→reasoning); T026 pins Gate/Cycle ↛ Semantic (008 FR-020). No `semantic-search` command or warm daemon introduced.
- **`Hx.Embedding.Core` zero-`Hx.*`-dep preserved (WI-5):** PASS — T020 makes `CSharpMemberChunker` internal with no new deps (Roslyn documented as fallback only); T026 asserts the boundary. Models verified present at `D:\LLM-Models` (Qwen3 GGUF + BGE-M3 ONNX), so T022/T023 calibration is runnable, not aspirational.
- **Composition keeps `Cycle.Core ↛ Doti.Core` clean (FR-008):** PASS — T011 places the §2 composition in the **runner** (`RunnerCommands.Doti.ReviewContext`), not in `Hx.Cycle.Core`; T010/T026 add an ArchUnit assertion that no Cycle→Doti core edge appears.
- **Dependency ordering:** PASS — Phase 0 (template + this-repo constitution + `ProjectNameResolver`) precedes the skill/service/command that reference it; every `[test]` precedes its implementation; T017 paired with T018; no task depends on a higher-numbered task.
- **Cited code locations exist:** `Hx.Scaffold.Cli.csproj` payload glob, `DotiInstaller.StaticDotiSubdirectories`, `RunnerCommands.Doti.ReviewContext`, `ReviewContextProjector`, `Qwen3Embedder`, `Thresholds`, `HxLocalConfiguration`, `plan-template.md:15` — all present; the new `ConstitutionService`/`ConstitutionInitializer`/`ProjectNameResolver`/`CSharpMemberChunker` names do not collide.

## Findings

**CRITICAL: 0 · HIGH: 0** → implement is not blocked.

**MEDIUM (non-blocking):**
- **M1 — converge cross-citation false positives (FR-020/FR-029).** Documented above; the citations are accurate 008 references, intentionally retained. No spec change (spec is source of truth; not contorted for an advisory regex).

**LOW:**
- **L1 — held bug fix folded as T025.** The LocalReleaseRoot bug fix + README/diagram are part of this cycle's implement diff (operator decision to fold into 009). Tasked explicitly (T025) so the drift-review diff is fully accounted for, not a stray.
- **L2 — analyze self-binding bootstrap.** This report is 009's first analyze record; its `produces`-binding is forward-consistent with the cycle (008 already bound the review stages).

## Constitution alignment
Checked against this repo's 9 principles (Deterministic Ownership, Bootstrap Honesty, Template Boundary, Public Hygiene, Cross-Platform, Engineering Discipline, Operator Decisions, Codified Cycle, Channel Independence): **no violation.** §2 evaluation stays advisory (Bootstrap Honesty: advisory labelled advisory); new behavior lands in `*.Core` (Channel Independence); the constitution is a project artifact, not a per-cycle stamp (Codified Cycle); the project name is auto-derived (Public Hygiene — no machine paths).

## Verdict
**Internally consistent, fully covered (26/26 real; the 2 converge "misses" are 008 cross-citations), correctly ordered, honest about advisory-vs-enforced surfaces. 0 CRITICAL / 0 HIGH — implement is not blocked.** Ready for `/06-doti-arch-review`.
