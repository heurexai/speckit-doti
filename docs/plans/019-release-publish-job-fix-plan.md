# Plan — 019 Fix the release publish job

**Spec:** [docs/specs/019-release-publish-job-fix.md](../specs/019-release-publish-job-fix.md). **Stage:** `/03-doti-plan`. CI/deploy-config only.

## Existing-architecture assessment (verified)

- `release.yml` has two jobs: **pack-and-smoke** (checks out the repo, `setup-dotnet` with `global-json-file: global.json` — correct, it builds) and **publish** (`environment: production`, `id-token: write`, downloads the nupkg artifact, `setup-dotnet` with `global-json-file: global.json`, NuGet OIDC login, `dotnet nuget push --skip-duplicate`). The publish job has **no `actions/checkout`**, so `global.json` is absent → `setup-dotnet` fails (`The specified global.json file 'global.json' does not exist`) → every `production` deployment fails.
- Prereqs verified present: the `NUGET_USER` secret is set; the `production` environment exists (no required reviewer → the job runs, not waits). So the `global.json` step is the sole blocker.

## Design

**Decision:** change the publish job's `actions/setup-dotnet` input from `global-json-file: global.json` to `dotnet-version: '10.0.x'`. The publish job builds nothing — it only runs `dotnet nuget push` + the NuGet/login action, which need a .NET SDK on PATH, not the repo's pin. This is the minimal fix and needs no checkout.

**Rationale:** the bug is that the job references a repo file it never checks out; pinning by version removes that dependency without adding an unnecessary checkout. The pack-and-smoke job (which DOES check out + build) keeps `global-json-file` — it legitimately needs the repo SDK pin.

**Alternatives rejected:** add `actions/checkout` to the publish job — works, but pulls the whole repo into a job that only pushes a nupkg; heavier than needed and widens the job's surface.

## Architecture delta

- None — a single YAML input change in the publish job. The OIDC `id-token: write` scoping, `environment: production`, NuGet login, and `dotnet nuget push --skip-duplicate` are untouched; the security posture (OIDC-only Trusted Publishing on this job alone) is preserved.

## Constitution Check

- §1/§2: **PASS** — CI-config only, nothing weakened; the fix makes the deploy actually run.

## Risk

- **Low.** One YAML input. The deploy's end-to-end success past the NuGet login still depends on the operator-owned nuget.org Trusted-Publishing policy — observed on the v0.12.5 run, reported honestly.

## Next

`/04-doti-tasks`.
