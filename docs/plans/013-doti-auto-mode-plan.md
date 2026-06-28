# Plan — 013 Doti Auto Mode

**Spec:** [docs/specs/013-doti-auto-mode.md](../specs/013-doti-auto-mode.md). **Stage:** `/03-doti-plan`.

## Summary

Add an **unnumbered `doti-auto` utility skill** that drives the numbered Doti cycle (`/01`–`/09`) hands-off to a target stage, stopping only at genuine operator-decision points. This is **Doti-prose only** — a new skill entry single-sourced in `.doti/core/skills.json` plus a command template `.doti/core/templates/commands/doti-auto.md`, rendered to `.claude`/`.agents` by `doti render-skills`. No `*.Core` code, no contract, no rule/Sentrux/ArchUnit change. It mirrors the 009 `doti-constitution` skill addition exactly.

## Existing-architecture assessment (required before deciding)

- **The skill substrate is data-driven and single-sourced.** `.doti/core/skills.json` (`schemaVersion`, `skills[]`) is rendered by the Doti renderer to the installed skill files; `CLAUDE.md` states "Installed skills are rendered from one source — edit `.doti/core/skills.json` and re-render; do not hand-edit installed skill files." Each entry is `{name, description, argumentHint, highlights, nextStage}`. The 15 existing skills include unnumbered utilities (`doti-bug`, `doti-amend`, `doti-constitution`, `converge`, `doti-upgrade`) that "run anytime / inside the active cycle" — `doti-auto` is the same shape.
- **Command templates** live under `.doti/core/templates/commands/*.md` (`commandTemplateDir`); each unnumbered utility skill with prose behavior has one (`doti-constitution.md`, `doti-bug.md`, `doti-amend.md`, `doti-upgrade.md`). `doti-auto` needs `doti-auto.md`.
- **The cycle engine is unchanged.** `doti-auto` is orchestration OVER the existing enforced stages (`/01`–`/09`, `doti cycle stamp/check`, `gate run`, `hx release`) — it invokes them and honors their chokepoints; it adds NO new enforcement surface, no numbered stage, no reordering. The workflow stage model (`.doti/core/workflows/doti/workflow.yml`) is untouched.
- **Parity authorities** that gate the change: `doti render-skills --check` (rendered skills/agent-context/entrypoints match source) and `doti payload check --repo .` (installed `.doti` payload parity). Both must stay clean after the skill is added and re-rendered.

## Design

**Decision:** Single-source the new skill in `.doti/core/skills.json` (one `skills[]` entry) + a `.doti/core/templates/commands/doti-auto.md` command template; re-render with `doti render-skills`.

**Rationale:** This is the ONLY pattern for adding a skill — it is how every existing utility skill is defined, it keeps the installed assets render-derived (no hand-edit, so `render-skills --check` stays the single source of truth), and it requires no code because the behavior is agent orchestration over already-enforced commands. Choosing anything else would either break the single-source invariant or introduce a coded "run the whole cycle" driver that bypasses per-stage agent judgment (explicitly OUT of scope per the spec).

**Alternatives rejected:**
- *A coded `doti cycle auto` command that loops stages.* Rejected: it would bypass the per-stage agent judgment the cycle depends on (clarify ambiguity, arch-review lens triage, RCA of a gate failure are not mechanizable into one command), and it would become a new enforcement surface that could weaken the chokepoints. The spec's Scope explicitly excludes it.
- *Hand-editing the installed skill files directly.* Rejected: violates the render-from-one-source invariant (`CLAUDE.md`); `render-skills --check` would flag drift.
- *A numbered stage.* Rejected: `doti-auto` is advisory orchestration, not a cycle stage — it must not reorder `/01`–`/09` (FR-008).

## Architecture delta (enforced, not just described)

- **No ArchUnit family change, no Sentrux boundary change, no contract change.** The skill text is Doti prose (Sentrux source-excluded). The only deterministic surfaces touched are `.doti/core/skills.json` + the new command template, both render inputs.
- **Parity is the enforcement point:** `doti render-skills --check` + `doti payload check` (run inside `gate run --profile normal` at `/07`) prove the new skill rendered correctly and the installed payload matches source. The gate's skill-drift and payload-parity steps are the deterministic proof for this change.

## Constitution Check

- §1 (inherited doti invariants): **PASS** — no gate downgraded, no enforced→advisory weakening; `doti-auto` is explicitly forbidden from weakening any gate or publishing (FR-005/006). Single-source + render preserved.
- §2 (project declarations): **PASS** — no tech-stack/coding-style impact (prose-only).

## Risk

- **Lowest tier.** The blast radius is one skills.json entry + one prose template. The only failure mode is a render/parity miss (caught by `render-skills --check` + `payload check`). The behavioral risk — an auto driver that weakens a gate or publishes unattended — is addressed in the skill TEXT itself (FR-004/005/006 enumerate the stop conditions and the never-weaken/never-publish rules); drift-review verifies the text encodes them.

## Next

`/04-doti-tasks` — break this into executable tasks (author skill entry, author template, render, verify parity).
