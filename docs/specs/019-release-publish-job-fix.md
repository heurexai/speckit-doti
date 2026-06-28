# 019 — Fix the release publish job (NuGet.org deploy)

## Goal

The `production` environment deployments are failing: `release.yml`'s **publish** job (which runs `environment: production` to mint the Trusted-Publishing OIDC token) dies at `setup-dotnet` with `The specified global.json file 'global.json' does not exist`. The publish job uses `global-json-file: global.json` but — unlike the pack-and-smoke job — it **never checks out the repo**, so `global.json` is absent. The job only runs `dotnet nuget push`, so it does not need the repo's exact SDK pin. Switch its `setup-dotnet` to a `dotnet-version` so the deploy proceeds to the NuGet login + push.

Context (verified): the `NUGET_USER` secret IS set and the `production` environment exists (no required reviewer, so the job runs immediately rather than waiting) — the `global.json` step is the sole blocker. After this fix the publish reaches the NuGet Trusted-Publishing login; whether that succeeds depends on the nuget.org Trusted-Publishing policy (operator-owned, not in this repo).

## User Scenarios & Testing

**Priority Mode** — CI/deploy fix: fail-closed safety + a correct pipeline before ergonomics. The publish job's security posture (OIDC on this job only, Trusted Publishing the sole credential path) MUST be preserved.

### Work Item 1 — The publish job runs past `setup-dotnet` (Priority: P1)

The `release.yml` publish job's `setup-dotnet` succeeds without a repo checkout, so the deploy reaches the NuGet login + push.

- **Why this priority:** every `production` deployment fails at this step today; the package cannot reach NuGet.org regardless of the (passing) smoke.
- **Independent Test:** on the next pushed `v*` tag, the publish job's `setup-dotnet` succeeds (no `global.json` error) and the job advances to the NuGet login + `dotnet nuget push` steps.
- **Acceptance Scenarios:**
  1. **Given** the publish job with no checkout, **When** `setup-dotnet` runs, **Then** it installs the .NET 10 SDK via `dotnet-version` and does not error on a missing `global.json`.
  2. **Given** the fix, **When** the job continues, **Then** the OIDC/`id-token: write` scoping, the `production` environment, the NuGet login, and the `dotnet nuget push` step are unchanged — only the `setup-dotnet` input changed.

### Edge Cases

- The publish job MUST NOT gain a repo checkout it doesn't need — `dotnet-version` is the minimal fix; a checkout would also work but is heavier and unnecessary for `dotnet nuget push`.
- The pack-and-smoke job (which DOES check out + uses `global-json-file`) is unchanged — it needs the repo SDK pin.

## Scope

Included: in `.github/workflows/release.yml`, change the **publish** job's `actions/setup-dotnet` input from `global-json-file: global.json` to `dotnet-version: '10.0.x'`.

Excluded: no change to the pack-and-smoke job, the OIDC/permissions, the `production` environment, the NuGet login or push steps, or any other workflow. No code, no skill, no proof change. The nuget.org Trusted-Publishing policy + any reviewer on `production` are operator-owned and out of scope.

## Functional Requirements

- `FR-001`: The `release.yml` publish job's `setup-dotnet` MUST NOT depend on a repo file (`global.json`) it has no checkout for — it MUST pin via `dotnet-version: '10.0.x'` so the step succeeds on a checkout-less job. `[WI1]`
- `FR-002`: The change MUST be confined to the publish job's `setup-dotnet` input — the `id-token: write` scoping, `environment: production`, NuGet login (OIDC), and `dotnet nuget push --skip-duplicate` steps remain unchanged (the security posture is preserved). `[WI1]`

## Success Criteria

- `SC-001`: On the next pushed `v*` tag, the publish job advances past `setup-dotnet` (no `global.json` error) to the NuGet login + push.
- `SC-002`: No other workflow/job/step changes; the publish job still runs only in `production` with OIDC-only Trusted Publishing.

## Key Entities

- **The publish job** — `release.yml`'s `production`-environment NuGet.org Trusted-Publishing deploy; its `setup-dotnet` is the failing step.

## Deterministic Surfaces

- `.github/workflows/release.yml` — the publish job's `setup-dotnet` input (the only edit).

## Architecture Impact

- CI/deploy-config only. No `*.Core` code, no contract, no rule/Sentrux/ArchUnit/proof surface.

## Sentrux And Hygiene Impact

- None — a YAML workflow edit (Sentrux-excluded). It strengthens the pipeline (the deploy actually runs); it relaxes no gate.

## Assumptions

- `dotnet nuget push` + the NuGet/login action need only a .NET SDK on PATH, not the repo's exact `global.json` pin (the publish job builds nothing) — verified by the job's steps (download artifact → setup-dotnet → login → push).
- The `production` environment + `NUGET_USER` secret are already configured (verified); the nuget.org Trusted-Publishing policy is operator-owned and observed on the next run.

## Acceptance

- Command-backed: the workflow is YAML-validated locally; the publish-job behavior is observed on the v0.12.5 tag push.
