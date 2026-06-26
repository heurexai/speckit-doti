# RFC — the doti extension + multi-agent transpilation system

> Concept record for **T037 / FR-038** (feature 007). How doti skills and commands are authored **once** and
> transpiled to **N agent formats** through one registrar, and how an extension (e.g. the bug mini-cycle) plugs into
> that pipeline without hand-maintaining per-agent files.

## The problem

A skill (e.g. `/07-doti-implement`) and a command body should live in exactly ONE source. Hand-maintaining a separate
`SKILL.md` per agent (Claude, Codex, and any future agent) drifts silently and multiplies the edit surface. doti
authors once and renders to every agent, then drift-checks the result so the transpilation is non-forgeable.

## Authored once — the single sources

| Source | What it holds |
| --- | --- |
| `.doti/core/skills.json` | per-skill metadata: name, description, argument hint, highlights, next-step, and the shared operator-question protocol |
| `.doti/core/templates/commands/<name>.md` | the command body a skill points to (the step-by-step behavior) |
| `.doti/profiles/<profile>/profile.json` | the canonical command-availability footnote rendered into every skill |
| `.doti/core/templates/agent-context-template.md` | the shared agent context |

A cycle skill (`doti-specify`…`doti-release`) gets its `NN-` ordinal from `DotiWorkflowRegistry`; a utility skill
(`doti-upgrade`, `doti-bug`) renders unnumbered (`DotiWorkflowRegistry.ResolveSkillIdentity`). Either way, the source
is the same skills.json entry + command template.

## One registrar — `DotiAgentTarget`

Every agent format is one `DotiAgentTarget` record (`tools/Hx.Doti.Core/DotiAgentTarget.cs`) declaring:

- `Key` / `Compatibility` — the agent id and the `compatibility:` frontmatter value;
- `SkillsRoot` — where its `SKILL.md` files land (`.claude/skills`, `.agents/skills`, …);
- `ClaudeFrontmatter` — whether it emits the Claude-only keys (`argument-hint`, `user-invocable`, `disable-model-invocation`);
- `RootEntrypointPath` — its thin root entrypoint (`CLAUDE.md`, `AGENTS.md`, …);
- `Title` / `SkillsGlob`.

`DotiAgentTarget.All` is the registry; `FromKey` parses `--agents`. This single record is the ONLY place an agent's
format lives.

## Render to N — `doti render-skills`

`hx doti render-skills --repo . --agents <csv>` iterates **the registry × the authored skills**: for each agent it
transpiles each skill to that agent's `SKILL.md`, writes the per-agent root entrypoint, and renders the shared agent
context. `--check` re-renders in memory and reports drift (fail-closed) instead of writing — so the rendered files can
never silently diverge from the source.

### Adding a new agent format (beyond Claude/Codex)

1. Add one `DotiAgentTarget` to `All` (and a `FromKey` case) with the new agent's `SkillsRoot`, frontmatter policy,
   and root entrypoint.
2. `hx doti render-skills --agents <new-key>` — every authored skill transpiles to the new format; nothing else changes.
3. The drift check now covers the new agent's files too.

No skill, command body, profile, or extension is touched: the authored-once sources render to the new agent unchanged.

## Extensions — plug a sub-workflow into the same pipeline

An extension lives under `extensions/<name>/` and adds a sub-workflow without forking the renderer:

- `extensions/<name>/commands/*.md` — the absorbed command definitions (the Spec Kit source being ridden);
- a deterministic core service in `Hx.Doti.Core` + a thin CLI that enforce the workflow's teeth;
- a skills.json entry + a `.doti/core/templates/commands/<name>.md` body, which render through the SAME registrar to
  every agent.

The **bug mini-cycle** (`extensions/bug/`, T033/T034) is the first extension: `assess → fix → test` with a fail-closed
`BugCycleService`, the `doti bug` CLI, the SSRF-resistant URL trust policy for any URL the assess stage ingests, and
the rendered `doti-bug` skill. Future extensions (Spec Kit's `converge`, deeper checklists) follow the same shape.

## Why this rides the substrate (the teeth)

Transpilation and extensions are shared methodology; doti makes them safe. The rendered outputs are drift-checked
(`render-skills --check`) and payload-parity-checked, so authored-once → N-format never diverges; and an extension's
enforcement (the bug cycle's bound proofs, the URL policy's fail-closed refusal) is non-forgeable. Authoring once for
many agents is safe precisely because the gate proves the many match the one.
