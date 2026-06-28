# Arch Review — 015 Release accuracy

**Stage:** `/06-doti-arch-review`. **Change under review:** [spec](../specs/015-release-accuracy.md) / [plan](../plans/015-release-accuracy-plan.md) / [tasks](../tasks/015-release-accuracy-tasks.md). Changed files: `README.md`, `.github/workflows/release.yml`.

## Triage

**Docs + CI-config change — no production/`*.Core` code, no contract, no generated-code template.** The production code lenses (data-contract, modularity, testability of code) EXIT *not applicable*. The applicable lenses: **docs accuracy/clarity** (the README half) and **blast-radius + security** (the `release.yml` half — a CI/release-pipeline change). Sentrux/ArchUnit are not run here.

## Lens findings

### Docs accuracy & single-source (README) — no blocker

- **F1 (MEDIUM → mitigated):** the README must describe the seven skills *accurately* (a wrong description is a doc-correctness defect). Mitigation: each summary is drawn from the canonical `.doti/core/skills.json` descriptions (constitution §1/§2, bug assess→fix→test, amend reconcile, drift-fix fix-code-not-spec, converge coverage-gap, upgrade two-plane). T001.
- **F2 (MEDIUM → mitigated):** the README must not become a second source of truth that drifts from the rendered skills (FR-003). Mitigation: the plan keeps it an *overview* (names + one-line purpose in a table; prose only for the constitution) and explicitly states the skills are single-sourced. `doti render-skills --check` + `doti payload check` (T003) confirm the README change touches no rendered asset.

### Blast-radius (release.yml) — no blocker

- **F3 (HIGH → mitigated):** a malformed `release.yml` step could break the whole release pipeline (the publish gate). Mitigation: the change is a single additive bash step in `pack-and-smoke` using the already-trusted `hx tools fetch`; it does not alter the job graph, the `publish` job, or the `needs:` dependency. The payload-root derivation is from `hx version --json` (the same envelope already used). If the fetch step fails, it fails closed (the publish was already blocked — no regression).
- **F4 (MEDIUM → confirm-at-CI):** the store-populate timing (whether `hx new`'s first-smoke resolves the fetched binary) can't be exercised locally. Mitigation: fetch into the exact `sourceRepoRoot` (the installed payload) the design reads from; observed on the v0.12.1 tag push, one CI iteration budgeted (the plan's stated residual risk).

### Security (release.yml) — no blocker

- **F5 (HIGH → preserved):** the publish path's security posture MUST be untouched — `id-token: write` stays on the `publish` job only, the `production` environment + required reviewer gate OIDC, and Trusted Publishing remains the only credential path. Mitigation: the change is confined to `pack-and-smoke` (a `contents: read` job with no OIDC); it adds a hash-verified fetch and touches nothing in the `publish` job. No new secret, no credential, no permission change.
- **F6 (LOW):** `hx tools fetch` downloads from the pinned `heurexai/*` fork-release URLs and verifies the manifest SHA-256 (fail-closed). No unpinned/`latest` fetch, no supply-chain widening beyond the already-vendored, already-hashed tool manifests.

## Verdict

**No open BLOCKER in any applicable lens.** The README stays an accurate single-sourced overview; the `release.yml` change is an additive, hash-verified, fail-closed provisioning step that preserves the publish job's OIDC/Trusted-Publishing security posture untouched. Cleared for `/07-doti-implement`.
