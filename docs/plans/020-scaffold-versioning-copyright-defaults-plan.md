# Plan — 020 Scaffold versioning + copyright defaults

**Spec:** [docs/specs/020-scaffold-versioning-copyright-defaults.md](../specs/020-scaffold-versioning-copyright-defaults.md). **Stage:** `/03-doti-plan`.

## Summary

Ship two missing defaults in the `hx-dotnet-cli` scaffold template so a generated repo is correct out of the box: (1) a **`GitVersion.yml`** that makes versioning trunk-based (the trunk increments **Patch** by default, the series starts at **0.1.0**) so the existing doti increment model computes correctly — bug-fix-only cycle → patch, feature cycle → minor — without the operator hand-authoring config; and (2) an auto-year **`<Copyright>`** in the template's `Directory.Build.props` so a generated repo's release assembly carries `Copyright © <release-year> <Company>`. Add template-test coverage so neither default can silently regress. This is a **template-content + test** change only — no production `*.Core`/`*.Cli` code, no architecture-rule delta.

## Technical Context

- The defect: the template ships **no version config**, so a generated repo falls back to GitVersion 6's **default workflow, GitFlow**, under which a `develop`/`dev` branch is the next-minor integration branch — a bug-fix cycle there bumps **minor**, not patch (observed in ergon: 0.2.0 instead of 0.1.1; reproduced/RCA'd, and the GitVersion cache masked the corrected config). The doti increment *model* already exists in the tool (FR-044/SC-016: feature → `+semver: minor` via the release transition; bug-fix-only → no signal → branch default); it only needs the per-repo config that makes GitVersion's default increment **Patch**.
- The copyright gap: the template's `Directory.Build.props` sets `<Company>ACME_COMPANY</Company>` (replaced by the `company` symbol) but **no `<Copyright>`**, so generated release assemblies carry no copyright (confirmed by reading the template + the generated agentx repo).
- Proven config (running in ergon + agentx via the vendored GitVersion 6.7.0): `workflow: GitHubFlow/v1` + `next-version: 0.1.0` + `assembly-*-scheme: MajorMinorPatch` yields a clean `0.1.0` start and patch-by-default.
- Template content lives under `scaffold/templates/dotnet-cli/**`, is Sentrux-/build-excluded in this repo, and is packed by `scaffold/Hx.Scaffold.Templates.csproj` via `Content Include="templates\**\*"` — so a new `GitVersion.yml` is auto-packed with **no csproj change**.

## Constitution Check (gate)

**§1 inherited invariants — PASS.** *Template Boundary*: the .NET template owns static generated-repo layout — adding a `GitVersion.yml` + a `<Copyright>` to template content is squarely within that boundary. *Cross-Platform*: a `GitVersion.yml` and an MSBuild property are platform-neutral; no PowerShell/Bash runner. *Deterministic Ownership / Codified Cycle*: versioning stays GitVersion-authoritative (no Doti-owned version arithmetic); the change adds config, not a gate downgrade. *Public Hygiene*: no developer-local paths/secrets.

**§2 project declarations — PASS.** *Coding style*: the change is generated-code template content + test, with **no `*.Core`/`*.Cli`** logic, so the thin-CLI / library-first rules are untouched. *Tech stack*: GitVersion (vendored win-x64) is already the declared versioning authority — this only supplies its config. *Performance / "deterministic, reproducible proof… not wall-clock"*: the copyright year is **assembly metadata, not a proof input** — no gate proof, affected-test proof, managed-asset hash, or release payload check reads it (verified: `GateProof` has no temporal fields; the template ships a *static* file whose literal `$([System.DateTime]::…)` string is deterministic in the package). The "not wall-clock" rule governs proof freshness, which is unaffected. The user's FR-007 explicitly scopes a date-valued copyright, which is the appropriate place for a wall-clock value.

Re-evaluated after design: still PASS — no rule delta, no boundary crossing, no proof touched.

## Research (resolve unknowns)

### Decision 1 — Ship a trunk-based GitVersion config in the template

- **Decision:** add `scaffold/templates/dotnet-cli/GitVersion.yml`:
  ```yaml
  workflow: GitHubFlow/v1
  next-version: 0.1.0
  assembly-versioning-scheme: MajorMinorPatch
  assembly-file-versioning-scheme: MajorMinorPatch
  assembly-informational-format: '{MajorMinorPatch}'
  ignore:
    sha: []
  ```
- **Rationale:** `GitHubFlow/v1` makes the trunk (`main`) increment **Patch** by default and has **no minor-tracking develop branch**, so a bug-fix-only cycle (no `+semver` signal) bumps patch, while the doti release transition's `+semver: minor` still bumps minor for feature cycles (FR-002/FR-003 preserved). `next-version: 0.1.0` pins the series start (FR-004). The assembly-scheme keys keep the reported version a clean `MAJOR.MINOR.PATCH`. This exact config is proven in ergon (v0.1.0 → v0.1.1 patch) and agentx (`hx version calculate` → 0.1.0).
- **Alternatives rejected:** *`TrunkBased/preview1`* — still a preview preset id (the unstable id is also why a `TrunkBased/v1` attempt errored); `GitHubFlow/v1` is the stable trunk-based preset with the same `main`=Patch behavior. *Explicit per-branch `increment:` config with no workflow preset* — verbose and easy to get a branch wrong (this is exactly the trap: the prior agentx config set `main: increment: Minor` and would have bumped minor on bug fixes); the preset encodes the intent in one line. *No config (status quo)* — the defect.

