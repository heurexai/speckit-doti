# Tasks — 019 Fix the release publish job

**Plan:** [docs/plans/019-release-publish-job-fix-plan.md](../plans/019-release-publish-job-fix-plan.md). **Stage:** `/04-doti-tasks`. CI/deploy-config only.

## Phase 1 — Fix the publish job

- [x] T001 In `.github/workflows/release.yml`, in the **publish** job's `actions/setup-dotnet` step, replace `global-json-file: global.json` with `dotnet-version: '10.0.x'` (the job has no checkout and only runs `dotnet nuget push`). Leave the pack-and-smoke job, the OIDC `id-token: write` scoping, `environment: production`, the NuGet login, and `dotnet nuget push --skip-duplicate` unchanged — `.github/workflows/release.yml` — [covers FR-001, FR-002] <!-- doti-task-hash: d5678618ca20bc76ff8a5645f0ba2360e3f3f85f34db3d6650d42a2adeaed069 -->

## Phase 2 — Verify

- [x] T002 `release.yml` is valid YAML and the change is confined to the publish job's `setup-dotnet` input; `gate run --profile normal` green over the change set; stamp implement on green. The publish-job-runs-past-`setup-dotnet` outcome is observed on the v0.12.5 tag push (not locally reproducible) — `.github/workflows/release.yml` — [covers SC-001, SC-002] <!-- doti-task-hash: 3e33843b28985183680e908611477b64382020ebc86dd662e8836ba8a78b74f2 -->

## Coverage

- FR-001 → T001 | FR-002 → T001 | SC-001 → T001, T002 | SC-002 → T001, T002.
