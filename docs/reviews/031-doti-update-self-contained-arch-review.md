# Arch Review — 031-doti-update-self-contained

**Stage:** /04-doti-arch-review · **Verdict:** CLEAN — no open BLOCKER in any applicable lens. Proceed to implement.

## Triage

Mixed change. **Code** (run the code lenses): P1 source resolution + P2 prune + P3 self-commit (`Hx.Doti.Core` + the `RunnerCommands.Doti.*` CLI) and P5 the doc-stamp scope fix (`Hx.Cycle.Core`). **Doti-prose** (skip code lenses): P4 the `/doti-bug` skill + bug command templates. No `scaffold/templates/**` generated-code change. Changed-files context: the update/install path (`DotiInstaller`/`DotiUpdater`/`RunnerCommands.Doti.{Install,Update,UpdateAll}`), `CycleService.TransitionReadiness` (P5, already landed + dogfooded), `errorcodes/registry.json`, the bug command templates + `skills.json`.

## Lenses

**Design soundness** — Each fix targets its FR's root cause (verified against the real code in /03): P1 swaps the CLI source from `FindDotiSource(CWD)` to the bundled payload, which also resolves the false `already-current` (FR-003) because `Install` then reads a real `payload.manifest.json`. P2 flips the one over-conservative no-baseline branch in `PruneOneOrphanDir`. P3 adds a single post-reconcile commit step. P5 is a one-`UnionWith` consistency fix mirroring `Check()`. No redesign; each is the simplest correct mechanism. ✅

**Edge-case / failure-mode** — (a) P1: neither bundled nor dev source resolvable → fail-closed coded error, never a silent wrong stamp. (b) P2: a `*doti-*` orphan that is operator hand-edited → pruned (hand-edits to rendered skills are drift per the agent context), NOT an operator-policy customization (skills.json/constitution, which are preserved); a NON-`doti-` operator skill dir is never a candidate (the `Contains("doti-")` filter). (c) P3: non-git repo → skip commit (no error); no changes → no empty commit; `.new` sidecars + gitignored runtime state excluded from staging. (d) P5: a genuinely foreign untracked/modified path still blocks (only the feature's own `OwnedPaths` are excluded) — confirmed by dogfooding (the stamps cleared with the plan doc untracked, and a stray sibling would still trip). ✅

**Data-contract** — `DotiUpdateOutcome`/`DotiInstallResult` gain additive fields (resolved-source origin, pruned paths [already in `Removed`], `.new` merge-pending list, commit sha/skip-reason). Additive only; no removed/renamed field. The `--no-commit` flag is additive. ✅

**Security** — P3's sanctioned commit sets `DOTI_SANCTIONED_COMMIT=1` only in the child `git commit` environment (the existing hook-honored signal), never persisted/exported; the auto message is built from the controlled version + path set (no operator-injected free text). P1/P2 are path operations through the existing `ResolveInside` fail-closed escape guard. No new attack surface. ✅

**Blast radius** — P1/P2/P3 touch only the `doti install`/`update`/`update-all` path; no other command consumes `FindDotiSource` or the prune/commit. P5 touches `ValidateTransitionReadiness` only — and the exclusion is scoped to `FeatureArtifactScope.OwnedPaths`, so every other transition guard is unchanged (a foreign change still blocks). Already dogfooded across 3 stamps with no regression. ✅

**Simpler alternative** — Considered + rejected in /03: parsing `hx version` for the source (vs `AppContext.BaseDirectory`); `--force`-only prune (vs default); `git add -A` (vs precise paths); a separate `hx doti commit` (vs automatic); excluding only the target stage's produces (vs all owned — #42's narrow form misses multi-doc-ahead + the incoming-feature case). The chosen forms are the minimal correct ones. ✅

**Modularity / design-smells** — New `*.Core` types are small single-responsibility units (`BundledPayloadResolver`, `DotiReconcileCommit`); the CLI stays parse→delegate→render; the commit lives in its own type so `Install` stays within the Sentrux function-size budget. No god-object growth. ✅

**Testability** — Each FR has a deterministic check: P1 (neutral-dir + dev-checkout source resolution), P2 (renamed-orphan removal + `payload check` pass; operator-policy preservation), P3 (precision/idempotence/non-git/`--no-commit`), P5 (ahead-doc stamp succeeds, foreign file blocks). All seam-testable in `Hx.Doti.Tests`/`Hx.Runner.Tests`/`Hx.Cycle.Tests`. ✅

**Fit with current architecture** — Honors the `*.Core`/`*.Cli` split, the command→`CliResult`→error-code convention, the managed-asset reconcile already in `DotiInstaller`, and the "commits owned by coded transitions/release paths" constitution invariant (P3 extends it to the asset-reconcile path; P5 makes the transition gate consistent with `Check()`). No `Doti.Core ↔ Cycle.Core` cycle (the resolver/commit are in `Doti.Core`; P5 is in `Cycle.Core`). ✅

## Sentrux / ArchUnit note

Measured at /07 by `gate run`, not here. The changes are branch edits + small new types; no expected god-file growth. Keep `DotiInstaller.Install` within the function-size budget (P3 commit in its own type). No `cliSurfaceConfinement`/`cliDelegation` violation (CLI stays thin).

## Dogfood record

P5 was implemented + built + **used for this cycle's own specify→clarify→plan re-stamps** — they cleared with the plan doc left untracked and zero set-aside dance, which is the working proof FR-016/#42 is fixed (a foreign untracked file would still block). The remaining stages stamp with the built `hx`.

## Conclusion

The design satisfies all 16 FRs with the simplest correct, modular mechanisms that fit the existing architecture and weaken no gate. No applicable lens raises a BLOCKER. **Arch-review stamp authorized.**
