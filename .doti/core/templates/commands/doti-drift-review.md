# doti-drift-review

Purpose: the last **consistency** gate before release. A green `gate run` proves the code is internally sound; drift-review proves the code still **agrees with everything around it** — what was promised (the **spec**), what is claimed (the **docs**), and what ships (the **installed/rendered assets**). This is where *stale truth* is caught: a mechanism removed from the code but still described in the README, a requirement quietly downgraded from enforced to advisory, a command renamed but not re-documented.

Command-aware advisory behavior.

1. Read `.doti/agent-context.md`, the release train's spec(s)/plan(s), and the arch-review record (`docs/reviews/<NNN-slug>-arch-review.md`).
2. **Scope to the implementation diff — don't scan blind.** Resolve the cycle base (`baseRef` in `.doti/cycle-state.json` — the commit *before* implementation began) and get the **status-rich change set as data** (FR-010): `hx impact plan --for change-context --base <baseRef> --json` emits `data.files` — each changed path with its **status** (Added / Modified / Deleted / Renamed + the rename old-path) and `data.affectedSourceProjects`. **Every axis works from THIS change set:** only what changed this cycle can have drifted. The removed/renamed-symbol worklist (Axis 2) is now data-driven — the `Deleted`/`Renamed` entries ARE the symbols that must survive in no doc/skill/help. The full diff *content* (hunk text) is still agent-run `git diff <baseRef>..HEAD` (+ `git diff HEAD` / `git status --porcelain` for the working tree) where a hunk-level read is needed. Fallback if the planner cannot run: `git diff --name-status <baseRef>..HEAD` + `git status --porcelain`.
3. **Triage the diff:** does it touch production code, contracts, **generated-code templates** (`scaffold/templates/**` — code that becomes a real repo; treat as production code, not prose), docs, or only **Doti prose** (command templates under `.doti/core/templates/commands/`, skills, agent-context)? Run only the axes the diff touches — a *Doti-prose / docs-only* diff skips Axis 1's design checks, but a `scaffold/templates/**` diff runs Axis 1 like any code change. If you can spawn sub-agents, run each applicable axis as its own clean-context sub-agent IN PARALLEL, handing each the diff.

## Axis 1 — spec ↔ code (was it built as specified?)

Read the diff's **code hunks** against the spec they were meant to satisfy. Look for:

- A requirement the spec said MUST be **enforced** (fail-closed) that the diff implemented as advisory, or skipped.
- A hunk that drifts from the approved **arch-review design** / the plan's **architecture delta** — logic that belongs in `*.Core` added to a `*.Cli`; a contract changed in a breaking way the design said keep additive; a gate left advisory that should fail closed; an abstraction the design said to avoid.
- A requirement with **no covering code** — run `hx doti converge --spec docs/specs/<NNN-slug>.md --tasks docs/tasks/<NNN-slug>-tasks.md` for the deterministic spec→tasks coverage gap (this one is *not* diff-bound: a requirement can be uncovered with no diff at all), then spot-check that each covering change actually does what the `FR` says.

Findings: each `FR-###`/`SC-###` whose implementation drifted from the spec, with `file:line` + the spec ref.

**The spec is the source of truth — fix the CODE, in THIS cycle.** When the implementation drifted from the spec, the resolution is ALWAYS to correct the **code** so it satisfies the spec, here and now. **NEVER** amend, downgrade, soften, or "reconcile" the spec to match what the code happens to do, and **NEVER** defer the gap to a follow-up cycle, a memory, or a "known-issue" note — that hollows out the entire gate. Drift-review exists to find implementation gaps and close them in-cycle; an `FR`/`SC` the code failed to honor (an enforced MUST left inert or advisory, a mechanism stubbed, a guarantee not wired) is a **bug to fix now**, not a spec to rewrite and not a ticket to punt. Do not even present "change the spec / defer it" as an option. (The only path that legitimately changes a requirement is an explicit operator-initiated `/01-doti-specify` decision that the requirement itself is wrong — never a silent drift "fix" to unblock a stamp.)

## Axis 2 — code ↔ docs (do the docs still tell the truth?)

The diff IS the worklist — a doc only drifts when the code it describes changed. From the diff:

