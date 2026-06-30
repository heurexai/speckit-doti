# 031 — Spec: self-contained `hx doti update`/`install`

## Goal

Make `hx doti install` / `update` / `update-all` **self-contained**: an operator or agent runs **one command from any directory** and the target repo's managed Doti assets are correctly reconciled to the installed tool's payload, orphaned assets are pruned, and the change is committed — with **no manual workarounds**. This eliminates the three steps required to update repos to 0.17.0 this cycle: (1) `cd` to the tool's bundled-payload dir because the source resolved from the working directory, (2) `rm` the orphaned pre-026 skill dirs because the reconcile never pruned renamed assets, and (3) `DOTI_SANCTIONED_COMMIT=1` + hand-staging only the doti paths because the reconcile left the change uncommitted and the insurance hook blocks bare commits.

This cycle ALSO hardens the **bug mini-cycle's instructions** (`/doti-bug` + the assess/fix/test command templates): they must direct the agent to conduct a proper root-cause analysis and implement a **root-cause** fix — never a symptom/bandaid patch — and must forbid presenting bandaid-vs-root options or asking the operator to choose a fix approach.

## Priority Mode

**Workflow / tooling change → fail-closed safety + deterministic proof before ergonomics.** The build order is: a **correct, fail-closed source + version** (P1) before a **complete reconcile** (P2) before the **self-committing ergonomics** (P3). A wrong source or an incomplete prune that is then auto-committed would bake a defect into history, so correctness precedes the commit by construction.

This is a **mixed change**. The **dominant** mode is workflow/tooling (P1–P3 — the `doti update`/`install` code path). The bundled **bug-cycle instruction hardening** (P4) is a **doti-prose** sub-change (authoritative-guidance correctness first), whose only artifacts are the `/doti-bug` skill source + the bug command templates and their rendered outputs.

## Work items (by priority)

### P1 — Correct, fail-closed source + version (the foundation)
The reconcile must read the right payload and stamp the true version, or fail closed. Today `doti install`/`update`/`update-all` resolve the source via `FindDotiSource(Directory.GetCurrentDirectory())` ([tools/Hx.Runner.Cli/RunnerCommands.Doti.cs:19](tools/Hx.Runner.Cli/RunnerCommands.Doti.cs)) with no bundled fallback — so from a dev checkout they stamp the wrong source, and from a neutral dir they error "Could not locate .doti/core/skills.json".
- **Independent check:** `hx doti update --repo <r>` from a neutral dir reconciles to the bundled tool version with no "Could not locate" error.

### P2 — Complete reconcile (no old doti left behind)
The reconcile must leave the repo byte-matching the bundled manifest — no orphaned managed assets from cross-version renames/removals, no staging strays. Today a rename (the 026 skill reorder: `04-doti-tasks`→`05-doti-tasks`, `06-doti-arch-review`→`04-doti-arch-review`, `05-doti-analyze`→`06-doti-analyze`) leaves the old-named dirs as orphans in both `.claude/skills` and `.agents/skills`, which `doti payload check` flags as drift.
- **Independent check:** updating a repo carrying renamed orphans removes them; `doti payload check` passes afterward.

### P3 — Self-committing ergonomics (the capstone)
After a correct, complete reconcile in a Git repo, the command commits exactly what it touched — so no manual sanctioned-commit wrangling.
- **Independent check:** after `hx doti update` in a Git repo the doti-only change is committed; the operator's unrelated work is untouched.

### P4 — Bug-cycle RCA discipline (doti-prose)
The `/doti-bug` mini-cycle's authoritative instructions must direct the agent to root-cause and root-fix — never bandaid — and never offer the operator a bandaid-vs-root choice. Today the bug-cycle prose enforces the assess→fix→test proof chain (assess read-only, fix bound to a confirmed assessment, honest test) but does NOT explicitly require an RCA, forbid a symptom patch, or forbid bandaid options — so an agent can record a confirmed assessment and a bandaid, or stop to ask the operator which fix to apply.
- **Independent check:** the rendered `/doti-bug` skill + the assess/fix command templates contain explicit RCA + root-fix-not-bandaid + no-bandaid-options instructions; `doti render-skills --check` passes.

