# Spec — 020 Scaffold versioning + copyright defaults

**Stage:** `/01-doti-specify`. WHAT/WHY only.

## Goal

A repo created by `hx new` should be **correct out of the box** for two things an operator currently has to hand-fix:

1. **Versioning** — start the version series at **0.1.0**, and let the existing doti increment model compute correctly: a **bug-fix-only cycle bumps the patch**, a **feature cycle bumps the minor**, with **no operator-authored `GitVersion.yml`**.
2. **Copyright** — the **release assembly carries the company copyright with the year set to the year the release was run** (auto-updating), with no operator-authored `<Copyright>`.

Both gaps were hit for real while scaffolding `agentx`: the generated repo shipped **no `GitVersion.yml`** and **no `<Copyright>`**, so the version series start, the bug→patch increment, and the assembly copyright all had to be added by hand. The same missing config caused a sibling repo (ergon) to compute a **minor** bump (0.2.0) on a bug-fix cycle that should have been **patch** (0.1.1), because with no config GitVersion falls back to its **GitFlow** default, under which a `develop`/`dev` branch is the next-minor integration branch.

This feature folds those fixes into the scaffold (and, for the copyright year, the tool) so every newly generated repo inherits them.

## User Scenarios & Testing

**Priority Mode** — **workflow / tooling** (dominant): fail-closed safety + deterministic proof before ergonomics. The deliverable is correct, deterministic *versioning* and a deterministic *copyright* in generated repos. Secondary aspect: **generated-code template** content (the template's `Directory.Build.props` + a new template `GitVersion.yml`).

### Work Item 1 — Generated repos version correctly out of the box (Priority: P1)

A generated repo, with no operator edits, starts at 0.1.0 and increments by cycle type: feature → minor, bug-fix-only → patch. This is the MVP slice — it removes the manual `GitVersion.yml` step and closes the latent GitFlow-default minor-bump trap that already bit ergon.

- **Why this priority:** Versioning correctness is a fail-closed safety property — a wrong default (minor on a bug fix, or a non-0.1.0 start) ships an incorrect release tag. The doti increment *model* already exists in the tool (FR-044/SC-016); what is missing is the per-repo GitVersion config that lets it compute correctly. Without it, every new repo is one `dev` branch away from ergon's bug.
- **Independent Test:** Generate a repo with `hx new`; run `hx version calculate` → `0.1.0`. Simulate a bug-fix-only cycle release → patch bump; simulate a feature-cycle release (with the doti `+semver: minor` transition signal) → minor bump. All without editing `GitVersion.yml`.
- **Acceptance Scenarios:**
  1. **Given** a freshly generated repo with no tags, **When** `hx version calculate` runs, **Then** the version is `0.1.0`.
  2. **Given** a generated repo released at v0.1.0, **When** a bug-fix-only cycle runs `hx release` (no `+semver` minor signal), **Then** the calculated version is `0.1.1` (patch).
  3. **Given** a generated repo released at v0.1.0, **When** a feature cycle's release transition writes `+semver: minor` and `hx release` runs, **Then** the calculated version is `0.2.0` (minor).
  4. **Given** a generated repo whose working branch is named `dev`, **When** a bug-fix-only cycle releases, **Then** the bump is still patch (the config does not treat `dev` as a next-minor integration branch).

### Work Item 2 — Generated release assemblies carry an auto-year company copyright (Priority: P2)

A generated repo's release assembly carries `Copyright © <release-year> <Company>`, where `<Company>` is the `--company` value and `<release-year>` is the year the release was produced — without the operator adding `<Copyright>`.

- **Why this priority:** A correct, auto-updating copyright is a public-release accuracy property; a hard-coded year silently goes stale and misstates the release. It depends on, but is independent of, WI1 (a repo can ship the copyright without the versioning change and vice-versa).
- **Independent Test:** Generate a repo, produce a release build, inspect the release assembly's `AssemblyCopyrightAttribute` → `Copyright © <current-release-year> <Company>`.
- **Acceptance Scenarios:**
  1. **Given** a repo generated with `--company Heurex`, **When** its release assembly is built/packed by `hx release`, **Then** the assembly's copyright is `Copyright © <year-of-release> Heurex`.
  2. **Given** the same repo released in a later calendar year, **When** it is re-released, **Then** the assembly copyright reflects the later year (auto-updating, not the original year).

### Edge Cases

- A generated repo whose first cycle is a **bug** cycle (no feature release yet): the first release must still be a sensible version (the 0.1.0 start floor applies).
- A repo that **already exists** (generated before this feature): it does NOT auto-receive the new `GitVersion.yml`/copyright — `GitVersion.yml` and `Directory.Build.props` are repo-root files, not reconciled `.doti` payload assets. Handled by scope exclusion + the operator (as done manually for ergon/agentx).
- An operator supplies an empty/odd `--company`: copyright falls back to a deterministic value (e.g. `<Company>` as given; an empty company is the operator's input, not this feature's concern).
- The vendored GitVersion binary is absent for the host RID: `hx version calculate`/`hx release` already fail closed — unchanged by this feature.

## Scope

**Included:**
- The dotnet-cli scaffold template ships a `GitVersion.yml` that: uses a **trunk-based workflow** (the trunk/`main` branch increments **Patch** by default), pins the series start to **0.1.0**, and preserves the existing doti `+semver: minor` feature signal so feature cycles bump minor.
- The template's `Directory.Build.props` (or the CLI project) gains a `<Copyright>` that resolves to the company copyright with the **release-year** auto-stamped.
- Whatever tool change (if any) is needed to stamp the **release-year** into the copyright at release time.
- Template golden/round-trip test coverage that fails closed if the generated `GitVersion.yml` or the copyright is missing or wrong.

**Excluded:**
- Changing speckit-doti's **own** versioning/copyright (it works; not part of the generated-repo defaults).
- Migrating **already-generated** repos (ergon, agentx, …) — they remain an operator fix.
- Changing the doti increment **model** itself (FR-044/SC-016 stay as-is; this feature only ships the per-repo config that lets the model compute correctly).
- **Enforcing** a branch strategy. Trunk-based (work on `main`) is the doti convention and the config makes versioning robust regardless of branch; this feature documents but does not police which branch an operator works on.

## Functional Requirements

- `FR-001`: The dotnet-cli scaffold template MUST include a GitVersion configuration file so a newly generated repo computes versions deterministically with no operator-authored config. `[WI1]`
- `FR-002`: The scaffolded GitVersion config MUST make the default (no-`+semver`-signal) increment on the trunk/release branch **Patch**, so a doti bug-fix-only cycle (which writes no `+semver` signal) bumps the patch. `[WI1]`
- `FR-003`: The scaffolded GitVersion config MUST preserve feature cycles bumping **minor** via the existing doti `+semver: minor` release-transition signal — no regression of FR-044/SC-016. `[WI1]`
- `FR-004`: A newly scaffolded repo's first release MUST produce version **0.1.0**. `[WI1]`
- `FR-005`: The scaffolded GitVersion config MUST NOT rely on GitVersion's default (unset) workflow, which is GitFlow and treats a `develop`/`dev` branch as the next-minor integration branch (the cause of the observed bug-cycle minor bump). It MUST select an explicit trunk-based workflow. `[WI1]`
- `FR-006`: Generated projects MUST set an assembly `Copyright` on the release assembly attributing the company (the `--company` value). `[WI2]`
- `FR-007`: The copyright year MUST reflect the **year the release was produced** — auto-updating, never a hard-coded constant that goes stale. The stamping **mechanism** (a build-time MSBuild date function in the template vs `hx release` injecting the release-time year at pack time) is a `/03-doti-plan` design decision, weighed on determinism + simplicity; both satisfy this WHAT. `[WI2]`
- `FR-008`: The copyright holder MUST flow from the existing `--company` input/`company` template symbol; generated repos MUST NOT hard-code a company name other than the template's default. `[WI2]`
- `FR-009`: The template golden and/or round-trip tests MUST assert the generated `GitVersion.yml` is present + correct AND the generated copyright is present + correct, so the scaffold cannot silently regress (today these properties have no test coverage). `[WI1, WI2]`

## Success Criteria

- `SC-001`: A repo generated by `hx new` and released once, with no operator edits to `GitVersion.yml`, produces a **v0.1.0** tag.
- `SC-002`: In a generated repo released at v0.1.0, a bug-fix-only cycle's `hx release` produces a **patch** bump (0.1.0 → 0.1.1) and a feature cycle produces a **minor** bump (0.1.0 → 0.2.0), neither requiring a manual `GitVersion.yml` edit, and a `dev`-named working branch does not change the bug-cycle result to minor.
- `SC-003`: A generated repo's release assembly carries a `Copyright` whose year equals the calendar year in which the release was produced.
- `SC-004`: Removing or corrupting either the generated `GitVersion.yml` or the generated copyright causes a template test to fail (regression guard).

## Deterministic Surfaces

- `scaffold/templates/dotnet-cli/GitVersion.yml` — **new** template asset (trunk-based workflow, 0.1.0 start). (Proven config in a generated repo: `workflow: GitHubFlow/v1` + `next-version: 0.1.0` + `assembly-*-scheme: MajorMinorPatch`.)
- `scaffold/templates/dotnet-cli/Directory.Build.props` — add `<Copyright>` (and/or the CLI `.csproj`).
- `scaffold/templates/dotnet-cli/.template.config/template.json` — possibly a `copyright`/`copyrightHolder` symbol if the holder is parameterised beyond `company`.
- `src/Hx.Scaffold.Core/Release/LocalReleaseService.cs` (`BuildGlobalToolChannel`) — only if FR-007 resolves to release-time year stamping (pass `-p:CopyrightYear=<year>`); records the stamped year in `release.identity.json`.
- `tools/Hx.Scaffold.Cli new` — no change expected (already substitutes `company`).
- `hx version calculate` / `hx release` — already implemented; this feature supplies the config they consume (GitVersion 6.7.0, vendored).
- `test/Hx.Templates.Tests/TemplateGoldenTests.cs` + `TemplateRoundTripTests.cs` — add the FR-009 assertions.

## Architecture Impact

- Template content under `scaffold/templates/dotnet-cli/**` is generated-code template (excluded from the scaffold repo's Sentrux graph). Adding a `GitVersion.yml` + a `<Copyright>` is config, not runtime logic.
- If FR-007 resolves to release-time stamping, `LocalReleaseService` (a `*.Core` type) changes — production code under the thin-CLI/Core boundary and the Sentrux graph; the year injection stays in Core, the CLI unchanged.
- Docs: README/CHANGELOG + the agent context note the new generated-repo defaults; `hx release --help` if a copyright-year flag/behavior is added.

## Sentrux And Hygiene Impact

- Template additions are Sentrux-excluded (generated template content), so no structural-signal change from the template files.
- A `LocalReleaseService` change (release-time stamping path) is production code Sentrux measures — keep the year-resolution a small, named, single-responsibility unit.
- The release-year is assembly **metadata**, not a gate-proof input (gate/affected-test proofs carry no temporal fields) — so the auto-year does not perturb any proof digest. A build-time year (FR-007 option a) would make assemblies non-reproducible across calendar years; a release-time year (option b) keeps the value bound to the release identity — this is the substance of the FR-007 clarification.

## Assumptions

- The doti cycle-type increment **model** (feature → `+semver: minor`; bug-fix-only → no signal → patch) is correct and stays; this feature only ships the per-repo GitVersion config that makes it compute, plus the 0.1.0 floor. (Evidence: FR-044/SC-016 in `docs/specs/007-…`; `CycleService.ReleaseSemverSignal`, `LocalReleaseVersionPolicy.DefaultIntent`; observed working in a sibling repo where `GitHubFlow/v1` + a cleared GitVersion cache produced v0.1.0 → v0.1.1 patch increments.)
- **GitHubFlow/v1** is the intended trunk-based workflow (main = Patch by default; no minor-tracking develop). (Evidence: GitVersion 6 docs — all presets make `main` Patch; only GitFlow adds a minor-tracking develop; `TrunkBased` is `preview1`, so `GitHubFlow/v1` is the stable trunk-based choice.) The plan may instead choose `TrunkBased/preview1` — a `/03-doti-plan` mechanism decision, not a spec decision.
- The copyright **holder** is the `--company` value (no separate holder input) unless FR-007/operator says otherwise.
- Already-generated repos are out of scope (operator-applied, as done for ergon/agentx).

## Acceptance

- Command-backed today: `hx version calculate` (computes the generated repo's version from the shipped `GitVersion.yml`), `hx release` (intent reconciliation + tag), template golden/round-trip tests, `gate run` (the blocking gate over the change set).
- Advisory until built: the release-year copyright stamping mechanism (FR-007) is unbuilt; do not claim it as proof until the chosen mechanism exists and a test asserts it.

## Clarifications

No open scope/policy clarifications. The copyright-year stamping mechanism (build-time MSBuild date function vs `hx release` release-time injection) is a HOW deferred to `/03-doti-plan` — recorded there with Decision / Rationale / Alternatives-rejected — not a `/02` scope ambiguity. The trunk-based workflow choice (`GitHubFlow/v1` vs `TrunkBased/preview1`) is likewise a plan-stage mechanism decision; the spec requires only an explicit non-GitFlow, patch-default workflow (FR-002, FR-005).
