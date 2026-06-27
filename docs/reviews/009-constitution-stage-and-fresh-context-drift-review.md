# Drift Review — Feature 009: Constitution Stage and Always-Fresh Context

**Stage:** `/08-doti-drift-review` (before→after diff vs the approved design). **Date:** 2026-06-28.
**Cycle base:** `064a906` (arch-review commit). **Implementation commit:** `6cc3b5a` (`implement: 009`). **Diff:** `064a906..HEAD` ∪ the implement working tree — 46 files (WI1–4 constitution, WI5 .NET finder, the folded LocalReleaseRoot bug fix, README/diagram, cycle docs).

## Axis 1 — spec ↔ code (PASS)
- `gate run --profile normal`: **green** over the full change set (the change-set-bound proof; the affected-test base now matches the gate-proof base after removing the stray `dev` branch — see Note).
- `hx doti converge`: **26/26** declared 009 requirements (FR-001–016 + SC-001–010) covered. The two converge "misses" (`FR-020`, `FR-029`) are **008 cross-citations** in the spec (the Gate/Cycle↛Semantic boundary and Doti-prose Sentrux exclusion), not 009 requirements — documented in the [analyze report](009-constitution-stage-and-fresh-context-analyze-report.md); intentionally retained.
- Each FR has a real enforcing mechanism, matching the plan + arch-review: §2 delivery is **code-enforced** (composed into `review-context`; `hx doti constitution` carrier) while §2 *evaluation* is **agent-judged/advisory** (FR-012) — nothing downgraded from enforced to advisory. All new behavior lives in `*.Core` (`ConstitutionService`/`ConstitutionInitializer`/`ProjectNameResolver`/`CSharpMemberChunker`); the CLI deltas are wiring-only.
- The four arch-review MEDIUMs were honoured: **M1** §2 emit is a verbatim line-anchored slice (byte-identity, CRLF/LF-robust); **M2** the chunker is lexer-aware (string/char/comment masking before brace-counting); **M3** the .NET calibration is presence-gated (skips model-absent, thresholds committed as constants); **M4** the constitution is excluded from the shipped payload (generated repos initialize from the template), the source-based parity check unaffected.

## Axis 2 — code ↔ docs (PASS)
- The new `hx doti constitution` command is documented in the agent context + the `doti-constitution` skill; the `.NET`-tuned finder + **match-type guidance** (paraphrase→semantic, IDs→grep, contradictions→reasoning) is in the agent context (SC-009).
- `review-context`'s output shape changed (`data.review.*` + `data.constitution`); the only field-consuming reader (`doti-arch-review.md`) was updated to the new paths — **no stale `data.applicableLenses` reference remains** anywhere under `.doti/core`.
- **README code↔docs drift caught + fixed:** the README revamp had dropped the `cliSurfaceConfinement`/`cliDelegation` family ids the architecture-guidance gate requires; re-added to the architecture-gates bullet (the offending side — the README — was corrected, never the test).
- The plan/arch-review/plan-template prose cites §1/§2 by reference (the stale 7-principle Constitution Check list is fixed).

## Axis 3 — source ↔ installed (PASS)
- `doti render-skills --check`: **clean** (90 managed payload files; the new `doti-constitution` skill rendered to `.claude` + `.agents`, agent-context re-rendered).
- `doti payload check`: **clean** (parity for 90 managed files). No hand-edited installed assets — all changes are in `.doti/core` source + re-rendered.

## Note — gate-proof base-ref RCA (resolved in-cycle)
The first implement→drift-review transition failed `affected-test proof base ref does not match`. RCA: a `dev` branch created earlier this session made `GateRunner.ResolveBaseRef` resolve the affected-test base to `dev` (`954d52f2`), while `GateProofStore.Persist` uses the cycle base (`064a906`, the per-stage transition commit) — a divergence 008 never hit (no `dev` branch → both resolve to HEAD = the cycle base). The `dev` branch was an unnecessary over-correction (the cycle already rebases its base to concrete per-stage SHAs); removing it restored consistency, and the re-run gate proof carries matching base refs. (A latent gate-engine inconsistency to fold into a future cycle: `GateRunner.ResolveBaseRef` should prefer the cycle base when one exists.)

## Verdict
**No open drift in any applicable axis.** Spec↔code green + fully covered, code↔docs consistent (one real README drift found and fixed), source↔installed parity clean. Ready for `/09-doti-release`.