### P5 — Doc-stamp scope consistency (cycle-stamp engine; resolves #42)
The cycle-stamp transition must not treat the cycle's OWN produces docs as foreign changes. Today `Check()` excludes the feature's owned produces paths (`FeatureArtifactScope.OwnedPaths`) but `ValidateTransitionReadiness` does not, so authoring the next stage's doc before stamping the transition into it trips "untracked changes present" (or "staged path unrelated" if the agent stages it) — forcing a non-obvious set-aside/stage/restore dance that agents repeatedly get wrong (hit 3× while driving this very cycle).
- **Independent check:** author a stage's produces doc ahead, stamp the transition into that stage → no trip, no dance; a foreign untracked file present at the same time STILL blocks.

## Scope

**Included:** the three coupled fixes (source resolution, orphan/stray pruning, self-commit) across `doti install`, `doti update`, and `doti update-all`; their result-envelope reporting; the docs; (P4) the **bug-cycle instruction hardening** — the `/doti-bug` skill + assess/fix/test command templates require an RCA + a root-cause fix and forbid bandaid options; and (P5) the **doc-stamp scope-consistency fix** — `ValidateTransitionReadiness` excludes the feature's owned produces docs (resolves #42).

**Excluded:**
- The cycle-stamp / workflow-transition commit model is unchanged — only the **asset-reconcile path** gains a commit it owns.
- **Customization merge is out of scope:** when an operator has customized `.doti/core/skills.json` (or any managed asset), the reconcile preserves their version and removes the pristine staging stray, but does **not** auto-merge their customizations with the new base — that remains a manual operator step.
- Source-developer in-repo flows keep the working-directory (`FindDotiSource`) resolution; only the **global-tool** invocation changes its default.

## Functional requirements

**P1 — source + version**
- **FR-001** The global-tool `doti install`/`update`/`update-all` MUST default the payload source to the **running tool's bundled payload** (derived from the tool's own manifest path — `hx version` `data.prerequisites.manifestPath` with the trailing `/.doti/core/prerequisites.json` stripped), not the current working directory.
- **FR-002** They MUST fall back to a working-directory walk (`FindDotiSource`) **only** for a genuine in-repo/dev source, and MUST **fail closed** with a clear, coded error when no valid version-stamped payload can be resolved — never silently stamp from a non-payload source nor report a false `already-current`.
- **FR-003** The reconcile MUST stamp the target's `.doti/payload.json` to the bundled tool's version, thread `installedToolVersion` into the stamp, and report the true `before`→`after` — an `outdated` repo that reconciles MUST NOT report `already-current`.

**P2 — complete reconcile**
- **FR-004** The reconcile MUST **remove managed assets** present in the target repo but **absent from the target (bundled) manifest** (orphans from cross-version renames/removals), so the post-update repo matches the bundled manifest, and MUST report the pruned paths.
- **FR-005** Operator customizations to managed assets carrying a sanctioned **preservation policy** (e.g. `.doti/core/skills.json` under `managed-replace-preserve-live-config`; the constitution under write-once) MUST be preserved — never overwritten or pruned unless `--force`. Orphaned **rendered skill dirs** the render no longer targets are NOT operator content (hand-edits to rendered skills are drift, per the agent context) and are pruned per FR-004; operator work in policy-preserved assets is never deleted silently.
- **FR-006** When a customization is preserved, the bundled version staged as a `.new` sidecar (the existing preservation mechanism) MUST be **reported** in the result as a distinct merge-pending item (not silent untracked debris) and MUST be **excluded** from the auto-commit (a `.new` is an operator merge-helper, not a managed asset); and a `.new` MUST NOT be written when the operator's content is byte-identical to the bundled version (no spurious stray).

