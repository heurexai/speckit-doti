# Drift Review — Feature 019: Fix the release publish job

**Stage:** `/08-doti-drift-review`. **Date:** 2026-06-28. Change set: `.github/workflows/release.yml` (the publish job's `setup-dotnet` input) + the release-doc slug/CHANGELOG. **CI/deploy-config diff — no `*.cs`, no contract, no proof.**

## Axis 1 — spec ↔ code (PASS)

- **FR-001:** the publish job's `actions/setup-dotnet` now uses `dotnet-version: '10.0.x'` (line 98) instead of `global-json-file: global.json` — it no longer references a repo file it never checks out, so the checkout-less job's `setup-dotnet` step succeeds.
- **FR-002:** the change is confined to that one input — `global-json-file` now appears exactly once in `release.yml` (the pack-and-smoke job, which checks out + builds, correctly keeps it). The publish job's `id-token: write` scoping, `environment: production`, the NuGet OIDC login, and `dotnet nuget push --skip-duplicate` are untouched (verified by inspection); the security posture (OIDC-only Trusted Publishing on this job) is preserved.

Matches the plan: the minimal version-pin fix, no added checkout, pack-and-smoke unchanged (the rejected "add a checkout" alternative was not taken). `release.yml` is valid YAML.

## Axis 2 — code ↔ docs (PASS)

- The change is a workflow edit + its own explanatory comment; the CHANGELOG + README "Latest cycle" note describe it. No code symbol changed. No stale doc claim (the prior failure was undocumented; this records the fix).

## Axis 3 — source ↔ installed (PASS)

- `.github/workflows/release.yml` is not a `.doti` rendered/installed asset — `render-skills --check` + `payload check` are unaffected (the gate confirms). No skill/template/profile source touched.

## Gate

`gate run --profile normal` green over the change set. No code, rule, limit, or proof change.

## Note — the CI-observed outcome

WI-1's effect is observed on the v0.12.5 tag push: the publish job should advance past `setup-dotnet` (no `global.json` error) to the NuGet login + push. Whether the push itself succeeds depends on the operator-owned nuget.org Trusted-Publishing policy — reported honestly, not claimed here. The `NUGET_USER` secret + `production` environment are confirmed present; `production` has no required reviewer (a pre-existing operator choice, untouched).

## Verdict

**No open drift.** A minimal, security-neutral CI fix removing the publish job's dependency on an unchecked-out `global.json`. Ready for `/09-doti-release` (v0.12.5).
