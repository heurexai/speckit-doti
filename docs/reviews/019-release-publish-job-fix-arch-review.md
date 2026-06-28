# Arch Review — 019 Fix the release publish job

**Stage:** `/06-doti-arch-review`. **Change under review:** [spec](../specs/019-release-publish-job-fix.md) / [plan](../plans/019-release-publish-job-fix-plan.md) / [tasks](../tasks/019-release-publish-job-fix-tasks.md). Changed file: `.github/workflows/release.yml` (the publish job's `setup-dotnet` input).

## Triage

**CI/deploy-config change** (a single YAML input). No production `*.Core` code, no contract, no generated-code template, no rule/Sentrux/ArchUnit/proof surface. Applicable lenses: **blast-radius** (a release-pipeline edit) and **security** (the OIDC/Trusted-Publishing job).

## Lens findings

### Blast-radius (no blocker)

- **F1 (MEDIUM → mitigated):** a wrong edit to `release.yml` could break the deploy pipeline. The change is the minimal one — swap one `setup-dotnet` input (`global-json-file: global.json` → `dotnet-version: '10.0.x'`) in the **publish** job only; the job graph, `needs:`, artifact flow, and the pack-and-smoke job are untouched. The fix removes a dependency (a repo file the job never checks out), it adds none.
- **F2 (LOW → mitigated):** `dotnet-version: '10.0.x'` must be a valid SDK channel for `dotnet nuget push`. `10.0.x` matches the project's .NET 10 target and is the install channel setup-dotnet accepts; the job builds nothing, so only a working SDK on PATH is needed.

### Security (no blocker — the load-bearing lens)

- **F3 (HIGH → preserved):** the publish job's security posture MUST be unchanged. The edit touches only `setup-dotnet`'s SDK source — `id-token: write` stays scoped to this job, `environment: production` is unchanged, the NuGet OIDC login + `dotnet nuget push --skip-duplicate` and Trusted-Publishing-only credential path are untouched. No secret, permission, or environment change. (Separately: `production` has no required reviewer — a pre-existing operator choice, out of scope here; noted, not changed.)

## Verdict

**No open BLOCKER.** A minimal, security-neutral CI fix that removes the publish job's dependency on an unchecked-out `global.json`; the OIDC/Trusted-Publishing path is preserved. Cleared for `/07-doti-implement`.
