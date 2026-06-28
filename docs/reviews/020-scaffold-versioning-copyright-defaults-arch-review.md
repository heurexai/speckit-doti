# Arch Review — 020 Scaffold versioning + copyright defaults

**Stage:** `/06-doti-arch-review`. Multi-lens design review (conditional depth). **No BLOCKER.**

## Triage

Change categories: **generated-code template** (`scaffold/templates/dotnet-cli/GitVersion.yml` new, `Directory.Build.props` edit — both *declarative config*, not `.cs` logic), **test** (`TemplateGoldenTests.cs`), **docs** (`CHANGELOG.md`, `README.md`). No production `*.Core`/`*.Cli` code, no contract/CLI surface, no `rules/*` change. Sentrux/ArchUnitNET are not run here (they gate implemented code at `/07`).

**Lenses activated:** design-soundness, edge-case/failure-mode, data-contract, blast-radius, simpler-alternative, testability, fit-with-architecture.
**Lenses exited (not applicable):** security (no secret/injection/authz/privacy surface — versioning + a copyright string), modularity/design-smells (declarative config, no code structure to smell).

## Findings

### Design soundness — PASS
The intent (bug-fix cycle → patch, feature cycle → minor, series starts at 0.1.0) is achieved by `workflow: GitHubFlow/v1` (trunk `main` = Patch by default) + `next-version: 0.1.0`, composed with the *existing, unchanged* doti `+semver: minor` release-transition signal for features. Proven end-to-end in ergon (v0.1.0 → v0.1.1) and agentx (`hx version calculate` → 0.1.0). The copyright is a single declarative MSBuild property. No design risk.

### Edge-case / failure-mode — PASS (2 LOW)
- **LOW — empty `--company`:** if an operator passes an empty company, `<Copyright>` renders `Copyright © <year> ` (no holder). Acceptable: the `company` symbol defaults to `Heurex`, and an empty value is operator input, not a defect of this feature. *Evidence:* `template.json` `company` defaultValue `Heurex`. No fix required.
- **LOW — generated repo with no commits/tags:** GitVersion on a fresh `hx new` repo (which `git init`s) computes `0.1.0` from the `next-version` floor; no history needed. *Evidence:* agentx (one commit, no tag) → `hx version calculate` = 0.1.0. No fix required.

### Data contract — PASS (1 MEDIUM, mitigated)
- **MEDIUM (mitigated by T001) — instantiation of a root config file:** the design assumes `dotnet new` instantiates a root `GitVersion.yml` and the pack globs it. *Evidence:* `Hx.Scaffold.Templates.csproj` globs `Content Include="templates\**\*"` (so it packs); but the `.template.config/template.json` must not exclude/skip the file. T001 verifies this as the first task before implementation. If instantiation excluded it, a generated repo would silently lack the config — caught by T001 + the golden test asserting the file's presence is *source-side* (it checks the template, which guarantees the packed content). Mitigated.
- GitVersion keys (`workflow: GitHubFlow/v1`, `next-version`, `assembly-*-scheme`, `assembly-informational-format`, `ignore.sha`) are valid for the vendored GitVersion 6.7.0 — confirmed by ergon running the same keys. PASS.

### Blast radius — PASS
Affects **every newly generated repo** (the intended surface). Explicitly does NOT affect: already-generated repos (template change is not retroactive — out of scope), or **speckit-doti's own assemblies** (this repo's root `Directory.Build.props` is untouched; the template's props is packed as static content, so the literal `$([System.DateTime]::…)` string ships deterministically and the year only evaluates inside a generated repo's build). Verified — no payload-hash/release-staging impact on this repo's release.

### Simpler alternative — PASS (already resolved in plan)
Build-time MSBuild year (chosen) vs release-time `hx release` injection (rejected: production-`*.Core` change + dev-build fallback for a non-proof reproducibility gain) and static year (rejected: stale). Workflow preset `GitHubFlow/v1` (chosen) vs explicit per-branch `increment:` config (rejected: the exact trap that mis-set `main: Minor` in the first agentx config) vs `TrunkBased/preview1` (rejected: preview id). The chosen designs are the simplest correct.

### Testability — PASS (1 LOW)
- The golden tests (T002/T004) assert the shipped template config deterministically (presence + key content). **LOW — SC-002 live behavior** (a generated repo's actual patch/minor increment) is exercised only under `HX_TEMPLATE_ROUNDTRIP=1`; the default gate verifies the static config. Disclosed consistently in spec/plan/tasks. Acceptable — the live behavior is independently proven in ergon/agentx.

### Fit with current architecture — PASS
Fits the existing template-content + `Directory.Build.props`/`$(Company)` symbol pattern; introduces no new project, namespace, layer, CLI command, or error code; no ArchUnit family or Sentrux boundary change (the touched files are Sentrux-excluded template content + test). A rule delta here would itself be drift — correctly absent.

## Verdict

**No BLOCKER, no HIGH.** One MEDIUM (instantiation) is mitigated by T001 being the first task; the LOWs are acceptable as noted. Cleared for `/07-doti-implement`.
