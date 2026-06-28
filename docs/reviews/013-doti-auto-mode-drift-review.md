# Drift Review — Feature 013: Doti Auto Mode

**Stage:** `/08-doti-drift-review`. **Date:** 2026-06-28. Scoped from the implement change set: a new unnumbered `doti-auto` skill (`.doti/core/skills.json` entry + `.doti/core/templates/commands/doti-auto.md`), its rendered output (`.claude`/`.agents` `doti-auto/SKILL.md`), a README mention, and the cycle docs. **Docs/prose-only diff — no `*.cs`, no contract, no rule/Sentrux/ArchUnit surface** (the spec↔code design lenses reduce to "does the prose encode the requirements").

## Axis 1 — spec ↔ code (PASS)

Every FR has a real enforcing surface in the rendered skill text (the only "code" here):

- **FR-001** (unnumbered `doti-auto` skill exists, rendered like the other utilities): the `skills.json` entry renders to `.claude/skills/doti-auto/SKILL.md` + `.agents/skills/doti-auto/SKILL.md` (`render-skills` wrote both; `--check` clean). The skill is registered and invocable.
- **FR-002/003** (default = local release; `--until <stage>` bound): the command template's step 1 resolves the target from `--until`, default `release`; step 2 loops to it.
- **FR-004** (stop at operator-blocking conditions, Operator-Question Protocol): step 3 enumerates the four classes verbatim — clarify ambiguity/`[NEEDS CLARIFICATION]`, arch-review BLOCKER or missing applicable lens, unrecoverable gate failure, the publish boundary, and any genuine blocker incl. <95% confidence.
- **FR-005** (never weaken; spec↔code fixed in CODE): step 4 (95% auto-fix bar, spec↔code gap fixed in code never the spec) + step 5 (never skip a check / downgrade enforced→advisory, stamp every diff-bound proof, honor the chokepoints, commits owned by coded transitions).
- **FR-006** (never publish unattended): step 3 PUBLISH bullet — a `release` run performs only the LOCAL `hx release`; the remote `v*` push is always a separate explicit operator step, surfaced never performed. Consistent with `CLAUDE.md` ("`hx release` does not push tags").
- **FR-007** (honest boundary reporting): step 6.
- **FR-008** (advisory orchestration; no new stage/reorder/chokepoint): step 5 final sentence + the skill description ("orchestration over the existing enforced stages, never a bypass").

Matches the approved plan/arch-review: Doti-prose only, single-sourced + rendered, no coded "run the whole cycle" driver (the rejected alternative). Nothing downgraded enforced→advisory.

## Axis 2 — code ↔ docs (PASS)

- **The render IS the doc update.** `doti render-skills` regenerated the installed assets from source; the agent-context (`.doti/agent-context.md`) and `CLAUDE.md`/`AGENTS.md` re-rendered **unchanged** (they do not enumerate individual utility skills), so no stale claim exists there.
- **README** updated to mention `/doti-auto` (in parallel with the existing `/doti-constitution` mention), keeping the project's operator-facing skill description current with the new capability — the 011 README-accuracy goal.
- No symbol was removed/renamed (purely additive), so there is no stale old-name to grep for.

## Axis 3 — source ↔ installed (PASS)

- `doti render-skills --check` — **no drift** (rendered skills/agent-context/entrypoints match source).
- `doti payload check --repo .` — **93 managed payload files match** (installed `.doti` payload parity, static + rendered). No hand-edited installed asset; the skill was authored in `.doti/core/skills.json` + the template and rendered.

## Gate

`gate run --profile normal` green over the full change set (the skill-drift + doti-payload steps are the deterministic proof for this prose change; the affected-test planner correctly took the full lane on the unattributed `.doti/core/skills.json` path and all 12 prebuilt test projects passed — no behavioral regression).

## Note — release train

013 completes to drift-review as the **second member of the 012+013+014 release train**; it is finalized as completed-unreleased when 014 starts. No release here.

## Verdict

**No open drift** in any applicable axis. Additive prose, rendered cleanly, parity clean, docs current. Ready to carry into the train (start `/01` for 014).
