# 015 — Release accuracy: README skill surface + release-CI tool provisioning

## Goal

Make the v0.12.x release **correct and accurately documented**, in two parts an operator just surfaced:

1. **Document the full unnumbered skill surface in the README.** The README explains the nine numbered stages (`/01`–`/09`) and mentions `/doti-constitution` and `/doti-auto`, but omits the other unnumbered **utility skills** (`doti-bug`, `doti-amend`, `doti-drift-fix`, `doti-upgrade`, `converge`) and gives the **constitution** only a one-line treatment. A reader cannot discover the project-level constitution workflow or the recovery/automation/upkeep utilities from the README. Explain them all, with the constitution given a proper, fuller explanation.
2. **Fix the release CI's per-RID tool provisioning.** `release.yml`'s source-free install smoke runs `hx new`, whose first-smoke verifies the vendored structural tools (Sentrux, Gitleaks). The binaries are gitignored (fetched on demand) and the global tool never bundles them, so on the ubuntu-latest runner the smoke fails-closed: `"Sentrux executable is missing for linux-x64"`. The manifests already pin **win-x64 + linux-x64 + osx-arm64** assets to the `heurexai/sentrux` (and gitleaks/gitversion) fork releases with hashes — the CI just never fetches them. The published global tool is fine (the local Windows release smoke passes); only the Linux CI smoke + the consequent NuGet publish are blocked.

Both are release-accuracy fixes for v0.12.1: one makes the docs honest, the other makes the published artifact's CI gate pass so the package reaches NuGet.org.

## User Scenarios & Testing

**Priority Mode** — workflow / tooling + docs change: fail-closed safety + deterministic proof before ergonomics; for the docs half, truth-first (clarity + accuracy of what ships).

### Work Item 1 — Document the unnumbered utility skills in the README (Priority: P1)

A reader of the README can discover and understand every unnumbered Doti skill, especially the project constitution.

- **Why this priority:** the constitution is a first-class, operator-authored project artifact that `plan` and `arch-review` evaluate against — a reader who can't find it in the README misses how the project's own rules are declared and kept fresh. The recovery/automation utilities are likewise undiscoverable today.
- **Independent Test:** read the README's Doti-workflow section and confirm each unnumbered skill (`doti-constitution`, `doti-auto`, `doti-bug`, `doti-amend`, `doti-drift-fix`, `converge`, `doti-upgrade`) is named and explained, with the constitution given a fuller, multi-sentence treatment (its two layers, how it is consumed, how it is authored/amended).
- **Acceptance Scenarios:**
  1. **Given** the README, **When** a reader looks past the numbered stages, **Then** a clearly-labelled section explains every unnumbered utility skill by name and purpose.
  2. **Given** the constitution explanation, **When** a reader reads it, **Then** they understand the §1 (inherited, codified) / §2 (operator-authored: domain, tech stack, coding style, security, performance) split, that `plan` and `arch-review` re-read §2 fresh via `hx doti constitution`, and that it is authored/amended with `/doti-constitution` and tracked by the cycle + git history.

### Work Item 2 — Release CI provisions the host-RID vendored tools from the fork (Priority: P1)

`release.yml`'s install smoke fetches the host-RID structural tools from their pinned fork releases before `hx new`, so the first-smoke verifies them instead of failing-closed.

- **Why this priority:** the NuGet.org publish is gated behind the smoke; until it passes on the runner platform, the published global tool cannot reach the channel even though the package itself is correct.
- **Independent Test:** the `release.yml` pack-and-smoke job, on its runner (ubuntu-latest), provisions the host-RID Sentrux/Gitleaks via `hx tools fetch` (manifest-pinned, hash-verified, from the `heurexai/*` fork releases) so `hx new`'s first-smoke passes and the publish job proceeds.
- **Acceptance Scenarios:**
  1. **Given** a fresh CI runner with no vendored binaries, **When** the smoke runs, **Then** the host-RID structural tools are fetched + hash-verified from their pinned fork-release URLs before `hx new`, and the first-smoke verifies (not fail-closed).
  2. **Given** a manifest/hash mismatch on fetch, **When** it occurs, **Then** the fetch fails closed (no silent skip) — provisioning never weakens the hash pinning.

### Edge Cases

- A tool with no asset for the host RID: `hx tools fetch` skips it explicitly (already its behavior); only tools the manifest declares for the RID are fetched.
- The README must not duplicate the rendered skill text verbatim — it is a human overview that links the skill names, not a second source of truth (the skills are single-sourced in `.doti/core/skills.json`).
- The CI fetch is network-dependent (the fork releases); a transient download failure surfaces as a fail-closed fetch error, not a skipped verification.

## Scope

Included:

- A README subsection explaining the unnumbered utility skills, with the constitution given a fuller treatment.
- A `release.yml` step that fetches the host-RID vendored tools (`hx tools fetch`) into the installed payload before the `hx new` smoke.

