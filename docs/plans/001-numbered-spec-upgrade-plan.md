# Plan: Numbered spec upgrade

> Spec: `docs/specs/001-numbered-spec-upgrade.md`.

## Technical Context

The change spans the doti cycle engine, runner CLI diagnostics, scaffold updater guidance, source workflow templates, rendered agent files, README, and tests.

The existing workflow model stores produced artifacts as `docs/specs/{feature}.md`, `docs/plans/{feature}-plan.md`, and `docs/tasks/{feature}-tasks.md`. The correct design is to keep `{feature}` as the engine placeholder and require the supplied value to be the full numbered slug for new/open work.

## Constitution Check (gate)

PASS before design:

- **Deterministic Ownership** - first-stage slug validation is command-backed in `Hx.Cycle.Core`.
- **Bootstrap Honesty** - the rule is enforced only where implemented; legacy doc migration remains explicit agent/operator work.
- **Template Boundary** - source templates and generated skill wrappers are kept in sync through the renderer.
- **Public Hygiene** - no secrets, binaries, or local release artifacts are added.
- **Cross-Platform Rule** - no shell runner is introduced.
- **Codified Cycle** - the change strengthens cycle entry and update guidance.
- **Engineering Discipline** - tests cover rejection, legacy migration, completed legacy preservation, and updater follow-up guidance.

No violation, so Complexity Tracking is empty.

## Research

- **Decision:** keep the workflow placeholder as `{feature}` and validate the value supplied to the first `specify` stamp.
- **Rationale:** stage-model artifact resolution already works if `{feature}` is the full slug; changing the placeholder shape would cause broader migration risk with no benefit.
- **Alternatives rejected:** renaming workflow patterns to `{NNN-feature}` because it would be documentation-only and would not improve enforcement.

- **Decision:** do not make `hx update` rename project-owned feature docs.
- **Rationale:** update can safely replace managed doti assets, but it cannot know whether a legacy spec is implemented, open, abandoned, or intentionally historical.
- **Alternatives rejected:** automatic renaming of all unnumbered docs because it would rewrite completed history and could break references.

- **Decision:** allow active/open legacy specs to migrate by re-stamping `specify` with a numbered slug after the docs are renamed.
- **Rationale:** the current state substrate can replace the first-stage proof and feature slug without requiring bespoke migration state.
- **Alternatives rejected:** allowing all unnumbered active specs forever because it would preserve the ordering problem for new work.

## Design

### Cycle validation

Add a small `CycleFeatureSlug` helper in `tools/Hx.Cycle.Core` with a culture-invariant `NNN-short-name` regex and user-facing error text. Call it from `CycleService.Stamp` only when stamping a first-stage cycle with a supplied feature.

This preserves later-stage continuation and completed-cycle behavior while preventing new unnumbered cycles.

### Runner CLI diagnostics

Add a typed `CycleInputException` path so `RunnerCommands.DotiCycle.CycleStamp` returns a usage-class `CliResult` with:

- target `--feature`;
- summary `Invalid cycle feature slug.`;
- next action using `001-my-feature`.

### Update guidance

Add a general follow-up instruction to `ScaffoldUpdateService.Mutation` so `hx update` output reminds agents/operators:

- leave implemented/completed legacy specs unchanged;
- migrate open unimplemented unnumbered specs manually to `NNN-short-name`;
- create all subsequent specs with numbered slugs.

The update mutation continues replacing only managed assets.

### Templates and generated assets

Update:

- `doti/core/templates/commands/doti-specify.md`;
- `doti/core/templates/spec-template.md`;
- `.doti/templates/spec-template.md`;
- `doti/core/templates/agent-context-template.md`;
- `.doti/workflows/doti/workflow.yml`;
- `doti/core/workflows/doti/workflow.yml`;
- `doti/profiles/dotnet-cli/profile.json`;
- `README.md`.

Re-render installed Codex/Claude skills and root entrypoints with `doti render-skills`.

### Tests

Add/adjust tests in:

- `test/Hx.Runner.Tests` for first-stage slug rejection, active legacy migration, completed legacy preservation, and numbered next-cycle behavior.
- `test/Hx.Scaffold.Tests` for update follow-up guidance.
- existing cycle fixture samples to use numbered slugs.

## CLI Surface & Error Contract

Changed command: `doti cycle stamp`.

- **Error code:** existing `USG0001` for invalid arguments.
- **Exit class:** Usage.
- **Diagnostic target:** `--feature`.
- **Next action:** `doti cycle stamp --stage specify --feature 001-my-feature`.
- **`describe --json`:** `--feature` description says numbered feature slug is required on the first stamp.

No new stable error code is required.

## Deterministic Surfaces + Command Availability

Available and used:

- `dotnet run --project tools/Hx.Runner.Cli -c Release --no-build -- doti render-skills --repo . --agents codex,claude --check --json`
- `dotnet test test\Hx.Runner.Tests\Hx.Runner.Tests.csproj -c Release --nologo --disable-build-servers`
- `dotnet test test\Hx.Scaffold.Tests\Hx.Scaffold.Tests.csproj -c Release --nologo --disable-build-servers`
- `dotnet test test\Hx.Doti.Tests\Hx.Doti.Tests.csproj -c Release --nologo --disable-build-servers`
- `dotnet run --project tools\Hx.Runner.Cli -c Release --no-build -- gate run --repo . --profile normal --json`
- `dotnet run --project tools\Hx.Runner.Cli -c Release --no-build -- gate run --repo . --profile release --json`

## Constitution Check (after design)

PASS after design:

- The cycle engine owns validation.
- The updater only reports guidance for project-owned docs.
- Generated files are rendered, not hand-edited.
- Tests and gates cover the changed behavior.

## Complexity Tracking

No justified deviations.

## Risks

- A legacy repo may have inconsistent spec/plan/tasks stems; agent migration must inspect actual files before renaming.
- Historical references may point to old unnumbered filenames, so completed legacy docs are intentionally preserved.
- The feature was retrofitted after implementation; the commit must make the recovery visible through this spec/plan/tasks set.