- **For each code change**, find the doc(s) that describe that behavior — `README.md`, `CHANGELOG.md`, `packaging/*`, the agent context (`CLAUDE.md`/`AGENTS.md`/`.doti/agent-context.md` + each rendered skill's *Command availability*), `hx describe`/`--help` — and confirm they changed to match. **A code change with no corresponding doc change is the prime drift suspect.**
- **For each `Deleted` or `Renamed` entry in the change context** (`data.files` from step 2 — a dropped dependency, a deleted command, a retired mechanism; for a rename, the old-path/old name), grep the whole repo for the old name: it must survive in NO doc, agent-context line, skill, or help string. (This is the stale-`Velopack` class of miss — now a data-driven worklist, not a manual `-`-line scan.)
- **For each doc change in the diff**, confirm it matches the code it describes — the doc was updated to the truth, not to a *different* fiction.

Source-of-truth note: the agent context + skills are *rendered* from `.doti/core/` + `.doti/profiles/` — fix the source and re-render, never hand-edit the installed file. Findings: each stale/wrong/undocumented claim, with the doc `file:line` and the contradicting code.

## Axis 3 — source ↔ installed/rendered assets

4. Run **both** parity authorities — they check different things (and both are independent of the diff): `doti render-skills --check` re-derives the rendered skills + agent-context + thin entrypoints from source (rendered-file freshness), and `doti payload check --repo .` verifies the full installed `.doti` payload — the static `.doti` assets **plus** the rendered files — against source. Both are also enforced by `gate run` (step 6), but cite them here so the manual review is not under-scoped.
5. `AGENTS.md` and `CLAUDE.md` remain thin entrypoints; a hand-edited installed skill is drift — re-render from `.doti/core/skills.json`, never hand-edit the generated file.

## Drift patch loop (FR-012) — fix → re-prove → refresh → re-check → stamp

A drift finding is fixed **in this cycle** and then re-proven — never filed as a note. The loop:

1. **Fix the source of truth** for the finding's axis: Axis 1 spec↔code → fix the **code** (never the spec, never a deferral); Axis 2 code↔docs → fix the **doc**; Axis 3 source↔installed → fix `.doti/core/**` and re-render.
2. **Re-run the proof the fix invalidated.** The gate proof is change-set-bound, so a code edit voids the prior `gate run` — re-run `gate run --profile normal --repo .`. A doc/asset fix re-runs `doti render-skills --check` + `doti payload check --repo .`.
3. **Refresh the cycle state the fix staled.** A code fix moves the change-set identity and stales the `implement` proof; a runner/binding-only staleness is recovered with `doti cycle refresh --target drift-review --apply-safe`, which re-stamps ONLY the safe-to-reinterpret stages and **refuses** the rest (`doti cycle refresh-plan --target drift-review` previews safe vs needs-a-real-re-run). A genuine **input** change is never safe-refreshable — re-run that stage's command. Refresh never rewrites a guarantee to make staleness disappear.
4. **Re-run the affected axes on the NEW diff.** A fix can introduce fresh drift (a code fix may now contradict a doc; a re-render may move an asset). Stamp only when every applicable axis is clean on the final diff.

## Gate + hand-off

6. Run `gate run --profile normal --repo .`; any gate failure is blocking. Direct `dotnet test` output is diagnostic only — the persisted gate proof is the commit-authorizing evidence.
7. On a clean review (no open drift in any *applicable* axis), stamp the stage: `doti cycle stamp --stage drift-review`. The stamp is **fail-closed** — it refuses if any transitive prerequisite is stale or missing, so a clean stamp IS the readiness proof (there is no separate `commit` stage; the commit is owned by the coded transition into `/09-doti-release`). **Open drift blocks the stamp** — fix the source of truth and re-run; do not stamp over known drift. For a **spec↔code** gap (Axis 1) the source of truth is the **spec**, so the fix is ALWAYS the **code** — never the spec, and never a deferral; for a **code↔docs** (Axis 2) or **source↔installed** (Axis 3) gap the fix is the stale doc/asset (or re-render). Resolving spec↔code drift by editing the spec, or by punting the implementation gap to a later cycle, is prohibited.

Expected output: drift findings grouped by axis (spec↔code, code↔docs, source↔installed), each with evidence (`file:line` + the contradicting spec/doc/source ref) and the authoritative fix; plus which axes **ran** vs. **skipped (not applicable, + reason)**. A review where every applicable axis is clean is the pass.

## Next

Run `/09-doti-release` to release, or `/01-doti-specify` to add another feature to this release train.