### Decision 2 — Auto-year copyright via a build-time MSBuild date function

- **Decision:** add to the template's `Directory.Build.props`, immediately after `<Company>`:
  ```xml
  <Copyright>Copyright © $([System.DateTime]::UtcNow.Year) $(Company)</Copyright>
  ```
  `$(Company)` resolves to the substituted company (the `company` symbol replaces `ACME_COMPANY` above it), and the date function evaluates at build time.
- **Rationale:** `hx release` builds the target product **in the release year**, so the release assembly's `AssemblyCopyrightAttribute` carries the release year (FR-006/FR-007), with **no hx/`*.Core` change** and reusing the existing `Directory.Build.props` + `$(Company)` pattern (simplest correct, smallest blast radius). The year is not a proof input (Constitution Check above), so deterministic proof is preserved. Verified that speckit-doti's *own* release is unaffected: the template is packed as a static file (the literal function string), and this repo's root `Directory.Build.props` is untouched, so no payload-hash/release-staging impact.
- **Alternatives rejected:** *Release-time injection (`hx release` passes `-p:Copyright=…<year>` from `LocalReleaseService`)* — adds a production-`*.Core` change plus a non-release-build fallback, for a cross-year-reproducibility benefit that doti's proofs do not require (the year is not a proof input, so a build-time value already preserves deterministic proof). Heavier and crosses the template→tool boundary for no proof gain. *Static year / year-range* — goes stale; not auto-updating (fails FR-007).

## Design

**Patterns assessed:** the template's `Directory.Build.props` + `.template.config` symbol system; the `scaffold/templates/**` generated-code boundary (these files are config/content, not `.cs`); the `Hx.Scaffold.Templates.csproj` content glob; the existing `test/Hx.Templates.Tests` golden tests (which today assert template structure but not Company/Copyright/version). The design fits all of them and changes none.

**Files:**
- **NEW** `scaffold/templates/dotnet-cli/GitVersion.yml` — the trunk-based config (Decision 1). Auto-packed by the existing `templates\**\*` content glob.
- **EDIT** `scaffold/templates/dotnet-cli/Directory.Build.props` — add the `<Copyright>` line (Decision 2).
- **EDIT** `test/Hx.Templates.Tests/TemplateGoldenTests.cs` — assert (a) `GitVersion.yml` is present and pins `workflow: GitHubFlow/v1` + `next-version: 0.1.0`; (b) `Directory.Build.props` contains the `<Copyright>` with the year function + `$(Company)` (FR-009 regression guard).
- **EDIT** `CHANGELOG.md` + `README.md` — note the new generated-repo defaults (release-doc + drift hygiene; done in implement per the release-docs-in-implement practice).
- Verify (no edit expected): `.template.config/template.json` does not exclude `GitVersion.yml` from instantiation; `Hx.Scaffold.Templates.csproj` packs it.

**Architecture delta:** **none.** No projects/namespaces/layers added or moved; no `*.Core`/`*.Cli` code; no `rules/architecture.json` ArchUnit family change and no `.sentrux/rules.toml` boundary change (the touched files are Sentrux-excluded template content + test). A structural-rule change would be drift here — there is intentionally none.

## CLI surface & error contract

Omitted — the feature adds/changes no CLI command or option. `hx new`/`hx release`/`hx version calculate` are unchanged; they simply consume the newly-shipped template config.

## Command Availability

| Area | Command | Status |
| --- | --- | --- |
| Build | `dotnet build .\scaffold-dotnet.slnx -c Release --no-restore /m:1` | implemented |
| Test | `dotnet test .\scaffold-dotnet.slnx -c Release --no-build /m:1` | implemented |
| Template pack | `dotnet pack scaffold/Hx.Scaffold.Templates.csproj` | implemented |
| Version calc | `hx version calculate --repo . --json` | implemented (GitVersion 6.7.0 vendored) |
| Gate | `hx gate run --repo . --profile normal\|release --json` | implemented |
| Generate (smoke) | `hx new --name … --output … --company … --json` | implemented |

No planned-but-absent command; nothing downgraded.

## Complexity Tracking

None — the Constitution Check passed with no violation.

## Risks

- **Generated-repo round-trip coverage:** the end-to-end build of a generated repo (which would exercise the year function + GitVersion.yml live) runs only under `HX_TEMPLATE_ROUNDTRIP=1`; the default gate exercises the **static** golden assertions. Mitigation: the golden test asserts the template content deterministically; the live behavior is already proven in ergon/agentx. Note this honestly (advisory beyond the golden assertions).
- **GitVersion preset stability:** `GitHubFlow/v1` is a stable preset id (vs `TrunkBased/preview1`); pinned vendored GitVersion is 6.7.0. Low risk.
- **Cross-year assembly reproducibility (generated repos):** a generated repo rebuilt in a later year stamps a different copyright year — accepted and intended (a copyright year should reflect the build/release year); not a doti proof input.
