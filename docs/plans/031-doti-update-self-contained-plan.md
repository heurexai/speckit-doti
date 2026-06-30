# 031 — Plan: self-contained `hx doti update`/`install` + bug-cycle RCA discipline

Design that satisfies the spec's FRs with the simplest correct, modular mechanisms that fit the existing `*.Core`/`*.Cli` boundary, the command→`CliResult`→error-code convention, and the managed-asset reconcile already in `DotiInstaller`. Grounded in the real code (`RunnerCommands.Doti.{Install,Update,UpdateAll}.cs`, `DotiUpdater`, `DotiInstaller`).

## Existing-architecture assessment

- `RunnerCommands.Doti.{Install,Update,UpdateAll}.cs` each resolve the payload source as `FindDotiSource(Directory.GetCurrentDirectory())` and pass it as `payloadRoot` to `DotiInstaller.Install` / `DotiUpdater.Update` / `DotiBatchUpdater.Run`. **The source bug lives in this CLI wiring**, not in the `*.Core` reconcilers (which correctly consume whatever `payloadRoot` they're given).
- `DotiInstaller.Install` stamps `.doti/payload.json` from the bundled `payload.manifest.json` descriptor (`ReadBundledPayloadVersion`/`ReadBundledToolVersion`) **only when `bundledVersion is not null`**. A dev `.doti` has no `payload.manifest.json` → null → no stamp → `before==after` → the false `already-current`. So FR-003 is a *symptom* of the FR-001 source bug.
- Pruning ALREADY exists: `FinalizeManagedBaseline` → `PruneOrphanedManagedSkillDirs` → `PruneOneOrphanDir` (027 FR-008). But `PruneOneOrphanDir`'s **no-baseline branch preserves** the orphan as "operator-owned" (lines 489-494). When the source bug overwrote a repo's manifest without reconciling, the husks lost their baseline entry → preserved → never pruned. Root cause of the husks.
- `.new` sidecars are written by `StageSidecar` (the `managed-replace-preserve-live-config` policy + brownfield pre-existing content) — the intended customization-preservation mechanism, reported today via `DotiInstallResult.Preserved`.

## Decisions

### D1 — Bundled-payload source resolution (FR-001/002/003)
New `BundledPayloadResolver` in `Hx.Doti.Core`: `Resolve()` returns `AppContext.BaseDirectory` when `<BaseDirectory>/.doti/core/skills.json` exists (the global-tool's bundled payload sits beside the executable — the same root whose `…/.doti/core/prerequisites.json` is `hx version`'s `manifestPath`), else `null`. The three CLI handlers resolve `source = BundledPayloadResolver.Resolve() ?? FindDotiSource(CWD)` and **fail closed** with a new coded error (`Validation_DotiPayloadSourceUnresolved`) when both are null.
- **Rationale:** the bundled payload is the correct source for the global-tool use case (the dominant one); `FindDotiSource(CWD)` remains the dev/in-repo fallback (a `dotnet run` from source has a bin `BaseDirectory` with no `.doti`, so it falls through to the repo's `.doti`). Fixing the source fixes FR-003 by construction: `Install` reads the bundled `payload.manifest.json` → stamps the real version → `before≠after` → honest `Updated`.
- **Alternatives rejected:** parsing `hx version` JSON for `manifestPath` (indirect + a process hop; `AppContext.BaseDirectory` is the direct, in-process truth). Threading `installedToolVersion` into `Install` as the stamp source (it already reads the bundled descriptor; once the source is the bundled payload that descriptor is correct — no extra thread needed, though `DotiUpdater` already passes it for the relation calc).

### D2 — Prune renamed/removed orphan skill dirs (FR-004)
In `DotiInstaller.PruneOneOrphanDir`, change the **no-baseline** branch (currently "preserved operator-owned") so a `*doti-*` rendered skill-dir orphan is **pruned** (reported `removed`, reason "orphaned managed Doti skill dir the render no longer targets; rendered skills are not operator-owned"). The dirs reaching `PruneOneOrphanDir` are already filtered to `*doti-*` under an agent `SkillsRoot` and absent from the current render (`PruneOrphanedManagedSkillDirs`), so they are doti-rendered debris by construction.
- **Rationale:** rendered doti skills are NOT operator content — "hand-edits are drift" (agent context). An operator's real customizations live in `skills.json`/the constitution (policy-preserved, untouched). The current conservative no-baseline=preserve is what let the husks survive when the source bug erased their baseline. Pruning them is the "old doti not left behind" the operator wants.
- **Alternatives rejected:** requiring `--force` (defeats the self-contained goal — the operator should not need a flag to clean rendered debris); content-matching against old payload versions (the tool doesn't carry historical payloads). The baseline-clean branch is unchanged; `--force` still removes operator-edited orphans.

### D3 — `.new` sidecar accuracy (FR-005/006)
`StageSidecar` gains a byte-equality guard: skip the `.new` when the bundled content is byte-identical to the operator's file (no spurious stray). The `.new` sidecars the reconcile DID stage are surfaced as a distinct **merge-pending** list on the result (today they're folded into `Preserved`), and the D4 commit **excludes** every `*.new` path.
- **Rationale:** the `.new` is the intended merge-helper, not debris; it must be visible (so the operator merges) and never committed as a managed asset.

### D4 — Self-owned sanctioned reconcile commit (FR-007/008/009/010/011)
New `DotiReconcileCommit` in `Hx.Doti.Core`: given the repo root + the reconcile's touched paths, when the repo is a Git work tree and `--commit` is on (default), it stages **exactly** those paths (`DotiInstallResult.Installed` ∪ `Removed` ∪ render `Written`, MINUS `*.new` sidecars and MINUS gitignored runtime state) and runs a commit the coded path **owns** — it sets `DOTI_SANCTIONED_COMMIT=1` in the child `git commit` environment so the insurance hook permits it — with an auto message (`chore(doti): reconcile Doti assets to <after>` + the `before→after` line + the pruned-orphan summary). No staged change → no commit (idempotent). Non-Git target → skip (parity with the hook). The commit sha / skip-reason is reported on the result; the CLI gains a `--no-commit` flag.
- **Rationale:** the reconcile owns its commit exactly as workflow transitions and the release path already do; precise path staging (never `git add -A`) guarantees the operator's unrelated work is never touched. `DOTI_SANCTIONED_COMMIT=1` is the existing, hook-honored sanction signal (verified this session).
- **Alternatives rejected:** `git add -A` (stages operator work — explicitly forbidden); a separate `hx doti commit` command (the operator wants it automatic, not another step); a `prepare-commit-msg`-style hook (commits aren't agent-authored here — the coded path is).

### D5 — Result envelope (FR-011)
Extend `DotiUpdateOutcome` / the install render with: the resolved source origin (`bundled`/`dev-cwd`), the pruned-orphan paths (already in `Changes`/`Removed`), the merge-pending `.new` list (D3), and the commit outcome (sha or skip reason). Surfaced in the `--json` envelope and the human summary. `DotiBatchUpdater` carries them per repo.

### D6 — Docs (FR-012)
Update the README "doti update/install" description, re-render `.doti/agent-context.md` from `.doti/core/skills.json`, and the `update`/`install` command descriptions (`RunnerCommandFactory.Doti.cs`) for the bundled-source default, pruning, and the auto-commit + `--no-commit`.

### D7 — Bug-cycle RCA discipline (FR-013/014/015, doti-prose)
Edit the source-of-truth, then re-render:
- `.doti/core/skills.json` — the `/doti-bug` skill body: add that the mini-cycle requires a **proper RCA** (reproduce → root cause → validate) and a **root-cause** fix, and explicitly forbids bandaid patches and bandaid-vs-root options/asking.
- `extensions/bug/commands/speckit.bug.assess.md` — assess must produce a reproduce-and-root-cause diagnosis with evidence (not a surface verdict).
- `extensions/bug/commands/speckit.bug.fix.md` — fix targets the root cause; a symptom/bandaid/workaround is forbidden; the agent DOES the fix and does NOT present options or ask which fix to apply (surface only a genuine blocker).
- `extensions/bug/commands/speckit.bug.test.md` — unchanged intent (honest verification), reinforced to verify the root cause is gone (not just the symptom masked).
- Re-render skills + agent context; `doti render-skills --check` + `doti payload check` must pass (source→rendered parity).
- **Rationale:** makes the agent-context engineering discipline ("Root-cause, don't patch symptoms") explicit and enforced where bugs are handled. Prose-only; no code path.

### D8 — Doc-stamp scope consistency (FR-016, resolves #42)
`CycleService.ValidateTransitionReadiness` ([TransitionReadiness.cs:14](tools/Hx.Cycle.Core/CycleService.TransitionReadiness.cs)) unions the active feature's owned produces paths (`FeatureArtifactScope.OwnedPaths(_stageModel, state.Feature)`) into its `excluded` set — the SAME set `Check()` already excludes ([CycleService.Check.cs:112](tools/Hx.Cycle.Core/CycleService.Check.cs)) — so the untracked-changes (line 66) and unstaged-tracked (line 61) guards no longer flag the cycle's own ahead-authored produces docs. `ValidateDocStageScope` is unchanged: staging a path other than the current stage's produces still errors, and a genuinely foreign untracked/modified path still blocks.
- **Rationale:** the inconsistency between `Check()` (excludes owned paths) and `ValidateTransitionReadiness` (didn't) IS the entire doc-dance; one `UnionWith` closes it without weakening the gate.
- **Alternatives rejected:** a `--allow-untracked` flag (weakens the scope guard for everyone); excluding only the *target* stage's produces (#42's narrow form — but authoring multiple docs ahead, or the incoming-feature case, still trips; excluding ALL the feature's owned produces is the general fix `Check()` already uses).
- **Dogfooded:** implemented + built first, then used for this very cycle's remaining stamps — the proof the doc-dance is gone is that no set-aside dance is needed for arch-review/tasks/analyze.

## Architecture / Sentrux / hygiene delta

- **New `*.Core` types:** `BundledPayloadResolver`, `DotiReconcileCommit` (both `Hx.Doti.Core`). No new project edge (`Hx.Runner.Cli` already depends on `Hx.Doti.Core`). `DotiReconcileCommit` shells `git` via the existing `Hx.Runner.Core.Process` runner (already referenced).
- **CLI:** `RunnerCommands.Doti.{Install,Update,UpdateAll}.cs` swap the source resolution + invoke the commit step; `RunnerCommandFactory.Doti.cs` adds `--no-commit`. Bodies stay parse→delegate→render.
- **Sentrux:** the prune change is a branch edit (no size growth); `DotiReconcileCommit`/`BundledPayloadResolver` are small, single-responsibility. No layer/cycle violation; keep `Install` within the function-size budget (the commit lives in the new type, not inlined).
- **Error code:** register `Validation_DotiPayloadSourceUnresolved` in `errorcodes/registry.json` (append-only).
- **Tests:** `test/Hx.Doti.Tests` (resolver, prune-no-baseline, `.new` guard, commit precision/idempotence/non-git) + `test/Hx.Runner.Tests` (CLI source default, `--no-commit`, the bug-prose content checks). Tests are Sentrux-excluded.
- **Doti-prose (D7):** edits to `.doti/core/skills.json` + `extensions/bug/commands/*` + re-rendered outputs; no code.
