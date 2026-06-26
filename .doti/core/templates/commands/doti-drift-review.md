# doti-drift-review

Purpose: the last **consistency** gate before release. A green `gate run` proves the code is internally sound; drift-review proves the code still **agrees with everything around it** — what was promised (the **spec**), what is claimed (the **docs**), and what ships (the **installed/rendered assets**). This is where *stale truth* is caught: a mechanism removed from the code but still described in the README, a requirement quietly downgraded from enforced to advisory, a command renamed but not re-documented.

Command-aware advisory behavior.

1. Read `.doti/agent-context.md`, the release train's spec(s)/plan(s), and the arch-review record (`docs/reviews/<NNN-slug>-arch-review.md`).
2. **Scope to the implementation diff — don't scan blind.** Resolve the cycle base (`baseRef` in `.doti/cycle-state.json` — the commit *before* implementation began) and take the whole **before→after** change set: `git diff <baseRef>..HEAD` for the committed work, plus `git diff HEAD` / `git status --porcelain` for the working tree. **Every axis works from THIS diff:** only what changed this cycle can have drifted, so start from the changed code/docs/spec, never the whole repo. (`hx impact plan --for arch-review --json` already gives the changed-file list + affected projects; the full diff *content* is agent-run `git diff` for now — a future cycle folds the diff into `hx` as a first-class command, the same way T040 added the arch-review changed-files context.)
3. **Triage the diff:** does it touch production code, contracts, **generated-code templates** (`scaffold/templates/**` — code that becomes a real repo; treat as production code, not prose), docs, or only **Doti prose** (command templates under `.doti/core/templates/commands/`, skills, agent-context)? Run only the axes the diff touches — a *Doti-prose / docs-only* diff skips Axis 1's design checks, but a `scaffold/templates/**` diff runs Axis 1 like any code change. If you can spawn sub-agents, run each applicable axis as its own clean-context sub-agent IN PARALLEL, handing each the diff.

## Axis 1 — spec ↔ code (was it built as specified?)

Read the diff's **code hunks** against the spec they were meant to satisfy. Look for:

- A requirement the spec said MUST be **enforced** (fail-closed) that the diff implemented as advisory, or skipped.
- A hunk that drifts from the approved **arch-review design** / the plan's **architecture delta** — logic that belongs in `*.Core` added to a `*.Cli`; a contract changed in a breaking way the design said keep additive; a gate left advisory that should fail closed; an abstraction the design said to avoid.
- A requirement with **no covering code** — run `hx doti converge --spec docs/specs/<NNN-slug>.md --tasks docs/tasks/<NNN-slug>-tasks.md` for the deterministic spec→tasks coverage gap (this one is *not* diff-bound: a requirement can be uncovered with no diff at all), then spot-check that each covering change actually does what the `FR` says.

Findings: each `FR-###`/`SC-###` whose implementation drifted from the spec, with `file:line` + the spec ref.

## Axis 2 — code ↔ docs (do the docs still tell the truth?)

The diff IS the worklist — a doc only drifts when the code it describes changed. From the diff:

- **For each code change**, find the doc(s) that describe that behavior — `README.md`, `CHANGELOG.md`, `packaging/*`, the agent context (`CLAUDE.md`/`AGENTS.md`/`.doti/agent-context.md` + each rendered skill's *Command availability*), `hx describe`/`--help` — and confirm they changed to match. **A code change with no corresponding doc change is the prime drift suspect.**
- **For each symbol the diff REMOVED or RENAMED** (a dropped dependency, a deleted command, a retired mechanism — these are the `-` lines in the diff), grep the whole repo for the old name: it must survive in NO doc, agent-context line, skill, or help string. (This is the stale-`Velopack` class of miss.)
- **For each doc change in the diff**, confirm it matches the code it describes — the doc was updated to the truth, not to a *different* fiction.

Source-of-truth note: the agent context + skills are *rendered* from `.doti/core/` + `.doti/profiles/` — fix the source and re-render, never hand-edit the installed file. Findings: each stale/wrong/undocumented claim, with the doc `file:line` and the contradicting code.

## Axis 3 — source ↔ installed/rendered assets

4. `Hx.Runner.Cli doti render-skills --check` is the authority for skill / agent-context / payload-parity drift (independent of the diff — it re-derives every rendered file from source).
5. `AGENTS.md` and `CLAUDE.md` remain thin entrypoints; a hand-edited installed skill is drift — re-render from `.doti/core/skills.json`, never hand-edit the generated file.

## Gate + hand-off

6. Run `gate run --profile normal --repo .`; any gate failure is blocking. Direct `dotnet test` output is diagnostic only — the persisted gate proof is the commit-authorizing evidence.
7. On a clean review (no open drift in any *applicable* axis), stamp the stage (`doti cycle stamp --stage drift-review`), then confirm commit-readiness with `doti cycle check --stage commit` (fail-closed: every prerequisite stamped + fresh). **Open drift blocks the stamp** — fix the source of truth (the code, or whichever doc/asset lied about it) and re-run; do not stamp over known drift.

Expected output: drift findings grouped by axis (spec↔code, code↔docs, source↔installed), each with evidence (`file:line` + the contradicting spec/doc/source ref) and the authoritative fix; plus which axes **ran** vs. **skipped (not applicable, + reason)**. A review where every applicable axis is clean is the pass.

## Next

Run `/09-doti-release` to release, or `/01-doti-specify` to add another feature to this release train.
