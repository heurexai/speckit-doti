# 022 — Doti repo version lifecycle — Drift review

Scoped from the implementation diff `baseRef..HEAD` (cycle base `8f30618`, the commit before implement) plus the working tree. Triage: runtime CODE change (new `*.Core` types + a `Hx.Runner.Core` git primitive + the `Hx.Scaffold.Core` version-reporter fix + thin CLI + docs) — all three drift axes apply.

## (1) spec↔code — CLEAN

Every changed hunk satisfies its FR/SC with a real enforcing mechanism, matching the approved plan/arch-review design (single-sourced relation, reuse of the installer reconcile, thin CLI, worktree isolation). Spot-checked each requirement `hx doti converge` flagged against the covering code:

- FR-006/007 (scan skip/empty/error-tolerant) → `DotiRepoScanner` (prune set, one-entry-per-repo, fail-soft enumerate) + `DotiRepoScannerTests`.
- FR-009/010/011/012 (customization preserve/force, ahead-refused, no-baseline degrade+warn) → `DotiUpdater` + `DotiUpdaterTests`.
- FR-014/015 (git-required, worktree apply-back) → `GitWorktree` + `DotiWorktreeUpdate` + `GitWorktreeTests`.
- FR-017/018 (fail-soft, summary counts) → `DotiBatchUpdater` + `DotiBatchUpdaterTests`.
- FR-020 (single-source relation) → `DotiVersionRelationCalculator`, used by BOTH the new commands and `ScaffoldVersionReporter` (T024 fix).
- SC-001..008 → covered by the inspector/scan/update tests + the release gate (full suite + envelope-schema validation).

The `converge` "18 uncovered" output is a tool limitation, not a gap: it reads only the first ID of a slash-abbreviated `[covers FR-005/006/007]` annotation, so it under-counts. The committed analyze report's explicit FR/SC→task mapping table remains the authoritative coverage proof (every FR/SC maps to ≥1 task). No spec was downgraded or deferred.

## (2) code↔docs — CLEAN

Each code change has a matching doc change: the four commands are added to `README.md` (command map + source-free surface), `CHANGELOG.md` (`[Unreleased]`), the agent-context source (`.doti/core/templates/agent-context-template.md`) + the single-source `commandAvailabilityFootnote` in `.doti/profiles/dotnet-cli/profile.json` (re-rendered into every skill + `.doti/agent-context.md` + `AGENTS.md`/`CLAUDE.md`), the `describe` capability model (verified surfacing all four commands + the four new error codes), and `CONTRIBUTING.md` (the feature→dev squash / dev→main merge branch flow). Removed/renamed symbols: the extracted private `DotiInstaller.ReadRepoPayloadVersion`/`StampRepoPayload` survive in no live doc — the only grep hit is `docs/plans/022-...-plan.md` describing the pre-extraction starting state ("currently private") and predicting the `RepoPayloadStore` extraction, which is the correct historical design record.

## (3) source↔installed — CLEAN

Both parity authorities pass: `doti render-skills --check` (rendered skills/agent-context/entrypoints) and `doti payload check --repo .` (93 managed files). No hand-edited installed skills — the footnote + template edits were made to `.doti/core`/`.doti/profiles` source and re-rendered.

## Sentrux growth (FR-031 functionality-driven rebaseline)

The post-commit `sentrux gate` regression tripped on a **+0.1% aggregate signal** growth (measured ≈`0.6488` vs baseline `0.6481`). Root cause (validated against the Sentrux fork source): `sentrux gate` reads the tracked working tree but is blind to UNTRACKED files; the new feature files were untracked at the `/07` gate (staging happens at the stamp), so their growth was invisible until they became tracked at the implement commit — surfacing at `/08`. This is functionality-driven growth, NOT a structural defect, verified:

- **0 rule violations** — no function exceeds `max_cc=25` or `max_fn_lines=120`; no new god-file (still 5); no cycle (`max_cycles=0` holds — the new within-core edges `Hx.Doti.Core → Hx.Runner.Core`/`Hx.Version.Core` are acyclic, both within the `core` layer).
- The delta is import-coupling from ~900 lines of new, single-responsibility production code (7 `*.Core` types + the `GitWorktree` primitive + thin CLI), each within the function-size limit — the modular design the arch-review approved and this review confirms.
- `sentrux verify` (per-function rules) and `architecture-test` both pass.

Sanctioned response: a `hx sentrux baseline` **ratchet** — the floor moves UP to the current legitimate signal; the gate stays enforced (nothing downgraded enforced→advisory), and a future regression below this new floor still fails. The baseline is never lowered.

## Gate

`gate run --profile normal` green (blocking) after the FR-031 rebaseline. The release-profile gate (full suite + security + structural + release-documentation) was run green during implement (T070).

## In-cycle doti-engine fix (Bug #2 — gate proof base ref)

This cycle surfaced a real doti-engine defect: the gate proof's affected-test base ref was resolved INDEPENDENTLY of the cycle base — `GateRunner` planned the affected-test set off the `dev` branch while `GateProofStore.Persist` recorded the cycle's per-stage `BaseRef`. Once the cycle base advanced via the rebase-to-head transition model, the proof's two base refs diverged (here `993dcd0` vs `dev`=`3f59574a`), and the diff/release transition validator (`GateProofValidator`) rejected an otherwise-valid proof with "affected-test proof base ref does not match" — making the drift-review stamp unsatisfiable by any gate re-run. Per the constitution's **self-hosting defect handling** principle, this was fixed in `*.Core` (not worked around) and dogfooded on this very run: both the planner and the persistence now single-source the base through `GitRefs.ResolveProofBaseRef` (the active cycle base wins; `dev`/HEAD resolved to a concrete SHA as the standalone fallback). Verified by 5 new pure tests + the 79-test cycle/gate suite staying green, and by this cycle's own drift-review + release stamps validating (the proof's two base refs now both read the cycle base). A second defect, Bug #1 (the Sentrux fork's `gate` regression is blind to UNTRACKED files — `--include-untracked` exists only on `check`), was RCA'd and validated against the fork source and handed off as a Sentrux-side fix; it is non-blocking (the structural growth here is functionality-driven and was rebaselined).

## Verdict

**No open drift in any applicable axis.** Cleared to stamp drift-review and proceed to `/09-doti-release`.
