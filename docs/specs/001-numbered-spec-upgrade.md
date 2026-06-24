# Spec: Numbered spec upgrade

> WHAT and WHY only. This feature restores the Spec Kit-style ordering signal to doti specifications and makes upgrades from v0.5 safe for repos that already have unnumbered project docs.

## Goal

New doti work must be easy to follow in chronological order, and upgraded repos must not lose or silently rewrite historical project documentation.

The original Spec Kit workflow made feature order visible through numbered specification folders. doti had drifted to flat unnumbered filenames, which made current and future work harder to scan. This feature makes numbered slugs the default and enforced path for new/open work while preserving completed legacy history.

## Scope

Included behavior:

- New doti specs use a `NNN-short-name` slug, beginning with the next available three-digit prefix under `docs/specs`.
- The same numbered slug flows through `docs/specs/{feature}.md`, `docs/plans/{feature}-plan.md`, `docs/tasks/{feature}-tasks.md`, and `doti cycle stamp --feature`.
- `doti cycle stamp` fails closed when the first stage of a new cycle is stamped with an unnumbered feature slug.
- CLI diagnostics explain the expected slug shape and provide a numbered example.
- `hx update` guidance is safe for repos installed from v0.5: managed doti assets update normally, implemented/completed legacy specs are left alone, and open unimplemented unnumbered specs are migrated by the agent before continuation.
- README, shared agent context, source templates, and rendered Codex/Claude skill wrappers describe the same rule.

Excluded behavior:

- Automatic renaming of project-owned `docs/specs`, `docs/plans`, or `docs/tasks` during `hx update`.
- Renaming implemented or completed historical specs.
- Retagging, publishing, or cutting a release as part of this feature.
- Inferring implementation status for arbitrary legacy specs without agent/operator review.

## Functional Requirements

- `FR-001`: New doti feature specs MUST use a numbered feature slug in the form `NNN-short-name`.
- `FR-002`: The first `doti cycle stamp` for a new cycle MUST reject an unnumbered `--feature` value with a structured usage diagnostic.
- `FR-003`: The diagnostic for an unnumbered slug MUST identify the invalid feature and show the expected `NNN-short-name` format with an example.
- `FR-004`: Existing active legacy cycles MUST be able to migrate an open, unimplemented unnumbered spec by renaming the project docs and re-stamping `specify` with the new numbered slug.
- `FR-005`: Completed or implemented legacy specs MUST remain on their existing historical filenames during and after upgrade.
- `FR-006`: After a completed legacy cycle, the next new spec MUST use a numbered slug.
- `FR-007`: `hx update` MUST not silently rename project-owned feature docs; instead, it MUST report follow-up guidance for open legacy specs and subsequent new specs.
- `FR-008`: The source templates, installed templates, generated agent context, rendered Codex/Claude skills, README, and command help MUST all use consistent numbered-spec wording.
- `FR-009`: The workflow model may keep `{feature}` as the placeholder, but documentation MUST define that value as the full numbered slug for new/open work.

## Success Criteria

- `SC-001`: `doti cycle stamp --stage specify --feature not-numbered` fails with a usage result that names `NNN-short-name`.
- `SC-002`: A migrated open legacy spec can be re-stamped as `001-legacy-open` and pass prerequisite freshness for the next stage.
- `SC-003`: A completed legacy unnumbered spec remains present while the next cycle starts with a numbered slug.
- `SC-004`: `hx update` reports a follow-up that tells operators to leave implemented/completed legacy specs unchanged and migrate open unimplemented unnumbered specs.
- `SC-005`: `doti render-skills --check` reports no drift after the source template changes are rendered.
- `SC-006`: The normal and release gate lanes pass after the workflow and updater changes.

## Key Entities

- **Numbered feature slug** - the `NNN-short-name` identifier used as the doti cycle feature and doc filename stem.
- **Legacy completed spec** - an implemented or completed historical spec from before numbering; it remains unchanged.
- **Legacy open spec** - an unimplemented historical spec that must be migrated to a numbered slug before further doti work.
- **Managed doti assets** - workflow templates, source templates, generated skills, and metadata that `hx update` may replace.
- **Project-owned feature docs** - repo-specific specs, plans, and tasks under `docs/`; `hx update` must not silently rename them.

## Deterministic Surfaces

- `tools/Hx.Cycle.Core` validates numbered feature slugs at first-stage stamp time.
- `tools/Hx.Runner.Cli` maps slug validation failures to the existing `CliResult` usage envelope.
- `src/Hx.Scaffold.Core/Update` reports upgrade follow-up guidance from `hx update`.
- `doti/core/templates/commands/doti-specify.md`, `doti/core/templates/spec-template.md`, `doti/core/templates/agent-context-template.md`, `.doti/templates/spec-template.md`, `.doti/workflows/doti/workflow.yml`, and `doti/core/workflows/doti/workflow.yml` define the workflow guidance.
- Rendered `.agents`, `.claude`, `.doti/agent-context.md`, `AGENTS.md`, and `CLAUDE.md` are generated from the source templates and profile data.
- `README.md` documents install/update behavior for operators.

## Architecture Impact

- Slug validation belongs in `Hx.Cycle.Core`; CLI command bodies remain thin and translate known input failures into structured results.
- Update guidance belongs in `Hx.Scaffold.Core.Update`; update mutation remains limited to managed assets and does not rename project docs.
- No architecture rule changes are required because the change stays within existing core/CLI boundaries.

## Sentrux And Hygiene Impact

- No Sentrux baseline is created or updated.
- No vendored binaries, release artifacts, secrets, or local-only paths are added.
- Generated skills and agent context must be re-rendered from source and verified with `doti render-skills --check`.

## Assumptions

- Existing completed or implemented legacy docs are valuable historical records and should not be renamed automatically.
- The agent/operator can determine whether an unnumbered legacy spec is open and unimplemented before migration.
- `001-numbered-spec-upgrade` is the first numbered doti feature in this repo because existing historical docs are unnumbered.

## Acceptance

- Command-backed today: runner tests, scaffold update tests, Doti renderer tests, `doti render-skills --check`, `gate run --profile normal`, and `gate run --profile release`.
- Advisory review: verify the generated guidance consistently states the legacy upgrade rule and that no project-owned historical docs were silently renamed.

## Clarifications

### 2026-06-24

- No operator question was needed. The operator clarified that unimplemented older specs should use the new numbering format, implemented/completed specs should be left alone, and subsequent new specs should use numbering.
- This spec was retrofitted after implementation as a process recovery. Future feature work should begin with `/doti-specify`.
