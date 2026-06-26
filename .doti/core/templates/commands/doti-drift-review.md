# doti-drift-review

Purpose: the last **consistency** gate before release. A green `gate run` proves the code is internally sound; drift-review proves the code still **agrees with everything around it** — what was promised (the **spec**), what is claimed (the **docs**), and what ships (the **installed/rendered assets**). This is where *stale truth* is caught: a mechanism removed from the code but still described in the README, a requirement quietly downgraded from enforced to advisory, a command renamed but not re-documented, a hand-edited installed skill.

Command-aware advisory behavior. **Triage the change first**, then run only the axes it touches (a templates/docs-only change skips Axis 1's design checks; a code-only change still runs all three). **If you can spawn sub-agents, run each applicable axis as its own clean-context sub-agent IN PARALLEL**, handing each the changed-files list (`hx impact plan --for arch-review --json`, or `git diff` fallback) plus the spec/plan/arch-review; otherwise run them yourself, one focused pass per axis.

1. Read `.doti/agent-context.md`, the release train's spec(s)/plan(s), and the arch-review record (`docs/reviews/<NNN-slug>-arch-review.md`).

## Axis 1 — spec ↔ code (was it built as specified?)

For each feature in the release train, walk every `FR-###`/`SC-###` and confirm a **real mechanism in the code** satisfies it — not just a checked task box. Look for:

- A requirement the spec said MUST be **enforced** (fail-closed) that the code only treats as advisory, or skips entirely.
- The approved **arch-review design** / the plan's **architecture delta** implemented differently than approved — logic that belongs in `*.Core` living in a `*.Cli`; a contract that was meant to be additive made breaking; a gate that should fail closed left advisory; an abstraction added that the design said to avoid.
- A requirement with **no covering code at all**: run `hx doti converge --spec docs/specs/<NNN-slug>.md --tasks docs/tasks/<NNN-slug>-tasks.md` for the deterministic spec→tasks coverage gap, then spot-check that each *covering* task's code actually does what the `FR` says (a task box ticked is not proof the behavior exists).

Findings: each `FR-###`/`SC-###` whose implementation drifted from the spec, with `file:line` + the spec ref.

## Axis 2 — code ↔ docs (do the docs still tell the truth?)

Every behavioral claim in a doc MUST match what the code **actually does today**. Check the surfaces that go stale:

- **User docs** — `README.md`, `CHANGELOG.md`, `packaging/*`: every install/release/feature claim matches the shipped commands, flags, and channels.
- **Agent context** — `CLAUDE.md`, `AGENTS.md`, `.doti/agent-context.md`, and each rendered skill's *Command availability* + highlights: every command/flag/mechanism they describe exists and behaves as stated.
- **Command help** — `hx describe --json` + `--help`: option/command descriptions and the surfaced tier/channel match the real surface.

Method: for each behavioral claim, find the code path and confirm it matches; for each command/flag the code exposes, confirm it is documented; and **grep the whole repo for the name of anything you removed or renamed this cycle** — a dropped dependency, a deleted command, a retired mechanism must survive in NO doc, agent-context line, skill, or help string. (Source-of-truth note: the agent context + skills are *rendered* from `.doti/core/`+`.doti/profiles/` — fix the source and re-render, never hand-edit the installed file.) Findings: each stale, wrong, or undocumented claim, with the doc `file:line` and the contradicting code.

## Axis 3 — source ↔ installed/rendered assets

2. `Hx.Runner.Cli doti render-skills --check` is the authority for skill / agent-context / payload-parity drift.
3. `AGENTS.md` and `CLAUDE.md` remain thin entrypoints; a hand-edited installed skill is drift — re-render from `.doti/core/skills.json`, never hand-edit the generated file.

## Gate + hand-off

4. Run `gate run --profile normal --repo .`; any gate failure is blocking. Direct `dotnet test` output is diagnostic only — the persisted gate proof is the commit-authorizing evidence.
5. On a clean review (no open drift in any *applicable* axis), stamp the stage (`doti cycle stamp --stage drift-review`), then confirm commit-readiness with `doti cycle check --stage commit` (fail-closed: every prerequisite stamped + fresh). **Open drift blocks the stamp** — fix the source of truth (the code, or whichever doc/asset lied about it) and re-run; do not stamp over known drift.

Expected output: drift findings grouped by axis (spec↔code, code↔docs, source↔installed), each with evidence (`file:line` + the contradicting spec/doc/source ref) and the authoritative fix; plus which axes **ran** vs. **skipped (not applicable, + reason)**. A review where every applicable axis is clean is the pass.

## Next

Run `/09-doti-release` to release, or `/01-doti-specify` to add another feature to this release train.
