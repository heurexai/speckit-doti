# 026 — Drift review: arch-review after plan (cycle stage reorder)

Scope: `git diff <baseRef>..HEAD` for 026 (baseRef = cycle start `37ef7f3`). Triage: a doti-prose + workflow-data change with one small production code edit (`DotiWorkflowRegistry`, a static table) + test updates + re-rendered installed assets. No new runtime logic.

## Axis 1 — spec ↔ code

- **FR-001/002** (new order + fail-closed prereqs): `workflow.yml` declares `specify, clarify, plan, arch-review, tasks, analyze, implement, drift-review, release` with `arch-review←plan`, `tasks←arch-review`, `analyze←tasks`, `implement←analyze`. Verified live: this very cycle re-derived under the new graph (the doc stages re-stamped against their new prerequisites), and `cycle status` reports all stages fresh in the new order. No enforcement downgraded.
- **FR-003/004** (renumber + nextStage): `DotiWorkflowRegistry` ordinals arch-review=4/tasks=5/analyze=6, forward chain and `nextStep` updated; `skills.json` `nextStage` pointers re-flowed (plan→/04, arch-review→/05, tasks→/06, analyze→/07).
- **FR-006** (arch-review body reworded): no longer "before implementation / after analyze"; now "after plan, before tasks/analyze", "hard prereq of /05-doti-tasks".
- **FR-008** (no ID/produces/schema change): stage IDs, `produces` patterns, and `cycle-state.json` schema unchanged — existing 022 proofs remain interpretable. The design matches the approved plan + arch-review record; nothing landed in `*.Cli` that belongs in `*.Core` (the change is data + a `*.Core` static table).

## Axis 2 — code ↔ docs

Every code/data change has a matching doc change: `README.md` cycle description reordered (FR-005); `CHANGELOG.md` `[Unreleased]` entry added (T011); the agent-context / constitution / spec templates + the rendered `.doti/agent-context.md`, `CLAUDE.md`, `AGENTS.md` re-rendered to the new numbering; `errorcodes/registry.json` remediation prose updated → `ErrorCodes.g.cs` regenerated. Removed/renamed symbols: the old `06-doti-arch-review` / `04-doti-tasks` / `05-doti-analyze` slash-command numbers survive in **no** live source (SC-004 grep clean; only CHANGELOG history retains them, correctly).

## Axis 3 — source ↔ installed

`hx doti render-skills --check` → no drift; `hx doti payload check --repo .` → parity passed (93 managed files). The three renamed skill directories replaced the old-numbered ones (no orphans); installed `.doti/workflows/doti/workflow.yml` matches source. Entrypoints are thin; no hand-edited installed skill (source `.doti/core` edited + re-rendered).

## Gate

`hx gate run --profile normal` → **success** (hygiene, gitleaks, affected-change, task-completion, restore-build-test, architecture, no-velopack/no-source, skill-drift, doti-payload, sentrux, version-calculate, security-scan). Full `dotnet test` green (Runner 237, Doti 96, Cycle 67, Scaffold 108, Impact 47, +others).

## Note — self-modifying-cycle bootstrap (transparency)

This cycle changed the cycle definition it runs under. After installing the new graph, the doc stages (tasks/analyze/arch-review) read `prereqArtifactChanged`-stale because their prerequisite *edges* changed (the conservative `rerunRequired` classification), though their artifacts are byte-identical. Resolved by re-stamping those stages under the new graph (the designed "re-run" recovery) — the work itself was unchanged. The implement edits were committed as a sanctioned commit because re-stamping the earlier doc stages required a clean tree (the implement transition can't run with the doc stages mid-re-bind). The change set base..HEAD is the correct final diff; the gate proof is bound to it.

## Verdict

**Clean** across all three applicable axes; gate green. Ready for `/09-doti-release`.