Excluded:

- No change to the skills themselves or `.doti/core/skills.json` (the README only documents them).
- No change to the structural gates, rules, Sentrux policy, or any proof.
- No change to `hx new`'s own provisioning behavior for end-users (a candidate follow-up: make `hx new` self-provision the host-RID tools so Linux/macOS users get it too — out of scope here).

## Functional Requirements

- `FR-001`: The README MUST explain every unnumbered Doti utility skill by name and purpose: `doti-constitution`, `doti-auto`, `doti-bug`, `doti-amend`, `doti-drift-fix`, `converge`, `doti-upgrade`. `[WI1]`
- `FR-002`: The README's `doti-constitution` explanation MUST cover the two-layer structure (§1 inherited/codified vs §2 operator-authored: domain, tech stack, coding style, security, performance), that `/03-doti-plan` and `/06-doti-arch-review` re-read §2 fresh via `hx doti constitution`, and that it is authored/amended with `/doti-constitution` and tracked by the cycle + git history (no SemVer doc-version). `[WI1]`
- `FR-003`: The README skill documentation MUST stay an overview that names the skills — it MUST NOT become a second source of truth duplicating the rendered skill text (the skills remain single-sourced in `.doti/core/skills.json`). `[WI1]`
- `FR-004`: `release.yml`'s pack-and-smoke job MUST fetch the host-RID vendored structural tools via `hx tools fetch` (manifest-pinned, hash-verified, from the `heurexai/*` fork releases) into the installed payload BEFORE the `hx new` smoke, so the first-smoke verifies them. `[WI2]`
- `FR-005`: The CI provisioning MUST remain fail-closed — a download or hash-verification failure fails the step (no silent skip), preserving the existing hash pinning and never weakening a gate. `[WI2]`

## Success Criteria

- `SC-001`: The README names and explains all seven unnumbered utility skills; a reader can find the constitution workflow and the recovery/automation/upkeep utilities.
- `SC-002`: The constitution explanation conveys the §1/§2 split, the fresh re-injection at plan/arch-review, and the author/amend path.
- `SC-003`: The README remains an overview (no verbatim duplication of the rendered skill bodies).
- `SC-004`: `release.yml` provisions the host-RID Sentrux/Gitleaks from the fork before the smoke; the smoke passes on ubuntu-latest and the publish job proceeds (pending the `production`-environment reviewer).
- `SC-005`: The CI fetch is hash-verified + fail-closed; no rule, limit, or proof changes.

## Key Entities

- **Unnumbered utility skill** — a by-name Doti skill outside the numbered cycle (`doti-constitution`/`doti-auto`/`doti-bug`/`doti-amend`/`doti-drift-fix`/`converge`/`doti-upgrade`).
- **Host-RID tool provisioning** — `hx tools fetch` resolving the manifest's per-RID asset (Sentrux/Gitleaks from the `heurexai/*` fork releases), hash-verified into the payload the generated repo's first-smoke reads.

## Deterministic Surfaces

- `README.md` — the human overview; documents the skills (WI-1).
- `.github/workflows/release.yml` — the release CI; gains the host-RID tool-fetch step (WI-2).
- `tools/*/{sentrux,gitleaks,gitversion}.version.json` — the per-RID manifests (unchanged; already declare linux/macOS assets) the fetch reads.
- `hx tools fetch` — the manifest-pinned, hash-verified provisioning command (unchanged; invoked by the CI).

## Architecture Impact

- **Docs + CI config only.** WI-1 is `README.md` prose; WI-2 is a `.github/workflows/release.yml` step using the existing `hx tools fetch`. No `*.Core` code, no contract, no rule/Sentrux/ArchUnit/proof change. The README is a human overview, not a deterministic surface; the skills stay single-sourced.

## Sentrux And Hygiene Impact

- No Sentrux baseline/policy/limit change; no code. The README is prose (Sentrux source-excluded). The CI fetch strengthens (not weakens) the published artifact's gate by making it actually run on the runner platform.

## Assumptions

- The per-RID assets exist + are hash-pinned in the manifests (verified: `hx tools fetch --tool sentrux --rid linux-x64` downloaded + hash-verified the linux Sentrux from `heurexai/sentrux` v0.5.11; gitleaks/gitversion also declare linux-x64 + osx-arm64).
- The installed payload (`hx new`'s `sourceRepoRoot`) is the location whose `tools/` the generated repo's vendor + store-populate read; fetching there makes the first-smoke resolve the binaries (confirmed at implement against the CI run).

## Acceptance

- Command-backed today: `hx tools fetch` (manifest-pinned, hash-verified), `doti render-skills --check`, `doti payload check`, `gate run`.
- Planned by this feature: the README skill documentation + the `release.yml` provisioning step; the CI smoke pass is observed on the next pushed tag (v0.12.1).