**P3 — self-commit**
- **FR-007** After a **successful** reconcile in a **Git** repo, the command MUST make a single commit it **owns** — a sanctioned commit permitted past the insurance pre-commit hook by a coded path, consistent with the constitution's "commits are owned by coded Doti workflow transitions and release paths" — with an auto-generated message naming the `before`→`after` version and pruned orphans.
- **FR-008** The commit MUST stage **exactly the managed-asset paths the reconcile touched** (installed/updated/rendered/pruned — from the reconcile result), **never `git add -A`**; the operator's unrelated working-tree changes MUST NOT be staged or committed.
- **FR-009** The auto-commit MUST be **default-on**; a `--no-commit` flag MUST opt out (leaving the reconciled changes in the working tree); a **non-Git** target MUST reconcile and **skip** the commit without erroring (parity with the insurance hook's non-git skip).
- **FR-010** A no-change reconcile (`already-current`) MUST make **no commit** (no empty commit); re-running MUST be a no-op.

**Cross-cutting**
- **FR-011** The resolved source, pruned paths, and commit outcome (the commit sha, or the `--no-commit`/non-git skip reason) MUST be reported in the command's result envelope, following the command→`CliResult`→error-code convention.
- **FR-012** The README, `.doti/agent-context.md` (re-rendered from `.doti/core/skills.json`), and `hx doti update`/`install` `--help`/`describe` MUST describe the self-contained behavior (bundled-source default, pruning, the auto-commit + `--no-commit`).

**P4 — bug-cycle RCA discipline (doti-prose)**
- **FR-013** The `/doti-bug` skill source (`.doti/core/skills.json`) and the bug command templates (`extensions/bug/commands/speckit.bug.assess.md`, `…fix.md`, `…test.md`, and `.doti/core/templates/commands/doti-bug.md`) MUST instruct the agent to conduct a **proper root-cause analysis** in the assess stage — reproduce the bug, identify the ROOT cause (not the symptom), and validate the diagnosis with evidence — before any fix.
- **FR-014** The fix-stage instructions MUST require fixing the **root cause**, and MUST explicitly forbid a symptom patch / bandaid / workaround.
- **FR-015** The bug-cycle instructions MUST direct the agent to DO the root fix, and MUST explicitly forbid presenting bandaid-vs-root options or asking the operator to choose a fix approach; the agent surfaces to the operator ONLY a genuine blocker (a real design decision it cannot make at ≥95% confidence, missing access, or an unverifiable premise), never a bandaid alternative. This makes the existing agent-context engineering discipline ("Root-cause, don't patch symptoms") explicit and enforced at the bug-cycle, where it matters most.

**P5 — doc-stamp scope consistency (cycle-stamp engine)**
- **FR-016** `CycleService.ValidateTransitionReadiness` MUST exclude the active feature's owned produces-doc paths (`FeatureArtifactScope.OwnedPaths(_stageModel, feature)`) from its untracked-changes and unstaged-tracked scope checks, mirroring `Check()` — so authoring a stage's produces doc ahead of stamping the transition into that stage never trips "untracked changes present" or "staged path … unrelated". A genuinely foreign untracked or modified path (NOT one of the feature's produces docs) MUST still block the transition (fail-closed scope safety preserved). Resolves issue #42.

## Success criteria

- **SC-001** `hx doti update --repo <r>` from a neutral dir (no `.doti` ancestor) reconciles to the bundled tool version — no "Could not locate .doti/core/skills.json" error.
- **SC-002** `hx doti update --repo <r>` from a Doti dev checkout stamps the **bundled** tool version (not the checkout's `.doti`).
- **SC-003** An outdated repo that reconciles reports `before` < `after` (never `already-current` when it moved).
- **SC-004** Updating a repo carrying renamed orphans (e.g. pre-026 skill dirs) removes them; `doti payload check --repo <r>` passes afterward.
- **SC-005** A `.new` sidecar is written ONLY when the operator's version genuinely differs from the bundled (no spurious stray when they match); when written it is reported as merge-pending and excluded from the auto-commit.
- **SC-006** An operator-customized managed asset is preserved across the reconcile (not pruned) unless `--force`.
- **SC-007** After `hx doti update` in a Git repo, the reconciled doti-only change is committed in one commit, and `git status` shows the operator's unrelated changes still present and uncommitted.
- **SC-008** The committed path set equals the reconcile's touched path set — no operator files, no `git add -A`.
- **SC-009** `hx doti update --no-commit` leaves the reconciled changes uncommitted in the working tree.
- **SC-010** `hx doti update` on a non-Git target reconciles and skips the commit with no error.
- **SC-011** A no-change `hx doti update` produces no commit.
- **SC-012** `hx doti update-all` applies the same source/prune/commit behavior per repo, fail-soft, with a per-repo summary.
- **SC-013** The rendered `/doti-bug` skill and the assess/fix command templates contain explicit instructions to (a) conduct a reproduce-and-root-cause RCA, (b) fix the root cause not a bandaid, and (c) not present bandaid-vs-root options — verifiable by content inspection.
- **SC-014** `hx doti render-skills --check` and `hx doti payload check` pass after the bug-cycle prose change (source-of-truth → rendered-asset parity holds).
- **SC-015** Authoring the next stage's produces doc (e.g. the plan doc) and then stamping the transition into that stage succeeds with NO set-aside/stage/restore dance; a foreign untracked file present at the same time STILL blocks the transition (fail-closed scope preserved).

## Key entities / data

- **Bundled payload source** — the version-stamped `.doti` payload shipped beside the installed tool (the manifest-path root); the new default source.
- **Reconcile result** — the existing install/update result extended to carry the resolved source origin, the pruned-orphan paths, and the commit outcome (sha / skip reason).
- **Managed-asset manifest** (`.doti/managed-assets.json`) — the canonical baseline diffed to compute orphans to prune.

## Deterministic surfaces

- `hx doti update --repo <p> [--force] [--dry-run] [--no-commit] --json`, `hx doti install --repo <p> [--no-commit] --json`, `hx doti update-all --root <d> [--no-commit] --json` — the commands gaining the behavior (a new `--no-commit` flag).
- `hx doti payload check --repo <p> --json` — the post-update parity authority (SC-004).
- The `DotiUpdateResult` / install-result envelope fields for source/pruned/commit (FR-011).

## Architecture / Sentrux / hygiene impact

- Logic lives in `*.Core`: a **bundled-source resolver** + the prune/commit steps in `DotiInstaller`/`DotiUpdater` (`Hx.Doti.Core`); the CLI (`RunnerCommands.Doti`) stays parse→delegate→render and only adds the `--no-commit` flag wiring.
- The self-commit is a **coded path that owns its commit** — it sets the sanctioned-commit signal the insurance hook honors, mirroring how workflow transitions and the release path already commit. No new project dependency edge; no Sentrux layer/cycle violation (extract helpers to stay within function-size limits).
- A new error code for unresolved-source fail-closed (FR-002) registered in `errorcodes/registry.json`.
- Tests live under `test/` (excluded from Sentrux's production barometer).

## Clarifications

1. **Auto-commit is default-on** (with `--no-commit` to opt out), not opt-in — the operator's goal is a wrangle-free single command (operator-confirmed).
2. **The commit is owned by the command** as a sanctioned coded path, extending "commits are owned by coded Doti workflow transitions and release paths" to the asset-reconcile path (operator-confirmed).
3. **Scope is all three coupled fixes** (source + prune + commit) in one cycle — they are one code path and one goal; committing a wrong/incomplete reconcile would bake in a defect (operator-confirmed: "A, all 3").
4. **Pruning preserves operator-customized managed assets** — only un-customized orphans are pruned (unless `--force`); operator work is never deleted silently.
5. **The bug-cycle RCA/root-fix instruction hardening (P4) is bundled into 031** (operator-directed) — a doti-prose sub-change to the `/doti-bug` skill + bug command templates, shipped with the update-path code fixes because both harden the same self-hosted Doti workflow.
6. **The doc-stamp scope-consistency fix (P5, resolves #42) is bundled into 031** (operator-directed) — a small `Hx.Cycle.Core` change so the cycle-stamp transition stops treating the cycle's own produces docs as foreign changes; same "agent-proof Doti workflow" theme as P1–P4. The operator hit this repeatedly (agents tripping the doc-dance when authoring docs ahead of stamping); the root fix prevents it for every future cycle.

## Assumptions

- The bundled payload is version-stamped and resolvable from the tool's manifest path — **verified this cycle**: the bundled-dir workaround (`…/heurex.speckitdoti/<ver>/…/net10.0/any/.doti`) reconciled correctly when run from there.
- The insurance pre-commit hook honors `DOTI_SANCTIONED_COMMIT=1` — **verified this cycle** on ergon's hook (`if [ "$DOTI_SANCTIONED_COMMIT" = "1" ]; then exit 0; fi`).
- `hx doti update`/`install` already track installed/updated/rendered/removed paths in their result — the commit reuses that set (to confirm at `/03-plan` by reading the result types).

## Command availability

All referenced commands (`doti install`/`update`/`update-all`/`payload check`/`scan`, `gate run`, `version`) are implemented today; the `--no-commit` flag and the source/prune/commit behavior are the new surface this feature adds.
